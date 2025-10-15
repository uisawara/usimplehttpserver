#nullable enable
using System;

namespace mmzkworks.SimpleHttpServer
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class HttpGetAttribute : Attribute
    {
        public HttpGetAttribute(string template)
        {
            Template = template;
        }

        public string Template { get; }
    }
}