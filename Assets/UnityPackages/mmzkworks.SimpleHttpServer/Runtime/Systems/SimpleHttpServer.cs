#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace UnityPackages.mmzkworks.SimpleHttpServer.Runtime
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

            _routes.Sort((a, b) => b.SegmentCount.CompareTo(a.SegmentCount));
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
                    if (headers.TryGetValue("Content-Length", out var lenStr) && int.TryParse(lenStr, out var len) &&
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

                    if (result is string s)
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
            ReadHeadAsync(
                NetworkStream stream, CancellationToken ct)
        {
            using var ms = new MemoryStream(4096);
            var buf = new byte[1];
            var state = 0;
            while (true)
            {
                var r = await stream.ReadAsync(buf, 0, 1, ct);
                if (r <= 0) break;
                ms.WriteByte(buf[0]);
                state = (state << 8) | buf[0];
                if ((state & 0xFFFFFFFF) == 0x0D0A0D0A) break;
                if (ms.Length > 64 * 1024) break;
            }

            var head = Encoding.ASCII.GetString(ms.ToArray());
            var lines = head.Split(new[] { "\r\n" }, StringSplitOptions.None);
            if (lines.Length == 0 || string.IsNullOrEmpty(lines[0]))
                return (false, "", "", new Dictionary<string, string>());
            var first = lines[0].Split(' ');
            if (first.Length < 3) return (false, "", "", new Dictionary<string, string>());
            var method = first[0].ToUpperInvariant();
            var target = first[1];
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrEmpty(line)) break;
                var idx = line.IndexOf(':');
                if (idx > 0) headers[line[..idx].Trim()] = line[(idx + 1)..].Trim();
            }

            return (true, method, target, headers);
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
            sb.Append("Connection: close\r\n\r\n");
            var head = Encoding.ASCII.GetBytes(sb.ToString());
            await stream.WriteAsync(head, 0, head.Length, ct);
        }
    }
}