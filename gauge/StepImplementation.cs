using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Gauge.CSharp.Lib;
using Gauge.CSharp.Lib.Attribute;
using Shouldly;

namespace netcore.template
{
    public class StepImplementation
    {
        private sealed class ServerInfo
        {
            public Process Process { get; set; }
            public string BaseUrl { get; set; }
            public int Port { get; set; }
        }

        private readonly Dictionary<string, ServerInfo> _servers = new(StringComparer.OrdinalIgnoreCase);
        private static readonly HttpClient _http = new HttpClient();

        [Step("Start server <name> process <exeRelativePath> on port <port>")]
        public void StartServerProcess(string name, string exeRelativePath, int port)
        {
            name.ShouldNotBeNullOrWhiteSpace();
            if (_servers.TryGetValue(name, out var existing) && existing.Process != null && !existing.Process.HasExited)
            {
                throw new InvalidOperationException($"Server '{name}' is already running (PID {existing.Process.Id}).");
            }

            // Guard: prevent multiple processes on the same port (both known and external)
            foreach (var kv in _servers)
            {
                var s = kv.Value;
                if (s.Port == port && s.Process != null && !s.Process.HasExited)
                    throw new InvalidOperationException($"Port {port} is already used by server '{kv.Key}'. Use a different port.");
            }
            if (IsPortInUse(port))
            {
                throw new InvalidOperationException($"Port {port} is already in use by another process. Choose a different port.");
            }

            var cwd = Directory.GetCurrentDirectory();
            var fullPath = Path.GetFullPath(exeRelativePath, cwd);
            if (!File.Exists(fullPath))
            {
                // Try resolve from repository root (one directory up from gauge/)
                var alt = Path.GetFullPath(Path.Combine(cwd, "..", exeRelativePath));
                if (File.Exists(alt))
                {
                    fullPath = alt;
                }
            }
            fullPath.ShouldNotBeNull();
            File.Exists(fullPath).ShouldBeTrue($"EXE not found: {fullPath}");

            var startInfo = new ProcessStartInfo
            {
                FileName = fullPath,
                Arguments = $"--enableServer --serverPort {port}",
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(fullPath)
            };

            var p = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            var started = p.Start();
            started.ShouldBeTrue("Failed to start server process");

            _servers[name] = new ServerInfo
            {
                Process = p,
                BaseUrl = $"http://127.0.0.1:{port}",
                Port = port
            };
        }

        [Step("Wait until endpoint <path> on <name> returns 200 within <seconds> seconds")]
        public void WaitUntilReady(string path, string name, int seconds)
        {
            var s = GetServer(name);
            var deadline = DateTime.UtcNow.AddSeconds(seconds);
            var url = new Uri(new Uri(s.BaseUrl), path).ToString();

            Exception lastError = null;
            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    var resp = _http.GetAsync(url, cts.Token).GetAwaiter().GetResult();
                    if ((int)resp.StatusCode == 200)
                    {
                        return;
                    }
                }
                catch (Exception ex)
                {
                    lastError = ex;
                }

                Thread.Sleep(500);
            }

            throw new Exception($"Endpoint not ready within {seconds}s: {url}", lastError);
        }

        [Step("GET <path> on <name> should return <expected>")]
        public void GetShouldReturn(string path, string name, string expected)
        {
            var s = GetServer(name);
            var url = new Uri(new Uri(s.BaseUrl), path).ToString();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var resp = _http.GetAsync(url, cts.Token).GetAwaiter().GetResult();
            ((int)resp.StatusCode).ShouldBe(200);
            var body = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            body.ShouldBe(expected);
        }

        [Step("Stop server <name> process")]
        public void StopServerProcess(string name)
        {
            if (!_servers.TryGetValue(name, out var s)) return;

            try
            {
                if (s.Process != null && !s.Process.HasExited)
                {
                    s.Process.CloseMainWindow();
                    if (!s.Process.WaitForExit(2000))
                    {
                        s.Process.Kill(true);
                        s.Process.WaitForExit(2000);
                    }
                }
            }
            finally
            {
                s.Process?.Dispose();
                _servers.Remove(name);
            }
        }

		[AfterScenario]
		public void CleanupAllServers()
		{
			// Ensure all started servers in this scenario are terminated
			foreach (var kv in _servers.ToArray())
			{
				try
				{
					StopServerProcess(kv.Key);
				}
				catch
				{
					// swallow to avoid masking original scenario failure
				}
			}
		}

        private ServerInfo GetServer(string name)
        {
            if (!_servers.TryGetValue(name, out var s))
                throw new KeyNotFoundException($"Server '{name}' not found. Start it first.");
            return s;
        }

        private static bool IsPortInUse(int port)
        {
            try
            {
                using var client = new TcpClient();
                var ar = client.BeginConnect("127.0.0.1", port, null, null);
                if (!ar.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(200)))
                {
                    return false; // timeout -> assume not in use
                }
                client.EndConnect(ar);
                return true; // connected -> something is listening
            }
            catch
            {
                return false;
            }
        }
    }
}
