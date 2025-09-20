#nullable enable
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace mmzkworks.SimpleHttpServer.Tests
{
	public static class BenchmarkRunner
	{
		[MenuItem("Tools/uSimpleHttpServer/Run Benchmarks")] 
		public static async void RunBenchmarksMenu()
		{
			try
			{
				var port = 8080;
				var baseUrl = $"http://127.0.0.1:{port}";
				var result = await RunAsync(baseUrl, requestCount: 200, concurrency: 20, timeoutMs: 5000);
				EditorUtility.DisplayDialog("Benchmark", FormatResult(result), "OK");
			}
			catch (Exception ex)
			{
				UnityEngine.Debug.LogError($"[uSimpleHttpServer] Benchmark failed: {ex}");
				EditorUtility.DisplayDialog("Benchmark", $"Failed: {ex.Message}", "OK");
			}
		}

		public sealed class BenchResult
		{
			public int Requested;
			public int Succeeded;
			public int Failed;
			public double P50Ms;
			public double P95Ms;
			public double P99Ms;
			public double AvgMs;
			public long TotalElapsedMs;
			public double Rps;
		}

		public static async Task<BenchResult> RunAsync(string baseUrl, int requestCount, int concurrency, int timeoutMs)
		{
			ServicePointManagerHelper.Ensure();
			using var http = new HttpClient { Timeout = TimeSpan.FromMilliseconds(timeoutMs) };
			var durations = new List<long>(requestCount);
			var succeeded = 0;
			var failed = 0;
			var totalSw = Stopwatch.StartNew();

			var sem = new SemaphoreSlim(concurrency);
			var tasks = new List<Task>(requestCount);
			for (var i = 0; i < requestCount; i++)
			{
				await sem.WaitAsync();
				tasks.Add(Task.Run(async () =>
				{
					var sw = Stopwatch.StartNew();
					try
					{
						var res = await http.GetAsync(baseUrl + "/bench/ping");
						res.EnsureSuccessStatusCode();
						var _ = await res.Content.ReadAsStringAsync();
						Interlocked.Increment(ref succeeded);
					}
					catch
					{
						Interlocked.Increment(ref failed);
					}
					finally
					{
						sw.Stop();
						lock (durations) durations.Add(sw.ElapsedMilliseconds);
						sem.Release();
					}
				}));
			}
			await Task.WhenAll(tasks);
			totalSw.Stop();

			durations.Sort();
			double Percentile(double p)
			{
				if (durations.Count == 0) return 0;
				var rank = (p / 100.0) * (durations.Count - 1);
				var lo = (int)Math.Floor(rank);
				var hi = (int)Math.Ceiling(rank);
				if (lo == hi) return durations[lo];
				var w = rank - lo;
				return durations[lo] * (1 - w) + durations[hi] * w;
			}

			var avg = durations.Count == 0 ? 0 : (double)Sum(durations) / durations.Count;

			return new BenchResult
			{
				Requested = requestCount,
				Succeeded = succeeded,
				Failed = failed,
				P50Ms = Percentile(50),
				P95Ms = Percentile(95),
				P99Ms = Percentile(99),
				AvgMs = avg,
				TotalElapsedMs = totalSw.ElapsedMilliseconds,
				Rps = totalSw.ElapsedMilliseconds > 0 ? succeeded * 1000.0 / totalSw.ElapsedMilliseconds : 0,
			};
		}

		private static long Sum(List<long> list)
		{
			long s = 0; foreach (var v in list) s += v; return s;
		}

		private static string FormatResult(BenchResult r)
		{
			return $"Requests: {r.Requested}\nSuccess: {r.Succeeded}, Fail: {r.Failed}\nTotal: {r.TotalElapsedMs} ms, RPS: {r.Rps:F1}\nAvg: {r.AvgMs:F2} ms\nP50: {r.P50Ms:F2} ms, P95: {r.P95Ms:F2} ms, P99: {r.P99Ms:F2} ms";
		}
	}

	internal static class ServicePointManagerHelper
	{
		private static bool _inited;
		public static void Ensure()
		{
			if (_inited) return;
			_inited = true;
			// Editor上の .NET で大量HTTPを安定させるための調整
			System.Net.ServicePointManager.DefaultConnectionLimit = Math.Max(100, System.Net.ServicePointManager.DefaultConnectionLimit);
		}
	}
}
#endif


