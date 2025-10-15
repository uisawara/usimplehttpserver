#nullable enable
using System;

namespace mmzkworks.SimpleHttpServer
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