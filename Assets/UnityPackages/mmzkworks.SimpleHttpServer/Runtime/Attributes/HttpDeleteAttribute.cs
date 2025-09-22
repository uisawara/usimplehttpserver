#nullable enable
using System;

namespace mmzkworks.SimpleHttpServer
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class HttpDeleteAttribute : Attribute
    {
        public HttpDeleteAttribute(string template)
        {
            Template = template;
        }

        public string Template { get; }
    }
}