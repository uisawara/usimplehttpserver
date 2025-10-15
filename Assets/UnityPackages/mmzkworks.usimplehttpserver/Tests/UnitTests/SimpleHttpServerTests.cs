using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.TestTools;

namespace mmzkworks.SimpleHttpServer.Tests
{
    public class SimpleHttpServerTests
    {
        private static int GetFreeTcpPort()
        {
            var l = new TcpListener(IPAddress.Loopback, 0);
            l.Start();
            var port = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }

        private static IEnumerator HttpGetUwr(int port, string path, Action<int, string> onResult,
            float timeoutSeconds = 5f)
        {
            if (!path.StartsWith("/")) path = "/" + path;
            var url = $"http://127.0.0.1:{port}{path}";
            using var req = UnityWebRequest.Get(url);
            req.timeout = Mathf.CeilToInt(timeoutSeconds);
            yield return req.SendWebRequest();
            var status = (int)req.responseCode;
            var body = req.downloadHandler != null ? req.downloadHandler.text : string.Empty;
            onResult?.Invoke(status, body);
        }

        [Test]
        public void StartStop_UpdatesIsRunning()
        {
            var port = GetFreeTcpPort();
            var server = new SimpleHttpServer(port, IPAddress.Loopback);
            try
            {
                Assert.False(server.IsRunning);
                server.Start();
                Thread.Sleep(100);
                Assert.True(server.IsRunning);
            }
            finally
            {
                server.Stop();
                Assert.False(server.IsRunning);
            }
        }

        [UnityTest]
        public IEnumerator GetRoute_ReturnsStringBody()
        {
            var port = GetFreeTcpPort();
            var server = new SimpleHttpServer(port, IPAddress.Loopback);
            server.RegisterControllersFrom(new[] { typeof(TestController) });
            try
            {
                server.Start();
                yield return null; // accept loop 起動待ち
                var status = 0;
                var body = string.Empty;
                yield return HttpGetUwr(port, "/api/test/hello", (s, b) =>
                {
                    status = s;
                    body = b;
                });
                Assert.AreEqual(200, status);
                Assert.AreEqual("world", body);
            }
            finally
            {
                server.Stop();
            }
        }

        [UnityTest]
        public IEnumerator GetRoute_WithRouteParams_ReturnsJsonNumber()
        {
            var port = GetFreeTcpPort();
            var server = new SimpleHttpServer(port, IPAddress.Loopback);
            server.RegisterControllersFrom(new[] { typeof(TestController) });
            try
            {
                server.Start();
                yield return null;
                var status = 0;
                var body = string.Empty;
                yield return HttpGetUwr(port, "/api/test/sum/2/3", (s, b) =>
                {
                    status = s;
                    body = b;
                });
                Assert.AreEqual(200, status);
                Assert.AreEqual("5", body);
            }
            finally
            {
                server.Stop();
            }
        }

        [UnityTest]
        public IEnumerator NotFound_Returns404()
        {
            var port = GetFreeTcpPort();
            var server = new SimpleHttpServer(port, IPAddress.Loopback);
            try
            {
                server.Start();
                yield return null;
                var status = 0;
                var body = string.Empty;
                yield return HttpGetUwr(port, "/nope", (s, b) =>
                {
                    status = s;
                    body = b;
                });
                Assert.AreEqual(404, status);
            }
            finally
            {
                server.Stop();
            }
        }

        [UnityTest]
        public IEnumerator RegisterControllersFromInstances_UsesProvidedInstance()
        {
            var port = GetFreeTcpPort();
            var server = new SimpleHttpServer(port, IPAddress.Loopback);
            server.RegisterControllersFrom(new object[] { new InstanceController("instance") });
            try
            {
                server.Start();
                yield return null;
                var status = 0;
                var body = string.Empty;
                yield return HttpGetUwr(port, "/id", (s, b) =>
                {
                    status = s;
                    body = b;
                });
                Assert.AreEqual(200, status);
                Assert.AreEqual("instance", body);
            }
            finally
            {
                server.Stop();
            }
        }

        [RoutePrefix("api/test")]
        private class TestController
        {
            [HttpGet("hello")]
            public string Hello()
            {
                return "world";
            }

            [HttpGet("sum/{a}/{b}")]
            public int Sum(int a, int b)
            {
                return a + b;
            }
        }

        private class InstanceController
        {
            private readonly string _value;

            public InstanceController(string value)
            {
                _value = value;
            }

            [HttpGet("id")]
            public string Id()
            {
                return _value;
            }
        }
    }
}