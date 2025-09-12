#nullable enable
using System;

namespace UnityPackages.mmzkworks.SimpleHttpServer.Runtime
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class RoutePrefixAttribute : Attribute
    {
        public RoutePrefixAttribute(string prefix)
        {
            Prefix = Normalize(prefix);
        }

        public string Prefix { get; }

        private static string Normalize(string s)
        {
            return string.IsNullOrWhiteSpace(s) ? "" : s.StartsWith('/') ? s : "/" + s.Trim();
        }
    }
}