#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using mmzkworks.SimpleHttpServer.OpenApi;

namespace mmzkworks.SimpleHttpServer
{
    [RoutePrefix("")]
    public sealed class OpenApiController
    {
        private static string? s_cachedYaml;
        private static DateTime s_cachedAt;

        // GET /openapi.yml
        [HttpGet("/openapi.yml")]
        public object GetOpenApiYaml()
        {
            if (!string.IsNullOrEmpty(s_cachedYaml))
                return new ContentResult(s_cachedYaml!, "application/yaml; charset=utf-8", 200);

            var asms = AppDomain.CurrentDomain
                .GetAssemblies()
                .Where(a => a.GetName().Name == "Assembly-CSharp")
                .DefaultIfEmpty(Assembly.GetExecutingAssembly())
                .ToArray();

            var doc = RuntimeOpenApiGenerator.Build("uSimpleHttpServer API", "v0.1.0", asms);
            var yaml = SimpleYamlRuntimeEmitter.WriteToString(doc);

            s_cachedYaml = yaml;
            s_cachedAt = DateTime.UtcNow;
            return new ContentResult(yaml, "application/yaml; charset=utf-8", 200);
        }
    }
}


