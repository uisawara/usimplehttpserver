#nullable enable
using System;

namespace mmzkworks.SimpleHttpServer
{
    public interface IJsonSerializer
    {
        string Serialize(object? value);
        object? Deserialize(string json, Type type);
    }
}