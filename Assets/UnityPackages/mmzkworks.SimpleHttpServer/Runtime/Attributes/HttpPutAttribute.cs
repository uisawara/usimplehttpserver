#nullable enable
using System;

namespace UnityPackages.mmzkworks.SimpleHttpServer.Runtime
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class HttpPutAttribute : Attribute
    {
        public HttpPutAttribute(string template)
        {
            Template = template;
        }

        public string Template { get; }
    }
}