#nullable enable
using System;

namespace UnityPackages.mmzkworks.SimpleHttpServer.Runtime
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