using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Gauge.CSharp.Lib;
using Gauge.CSharp.Lib.Attribute;
using Shouldly;

namespace netcore.template
{
    public class StepImplementation
    {
        private Process _serverProcess;
        private static readonly HttpClient _http = new HttpClient();
        private string _baseUrl = "http://127.0.0.1:8080";
        private int _port = 8080;

        [Step("Start server process <exeRelativePath> on port <port>")]
        public void StartServerProcess(string exeRelativePath, int port)
        {
            _port = port;
            _baseUrl = $"http://127.0.0.1:{_port}";

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
                Arguments = $"--enableServer --serverPort {_port}",
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(fullPath)
            };

            _serverProcess = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            var started = _serverProcess.Start();
            started.ShouldBeTrue("Failed to start server process");
        }

        [Step("Wait until endpoint <path> returns 200 within <seconds> seconds")]
        public void WaitUntilReady(string path, int seconds)
        {
            var deadline = DateTime.UtcNow.AddSeconds(seconds);
            var url = new Uri(new Uri(_baseUrl), path).ToString();

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

        [Step("GET <path> should return <expected>")]
        public void GetShouldReturn(string path, string expected)
        {
            var url = new Uri(new Uri(_baseUrl), path).ToString();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var resp = _http.GetAsync(url, cts.Token).GetAwaiter().GetResult();
            ((int)resp.StatusCode).ShouldBe(200);
            var body = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            body.ShouldBe(expected);
        }

        [Step("Stop server process")]
        public void StopServerProcess()
        {
            try
            {
                if (_serverProcess != null && !_serverProcess.HasExited)
                {
                    _serverProcess.CloseMainWindow();
                    if (!_serverProcess.WaitForExit(2000))
                    {
                        _serverProcess.Kill(true);
                        _serverProcess.WaitForExit(2000);
                    }
                }
            }
            finally
            {
                _serverProcess?.Dispose();
                _serverProcess = null;
            }
        }
    }
}
