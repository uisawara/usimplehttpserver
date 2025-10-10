#nullable enable
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Debug = UnityEngine.Debug;

namespace mmzkworks.SimpleHttpServer
{
    public sealed class SimpleHttpServer : IDisposable
    {
        private readonly IJsonSerializer _json;

        private readonly TcpListener _listener;
        private readonly List<Route> _routes = new();
        private CancellationTokenSource? _cts;

        public SimpleHttpServer(int port = 8080, IPAddress? bind = null, IJsonSerializer? json = null)
        {
            Port = port;
            BindAddress = bind ?? IPAddress.Loopback;
            _listener = new TcpListener(BindAddress, Port);
            _json = json ?? new JsonNetSerializer();
        }

        public int Port { get; }
        public IPAddress BindAddress { get; }
        public bool IsRunning { get; private set; }

        public void Dispose()
        {
            Stop();
        }

        public void RegisterControllersFrom(params Assembly[] assemblies)
        {
            if (assemblies == null || assemblies.Length == 0)
                assemblies = new[] { Assembly.GetExecutingAssembly() };

            foreach (var asm in assemblies)
            foreach (var type in asm.GetTypes().Where(t => t.IsClass && !t.IsAbstract))
                RegisterControllerClass(type);

            _routes.Sort((a, b) => b.SegmentCount.CompareTo(a.SegmentCount));
        }

        public void RegisterControllersFrom(Type[] types)
        {
            if (types == null || types.Length == 0) throw new ArgumentNullException();

            foreach (var type in types) RegisterControllerClass(type);

            _routes.Sort((a, b) => b.SegmentCount.CompareTo(a.SegmentCount));
        }

        public void RegisterControllersFrom(object[] instances)
        {
            if (instances == null || instances.Length == 0) throw new ArgumentNullException(nameof(instances));

            foreach (var instance in instances)
            {
                if (instance == null) continue;
                RegisterControllerInstance(instance);
            }

            _routes.Sort((a, b) => b.SegmentCount.CompareTo(a.SegmentCount));
        }

        private void RegisterControllerClass(Type type)
        {
            var prefixAttr = type.GetCustomAttribute<RoutePrefixAttribute>();
            var prefix = prefixAttr?.Prefix ?? "";
            object? instance = null;

            foreach (var m in type.GetMethods(BindingFlags.Instance | BindingFlags.Public))
            {
                foreach (var a in m.GetCustomAttributes<HttpGetAttribute>())
                {
                    instance ??= Activator.CreateInstance(type);
                    _routes.Add(Route.Create("GET", prefix, a.Template, instance!, m));
                }

                foreach (var a in m.GetCustomAttributes<HttpPostAttribute>())
                {
                    instance ??= Activator.CreateInstance(type);
                    _routes.Add(Route.Create("POST", prefix, a.Template, instance!, m));
                }

                foreach (var a in m.GetCustomAttributes<HttpPutAttribute>())
                {
                    instance ??= Activator.CreateInstance(type);
                    _routes.Add(Route.Create("PUT", prefix, a.Template, instance!, m));
                }

                foreach (var a in m.GetCustomAttributes<HttpDeleteAttribute>())
                {
                    instance ??= Activator.CreateInstance(type);
                    _routes.Add(Route.Create("DELETE", prefix, a.Template, instance!, m));
                }
            }
        }

        private void RegisterControllerInstance(object instance)
        {
            var type = instance.GetType();
            var prefixAttr = type.GetCustomAttribute<RoutePrefixAttribute>();
            var prefix = prefixAttr?.Prefix ?? "";

            foreach (var m in type.GetMethods(BindingFlags.Instance | BindingFlags.Public))
            {
                foreach (var a in m.GetCustomAttributes<HttpGetAttribute>())
                    _routes.Add(Route.Create("GET", prefix, a.Template, instance, m));

                foreach (var a in m.GetCustomAttributes<HttpPostAttribute>())
                    _routes.Add(Route.Create("POST", prefix, a.Template, instance, m));

                foreach (var a in m.GetCustomAttributes<HttpPutAttribute>())
                    _routes.Add(Route.Create("PUT", prefix, a.Template, instance, m));

                foreach (var a in m.GetCustomAttributes<HttpDeleteAttribute>())
                    _routes.Add(Route.Create("DELETE", prefix, a.Template, instance, m));
            }
        }

        public void Start()
        {
            if (IsRunning) return;
            _cts = new CancellationTokenSource();
            _listener.Start();
            IsRunning = true;
            Debug.Log($"[LightHttpServer] Listening on http://{BindAddress}:{Port}/");
            RunAcceptLoopAsync(_cts.Token).Forget();
        }

        public void Stop()
        {
            if (!IsRunning) return;
            _cts?.Cancel();
            try
            {
                _listener.Stop();
            }
            catch
            {
            }

            IsRunning = false;
        }

        private async UniTask RunAcceptLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    TcpClient client;
                    try
                    {
                        client = await _listener.AcceptTcpClientAsync().AsUniTask().AttachExternalCancellation(ct);
                    }
                    catch
                    {
                        break;
                    }

                    // 小さいパケットのまとめ待ちを無効化
                    client.NoDelay = true;
                    client.Client.LingerState = new LingerOption(false, 0); // Close時に即FIN

                    _ = HandleClientAsync(client, ct);
                }
            }
            finally
            {
                IsRunning = false;
            }
        }

        private async UniTask HandleClientAsync(TcpClient client, CancellationToken ct)
        {
            using (client)
            {
                client.NoDelay = true;
                using var stream = client.GetStream();

                try
                {
                    var (ok, method, rawTarget, headers) = await ReadHeadAsync(stream, ct);
                    if (!ok)
                    {
                        await WriteTextAsync(stream, 400, "Bad Request", "text/plain", ct);
                        return;
                    }

                    string path, query;
                    var q = rawTarget.IndexOf('?');
                    if (q >= 0)
                    {
                        path = Uri.UnescapeDataString(rawTarget[..q]);
                        query = rawTarget[(q + 1)..];
                    }
                    else
                    {
                        path = Uri.UnescapeDataString(rawTarget);
                        query = "";
                    }

                    if (path.Length == 0) path = "/";

                    var route = _routes.FirstOrDefault(r => r.Method == method && r.Regex.IsMatch(path));
                    if (route == null)
                    {
                        await WriteTextAsync(stream, 404, "Not Found", "text/plain", ct);
                        return;
                    }

                    var m = route.Regex.Match(path);
                    var routeVals = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    for (var i = 0; i < route.ParamNames.Length; i++)
                        routeVals[route.ParamNames[i]] = Uri.UnescapeDataString(m.Groups[i + 1].Value);

                    var queryVals = ParseQuery(query);

                    var body = "";
                    if (method != "POST" && method != "PUT" && method != "PATCH" &&
                        headers.TryGetValue("Content-Length", out var lenStr) &&
                        int.TryParse(lenStr, out var len) &&
                        len > 0)
                    {
                        var buf = new byte[len];
                        var read = 0;
                        while (read < len)
                        {
                            var r = await stream.ReadAsync(buf, read, len - read, ct);
                            if (r <= 0) break;
                            read += r;
                        }

                        body = Encoding.UTF8.GetString(buf, 0, read);
                    }

                    var args = new object?[route.Parameters.Length];
                    for (var i = 0; i < route.Parameters.Length; i++)
                    {
                        var p = route.Parameters[i];
                        var fromBody = p.GetCustomAttribute<FromBodyAttribute>() != null;
                        if (fromBody)
                        {
                            args[i] = string.IsNullOrWhiteSpace(body)
                                ? GetDefault(p.ParameterType)
                                : _json.Deserialize(body, p.ParameterType);
                            continue;
                        }

                        string? raw = null;
                        if (routeVals.TryGetValue(p.Name!, out var rv)) raw = rv;
                        else if (queryVals.TryGetValue(p.Name!, out var qv)) raw = qv;
                        args[i] = raw == null
                            ? p.HasDefaultValue ? p.DefaultValue : GetDefault(p.ParameterType)
                            : ConvertTo(raw, p.ParameterType);
                    }

                    object? result;
                    try
                    {
                        result = route.MethodInfo.Invoke(route.Target, args);
                    }
                    catch (TargetInvocationException tie)
                    {
                        await WriteTextAsync(stream, 500,
                            "Internal Error: " + (tie.InnerException?.Message ?? "invoke"), "text/plain", ct);
                        return;
                    }

                    if (result is ContentResult contentResult)
                    {
                        await WriteTextAsync(stream, contentResult.StatusCode, contentResult.Content,
                            contentResult.ContentType, ct);
                    }
                    else if (result is string s)
                    {
                        await WriteTextAsync(stream, 200, s, "text/plain; charset=utf-8", ct);
                    }
                    else
                    {
                        var json = _json.Serialize(result);
                        await WriteTextAsync(stream, 200, json, "application/json; charset=utf-8", ct);
                    }
                }
                catch (Exception e)
                {
                    try
                    {
                        await WriteTextAsync(stream, 500, "Internal Server Error: " + e.Message, "text/plain", ct);
                    }
                    catch
                    {
                    }
                }
            }
        }

        // --- util ---
        private static Dictionary<string, string> ParseQuery(string q)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(q)) return dict;
            foreach (var kv in q.Split('&'))
            {
                if (string.IsNullOrEmpty(kv)) continue;
                var idx = kv.IndexOf('=');
                if (idx < 0) dict[Uri.UnescapeDataString(kv)] = "";
                else dict[Uri.UnescapeDataString(kv[..idx])] = Uri.UnescapeDataString(kv[(idx + 1)..]);
            }

            return dict;
        }

        private static object? ConvertTo(string raw, Type t)
        {
            t = Nullable.GetUnderlyingType(t) ?? t;
            if (t.IsEnum) return Enum.Parse(t, raw, true);
            if (t == typeof(Guid)) return Guid.Parse(raw);
            if (t == typeof(DateTime))
                return DateTime.Parse(raw, null, DateTimeStyles.RoundtripKind);
            if (t == typeof(bool)) return bool.Parse(raw);
            if (t == typeof(string)) return raw;
            return Convert.ChangeType(raw, t);
        }

        private static object? GetDefault(Type t)
        {
            return t.IsValueType ? Activator.CreateInstance(t) : null;
        }

        private static async UniTask<(bool ok, string method, string target, Dictionary<string, string> headers)>
            ReadHeadAsync(NetworkStream stream, CancellationToken ct)
        {
            const int MaxHeaderBytes = 64 * 1024;
            const int ChunkSize = 4096;

            var pool = ArrayPool<byte>.Shared;
            var buf = pool.Rent(MaxHeaderBytes);
            var filled = 0;
            var headerEnd = -1;

            try
            {
                // ---- まとめ読み & \r\n\r\n 検出（跨り対応）----
                while (true)
                {
                    if (filled >= MaxHeaderBytes)
                        return (false, "", "", new Dictionary<string, string>());

                    var toRead = Math.Min(ChunkSize, MaxHeaderBytes - filled);
#if NETSTANDARD2_1_OR_GREATER || NET6_0_OR_GREATER
            int read = await stream.ReadAsync(buf.AsMemory(filled, toRead), ct);
#else
                    var read = await stream.ReadAsync(buf, filled, toRead, ct);
#endif
                    if (read <= 0)
                        return (false, "", "", new Dictionary<string, string>());

                    var scanStart = Math.Max(0, filled - 3);
                    filled += read;

                    var idx = FindCrlfCrlf(buf, scanStart, filled);
                    if (idx >= 0)
                    {
                        headerEnd = idx + 4;
                        break;
                    }
                }

                // ---- リクエストライン ----
                var line0End = FindCrlf(buf, 0, headerEnd);
                if (line0End <= 0) return (false, "", "", new Dictionary<string, string>());

                // METHOD SP TARGET SP VERSION
                var sp1 = IndexOfByte(buf, (byte)' ', 0, line0End);
                if (sp1 <= 0) return (false, "", "", new Dictionary<string, string>());
                var sp2 = IndexOfByte(buf, (byte)' ', sp1 + 1, line0End);
                if (sp2 < 0) return (false, "", "", new Dictionary<string, string>());

                var method = Encoding.ASCII.GetString(buf, 0, sp1).ToUpperInvariant();
                var target = Encoding.ASCII.GetString(buf, sp1 + 1, sp2 - (sp1 + 1));
                // string version = Encoding.ASCII.GetString(buf, sp2 + 1, line0End - (sp2 + 1)); // 必要なら

                // ---- ヘッダ行 ----
                var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var pos = line0End + 2; // 次行先頭

                while (pos < headerEnd - 2)
                {
                    var eol = FindCrlf(buf, pos, headerEnd);
                    if (eol < 0) break;
                    if (eol == pos) break; // 空行（終端）

                    var colon = IndexOfByte(buf, (byte)':', pos, eol);
                    if (colon > pos)
                    {
                        // key
                        var kb = TrimAsciiBounds(buf, pos, colon);
                        // value（':' の後から）
                        var vb = TrimAsciiBounds(buf, colon + 1, eol);

                        if (kb.len > 0)
                        {
                            var key = Encoding.ASCII.GetString(buf, kb.start, kb.len);
                            var val = vb.len > 0 ? Encoding.ASCII.GetString(buf, vb.start, vb.len) : string.Empty;
                            headers[key] = val;
                        }
                    }

                    pos = eol + 2;
                }

                return (true, method, target, headers);
            }
            finally
            {
                pool.Return(buf);
            }
        }

// ========= ヘルパ（クラススコープ、Span未使用） =========

        private static int FindCrlf(byte[] b, int start, int end)
        {
            // [start, end) の中で \r\n を検索
            for (var i = start; i + 1 < end; i++)
                if (b[i] == 13 && b[i + 1] == 10)
                    return i;
            return -1;
        }

        private static int FindCrlfCrlf(byte[] b, int start, int end)
        {
            // [start, end) の中で \r\n\r\n を検索
            for (var i = start; i + 3 < end; i++)
                if (b[i] == 13 && b[i + 1] == 10 && b[i + 2] == 13 && b[i + 3] == 10)
                    return i;
            return -1;
        }

        private static int IndexOfByte(byte[] b, byte value, int start, int end)
        {
            for (var i = start; i < end; i++)
                if (b[i] == value)
                    return i;
            return -1;
        }

        private static (int start, int len) TrimAsciiBounds(byte[] b, int start, int end)
        {
            // [start, end) 範囲から ASCII の空白（space/tab）を左右トリムして返す
            int s = start, e = end - 1;
            while (s <= e && (b[s] == 32 || b[s] == 9)) s++;
            while (e >= s && (b[e] == 32 || b[e] == 9)) e--;
            var len = Math.Max(0, e - s + 1);
            return (s, len);
        }

        private static async UniTask WriteTextAsync(NetworkStream stream, int status, string text, string contentType,
            CancellationToken ct)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            await WriteHeadAsync(stream, status, contentType, bytes.Length, ct);
            await stream.WriteAsync(bytes, 0, bytes.Length, ct);
        }

        private static async UniTask WriteHeadAsync(NetworkStream stream, int status, string contentType,
            int contentLength,
            CancellationToken ct)
        {
            var sb = new StringBuilder();
            sb.Append("HTTP/1.1 ").Append(status).Append(' ').Append("OK").Append("\r\n");
            sb.Append("Content-Type: ").Append(contentType).Append("\r\n");
            sb.Append("Content-Length: ").Append(contentLength).Append("\r\n");
            sb.Append("Connection: close\r\n");

            // CORS header
            sb.Append("Access-Control-Allow-Origin: *\r\n");
            sb.Append("Access-Control-Allow-Methods: GET, POST, PUT, DELETE, OPTIONS\r\n");
            sb.Append("Access-Control-Allow-Headers: Content-Type, Authorization\r\n");

            // Terminate header
            sb.Append("\r\n");

            var head = Encoding.ASCII.GetBytes(sb.ToString());
            await stream.WriteAsync(head, 0, head.Length, ct);
        }
    }
}