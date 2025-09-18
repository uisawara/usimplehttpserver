#nullable enable
using System;

namespace mmzkworks.SimpleHttpServer.OpenApi
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public sealed class SummaryAttribute : Attribute
    {
        public string Text { get; }
        public SummaryAttribute(string text) => Text = text;
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public sealed class TagAttribute : Attribute
    {
        public string Name { get; }
        public TagAttribute(string name) => Name = name;
    }

    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    public sealed class ParamAttribute : Attribute
    {
        public string? Description { get; }
        public bool Required { get; set; }
        public ParamAttribute(string? description = null) { Description = description; }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class ResponseAttribute : Attribute
    {
        public int StatusCode { get; }
        public Type? BodyType { get; }
        public string? Description { get; }
        public ResponseAttribute(int statusCode, Type? bodyType = null, string? description = null)
        { StatusCode = statusCode; BodyType = bodyType; Description = description; }
    }
}
