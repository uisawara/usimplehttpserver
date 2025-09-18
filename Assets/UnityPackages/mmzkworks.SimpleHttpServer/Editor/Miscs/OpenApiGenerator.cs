#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityPackages.mmzkworks.SimpleHttpServer.Runtime;


namespace mmzkworks.SimpleHttpServer.OpenApi
{
    public static class OpenApiGenerator
    {
        static readonly Regex PathParamRx = new(@"\{([^}]+)\}", RegexOptions.Compiled);

        public static Dictionary<string, object?> BuildOpenApi(string title, string version,
            IEnumerable<Assembly> scanAssemblies)
        {
            var schemas = new Dictionary<string, object?>(StringComparer.Ordinal);
            var paths = new Dictionary<string, object?>(StringComparer.Ordinal);

            foreach (var asm in scanAssemblies)
            foreach (var type in asm.GetTypes())
            {
                var prefixAttr = type.GetCustomAttribute<RoutePrefixAttribute>();
                if (prefixAttr == null) continue;

                var ctrlSummary = type.GetCustomAttribute<SummaryAttribute>()?.Text;
                var ctrlTags = type.GetCustomAttributes<TagAttribute>().Select(t => t.Name).ToArray();

                foreach (var m in type.GetMethods(BindingFlags.Instance | BindingFlags.Public |
                                                  BindingFlags.DeclaredOnly))
                {
                    var (http, tmpl) = GetHttpAttr(m);
                    if (http == null || tmpl == null) continue;

                    var fullPath = NormalizePath(prefixAttr.Prefix, tmpl);
                    if (!paths.TryGetValue(fullPath, out var piObj))
                        paths[fullPath] = piObj = new Dictionary<string, object?>(StringComparer.Ordinal);
                    var pathItem = (Dictionary<string, object?>)piObj!;

                    var op = new Dictionary<string, object?>();
                    var summary = m.GetCustomAttribute<SummaryAttribute>()?.Text ?? ctrlSummary;
                    AddIfNotNull(op, "summary", summary);

                    var tags = ctrlTags.Length > 0
                        ? ctrlTags
                        : m.GetCustomAttributes<TagAttribute>().Select(t => t.Name).ToArray();
                    if (tags.Length > 0) op["tags"] = tags;

                    var parameters = new List<object?>();
                    var routeParams = PathParamRx.Matches(fullPath).Select(x => x.Groups[1].Value)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
                    Dictionary<string, object?>? requestBody = null;

                    foreach (var p in m.GetParameters())
                    {
                        var inBody = p.GetCustomAttribute<FromBodyAttribute>() != null;
                        if (inBody)
                        {
                            requestBody ??= new Dictionary<string, object?>
                            {
                                ["required"] = true,
                                ["content"] = new Dictionary<string, object?>
                                {
                                    ["application/json"] = new Dictionary<string, object?>
                                    {
                                        ["schema"] = EnsureSchema(schemas, p.ParameterType)
                                    }
                                }
                            };
                            continue;
                        }

                        var location = routeParams.Contains(p.Name!) ? "path" : "query";
                        var pObj = new Dictionary<string, object?>();
                        pObj["name"] = p.Name;
                        pObj["in"] = location;
                        pObj["required"] = location == "path" ||
                                           (!p.HasDefaultValue &&
                                            Nullable.GetUnderlyingType(p.ParameterType) == null);

                        var desc = p.GetCustomAttribute<ParamAttribute>()?.Description;
                        AddIfNotNull(pObj, "description", desc);

                        pObj["schema"] = EnsureSchema(schemas, p.ParameterType, inlinePrimitive: true);
                        parameters.Add(pObj);
                    }

                    if (parameters.Count > 0) op["parameters"] = parameters;
                    if (requestBody != null) op["requestBody"] = requestBody;

                    var responses = new Dictionary<string, object?>(StringComparer.Ordinal);
                    var ra = m.GetCustomAttributes<ResponseAttribute>().ToArray();
                    if (ra.Length > 0)
                    {
                        foreach (var r in ra)
                        {
                            var resp = new Dictionary<string, object?>
                            {
                                ["description"] = r.Description ?? $"HTTP {r.StatusCode}"
                            };
                            if (r.BodyType != null)
                            {
                                resp["content"] = new Dictionary<string, object?>
                                {
                                    ["application/json"] = new Dictionary<string, object?>
                                    {
                                        ["schema"] = EnsureSchema(schemas, r.BodyType)
                                    }
                                };
                            }

                            responses[r.StatusCode.ToString()] = resp;
                        }
                    }
                    else
                    {
                        if (m.ReturnType == typeof(void))
                        {
                            responses["204"] = new Dictionary<string, object?> { ["description"] = "No Content" };
                        }
                        else
                        {
                            var resp = new Dictionary<string, object?>
                            {
                                ["description"] = "OK",
                                ["content"] = new Dictionary<string, object?>
                                {
                                    ["application/json"] = new Dictionary<string, object?>
                                    {
                                        ["schema"] = EnsureSchema(schemas, m.ReturnType)
                                    }
                                }
                            };
                            responses["200"] = resp;
                        }
                    }

                    op["responses"] = responses;

                    pathItem[http] = op;
                }
            }

            var doc = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["openapi"] = "3.0.3",
                ["info"] = new Dictionary<string, object?> { ["title"] = title, ["version"] = version },
                ["paths"] = paths,
                ["components"] = new Dictionary<string, object?> { ["schemas"] = schemas }
            };

            return doc;
        }

        // 属性のHTTPメソッドを拾う（Path/Template/Route/Valueを柔軟に見る）
        static (string? http, string? path) GetHttpAttr(MethodInfo m)
        {
            foreach (var attr in m.GetCustomAttributes(inherit: true))
            {
                var t = attr.GetType();
                var name = t.Name;
                string? method = name switch
                {
                    var s when s.Equals("HttpGetAttribute", StringComparison.OrdinalIgnoreCase) => "get",
                    var s when s.Equals("HttpPostAttribute", StringComparison.OrdinalIgnoreCase) => "post",
                    var s when s.Equals("HttpPutAttribute", StringComparison.OrdinalIgnoreCase) => "put",
                    var s when s.Equals("HttpDeleteAttribute", StringComparison.OrdinalIgnoreCase) => "delete",
                    var s when s.Equals("HttpPatchAttribute", StringComparison.OrdinalIgnoreCase) => "patch",
                    _ => null
                };
                if (method == null) continue;

                string? path = GetStringProp(t, attr, "Path")
                               ?? GetStringProp(t, attr, "Template")
                               ?? GetStringProp(t, attr, "Route")
                               ?? GetStringProp(t, attr, "Value");

                if (string.IsNullOrWhiteSpace(path))
                {
                    var routeAttr = m.GetCustomAttributes(inherit: true)
                        .FirstOrDefault(a =>
                            a.GetType().Name.Equals("RouteAttribute", StringComparison.OrdinalIgnoreCase));
                    if (routeAttr != null)
                    {
                        var rt = routeAttr.GetType();
                        path = GetStringProp(rt, routeAttr, "Template")
                               ?? GetStringProp(rt, routeAttr, "Route")
                               ?? GetStringProp(rt, routeAttr, "Path")
                               ?? GetStringProp(rt, routeAttr, "Value");
                    }
                }

                return (method, path);
            }

            return (null, null);

            static string? GetStringProp(Type t, object instance, string propName)
                => t.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                    ?.GetValue(instance) as string;
        }

        // 二重スラッシュ防止
        static string NormalizePath(string prefix, string template)
        {
            var a = (prefix ?? string.Empty).Trim('/');
            var b = (template ?? string.Empty).Trim('/');
            if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b)) return "/";
            if (string.IsNullOrEmpty(a)) return "/" + b;
            if (string.IsNullOrEmpty(b)) return "/" + a;
            return "/" + a + "/" + b;
        }

        static object EnsureSchema(Dictionary<string, object?> repo, Type type, bool inlinePrimitive = false)
        {
            var underlying = Nullable.GetUnderlyingType(type);
            if (underlying != null) type = underlying;

            if (TryPrimitive(type, out var prim)) return prim;

            if (type.IsEnum)
            {
                return new Dictionary<string, object?>
                {
                    ["type"] = "string",
                    ["enum"] = Enum.GetNames(type)
                };
            }

            if (type.IsArray)
            {
                return new Dictionary<string, object?>
                {
                    ["type"] = "array",
                    ["items"] = EnsureSchema(repo, type.GetElementType()!)
                };
            }

            if (IsGeneric(type, typeof(List<>)) || IsGeneric(type, typeof(IEnumerable<>)))
            {
                var t = type.GetGenericArguments()[0];
                return new Dictionary<string, object?> { ["type"] = "array", ["items"] = EnsureSchema(repo, t) };
            }

            if (IsGeneric(type, typeof(Dictionary<,>)) && type.GetGenericArguments()[0] == typeof(string))
            {
                var t = type.GetGenericArguments()[1];
                return new Dictionary<string, object?>
                {
                    ["type"] = "object",
                    ["additionalProperties"] = EnsureSchema(repo, t)
                };
            }

            var name = ToSchemaName(type);
            if (repo.ContainsKey(name))
                return new Dictionary<string, object?> { ["$ref"] = $"#/components/schemas/{name}" };

            // placeholder to break cycles
            repo[name] = new Dictionary<string, object?> { ["type"] = "object" };

            var props = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var p in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (p.GetMethod == null) continue;
                var jsonName = p.GetCustomAttribute<JsonPropertyAttribute>()?.PropertyName
                               ?? char.ToLowerInvariant(p.Name[0]) + p.Name.Substring(1);
                props[jsonName] = EnsureSchema(repo, p.PropertyType);
            }

            if (props.Count > 0)
                repo[name] = new Dictionary<string, object?> { ["type"] = "object", ["properties"] = props };
            else
                repo[name] = new Dictionary<string, object?> { ["type"] = "object" };

            return new Dictionary<string, object?> { ["$ref"] = $"#/components/schemas/{name}" };

            static bool IsGeneric(Type t, Type def) => t.IsGenericType && t.GetGenericTypeDefinition() == def;
        }

        static bool TryPrimitive(Type t, out Dictionary<string, object?> schema)
        {
            if (t == typeof(string))
            {
                schema = new() { ["type"] = "string" };
                return true;
            }

            if (t == typeof(bool))
            {
                schema = new() { ["type"] = "boolean" };
                return true;
            }

            if (t == typeof(int) || t == typeof(short) || t == typeof(byte))
            {
                schema = new() { ["type"] = "integer", ["format"] = "int32" };
                return true;
            }

            if (t == typeof(long))
            {
                schema = new() { ["type"] = "integer", ["format"] = "int64" };
                return true;
            }

            if (t == typeof(float))
            {
                schema = new() { ["type"] = "number", ["format"] = "float" };
                return true;
            }

            if (t == typeof(double) || t == typeof(decimal))
            {
                schema = new() { ["type"] = "number", ["format"] = "double" };
                return true;
            }

            if (t == typeof(Guid))
            {
                schema = new() { ["type"] = "string", ["format"] = "uuid" };
                return true;
            }

            if (t == typeof(DateTime) || t == typeof(DateTimeOffset))
            {
                schema = new() { ["type"] = "string", ["format"] = "date-time" };
                return true;
            }

            schema = null!;
            return false;
        }

        static string ToSchemaName(Type t)
        {
            if (!t.IsGenericType) return t.Name;
            var gen = string.Join("And", t.GetGenericArguments().Select(ToSchemaName));
            var bare = t.Name[..t.Name.IndexOf('`')];
            return $"{bare}Of{gen}";
        }

        static void AddIfNotNull(Dictionary<string, object?> d, string k, object? v)
        {
            if (v is string s && string.IsNullOrWhiteSpace(s)) return;
            if (v != null) d[k] = v;
        }
    }
}
