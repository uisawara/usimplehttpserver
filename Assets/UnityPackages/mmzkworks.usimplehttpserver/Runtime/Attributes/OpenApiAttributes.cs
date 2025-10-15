#nullable enable
using System;

namespace mmzkworks.SimpleHttpServer.OpenApi
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class SummaryAttribute : Attribute
    {
        public SummaryAttribute(string text)
        {
            Text = text;
        }

        public string Text { get; }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public sealed class TagAttribute : Attribute
    {
        public TagAttribute(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }

    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class ParamAttribute : Attribute
    {
        public ParamAttribute(string? description = null)
        {
            Description = description;
        }

        public string? Description { get; }
        public bool Required { get; set; }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class ResponseAttribute : Attribute
    {
        public ResponseAttribute(int statusCode, Type? bodyType = null, string? description = null)
        {
            StatusCode = statusCode;
            BodyType = bodyType;
            Description = description;
        }

        public int StatusCode { get; }
        public Type? BodyType { get; }
        public string? Description { get; }
    }
}