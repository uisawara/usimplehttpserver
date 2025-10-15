#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;

namespace mmzkworks.SimpleHttpServer.Tests
{
    public sealed class BenchmarkTests
    {
        [UnityTest]
        public IEnumerator Ping_Benchmark_100x_10cc_Succeeds()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var port = GetFreePort();
                using var server = new SimpleHttpServer(port);
                server.RegisterControllersFrom(new[]
                {
                    new BenchmarkController()
                });
                server.Start();
                await UniTask.Delay(100);

                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                var baseUrl = $"http://127.0.0.1:{port}";
                var requestCount = 100;
                var concurrency = 10;

                var sem = new SemaphoreSlim(concurrency);
                var succeeded = 0;
                var failed = 0;
                var durations = new List<long>(requestCount);
                var tasks = new List<UniTask>(requestCount);

                for (var i = 0; i < requestCount; i++)
                {
                    await sem.WaitAsync();
                    tasks.Add(UniTask.RunOnThreadPool(async () =>
                    {
                        var sw = Stopwatch.StartNew();
                        try
                        {
                            var res = await http.GetAsync(baseUrl + "/bench/ping");
                            res.EnsureSuccessStatusCode();
                            var body = await res.Content.ReadAsStringAsync();
                            Assert.AreEqual("pong", body);
                            Interlocked.Increment(ref succeeded);
                        }
                        catch
                        {
                            Interlocked.Increment(ref failed);
                        }
                        finally
                        {
                            sw.Stop();
                            lock (durations)
                            {
                                durations.Add(sw.ElapsedMilliseconds);
                            }

                            sem.Release();
                        }
                    }));
                }

                await UniTask.WhenAll(tasks);
                // 集計（平均・中央値・最小・最大）
                durations.Sort();
                var count = durations.Count;
                var min = count > 0 ? durations[0] : 0;
                var max = count > 0 ? durations[count - 1] : 0;
                var avg = count > 0 ? durations.Average(d => (double)d) : 0.0;
                var median = 0.0;
                if (count > 0)
                {
                    if ((count & 1) == 1) median = durations[count / 2];
                    else median = (durations[count / 2 - 1] + durations[count / 2]) / 2.0;
                }

                Debug.Log(
                    $"[Benchmark] requests={requestCount}, concurrency={concurrency}, success={succeeded}, fail={failed}, min={min}ms, median={median:F2}ms, avg={avg:F2}ms, max={max}ms");

                server.Stop();

                Assert.AreEqual(requestCount, succeeded, $"failed={failed}");
                Assert.Greater(durations.Count, 0);
            });
        }

        private static int GetFreePort()
        {
            var l = new TcpListener(IPAddress.Loopback, 0);
            l.Start();
            var port = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }
    }
}