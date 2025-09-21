#nullable enable
using System;
using System.Diagnostics;
using System.Linq;
using UnityPackages.mmzkworks.SimpleHttpServer.Runtime;

namespace mmzkworks.SimpleHttpServer.Tests
{
	[RoutePrefix("/bench")] 
	public sealed class BenchmarkController
	{
		// GET /bench/ping -> "pong"
		[HttpGet("/ping")]
		public string Ping()
		{
			return "pong";
		}

		// GET /bench/calc/{n} -> { input:n, sum:... }
		[HttpGet("/calc/{n}")]
		public object Calc(int n)
		{
			if (n < 0) n = 0;
			if (n > 1_000_000) n = 1_000_000;
			var sum = 0L;
			for (var i = 1; i <= n; i++) sum += i;
			return new { input = n, sum };
		}

		// GET /bench/cpu?ms=10 -> 指定msだけ忙しく回す
		[HttpGet("/cpu")]
		public object Cpu(int ms = 10)
		{
			if (ms < 0) ms = 0; if (ms > 10_000) ms = 10_000;
			var sw = Stopwatch.StartNew();
			var x = 0.0;
			while (sw.ElapsedMilliseconds < ms)
			{
				x = Math.Sqrt(x + 1.2345);
			}
			sw.Stop();
			return new { requestedMs = ms, elapsedMs = sw.ElapsedMilliseconds, result = x };
		}
	}
}


