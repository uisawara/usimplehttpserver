using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;

namespace mmzkworks.SimpleHttpServer
{
    public sealed class Route
    {
        public string Method;
        public MethodInfo MethodInfo;
        public ParameterInfo[] Parameters;
        public string[] ParamNames;
        public Regex Regex;
        public int SegmentCount;
        public object Target;

        private Route(string method, Regex regex, string[] names, object target, MethodInfo mi, ParameterInfo[] pars,
            int segs)
        {
            Method = method;
            Regex = regex;
            ParamNames = names;
            Target = target;
            MethodInfo = mi;
            Parameters = pars;
            SegmentCount = segs;
        }

        public static Route Create(string method, string prefix, string template, object target, MethodInfo mi)
        {
            var full = Normalize(prefix) + Normalize(template);
            var (regex, names, seg) = BuildRegex(full);
            return new Route(method, regex, names, target, mi, mi.GetParameters(), seg);
        }

        private static string Normalize(string p)
        {
            if (string.IsNullOrWhiteSpace(p)) return "";
            if (!p.StartsWith("/")) p = "/" + p;
            return p.TrimEnd('/');
        }

        private static (Regex regex, string[] names, int segments) BuildRegex(string template)
        {
            var names = new List<string>();
            var pattern = Regex.Replace(template, @"\{([a-zA-Z_][a-zA-Z0-9_]*)\}", m =>
            {
                names.Add(m.Groups[1].Value);
                return "([^/]+)";
            });
            if (string.IsNullOrEmpty(pattern)) pattern = "/";
            var rx = new Regex("^" + pattern + "$",
                RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            var seg = pattern.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries).Length;
            return (rx, names.ToArray(), seg);
        }
    }
}