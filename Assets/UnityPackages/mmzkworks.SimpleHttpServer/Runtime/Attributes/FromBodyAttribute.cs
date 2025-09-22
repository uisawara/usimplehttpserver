#nullable enable
using System;

namespace mmzkworks.SimpleHttpServer
{
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class FromBodyAttribute : Attribute
    {
    }
}