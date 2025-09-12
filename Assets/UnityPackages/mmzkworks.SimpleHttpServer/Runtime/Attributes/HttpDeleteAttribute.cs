#nullable enable
using System;

namespace UnityPackages.mmzkworks.SimpleHttpServer.Runtime
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