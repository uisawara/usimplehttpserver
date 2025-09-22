#nullable enable
using System;

namespace mmzkworks.SimpleHttpServer
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class HttpPostAttribute : Attribute
    {
        public HttpPostAttribute(string template)
        {
            Template = template;
        }

        public string Template { get; }
    }
}