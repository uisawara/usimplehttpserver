#nullable enable
using System;

namespace UnityPackages.mmzkworks.SimpleHttpServer.Runtime
{
    public interface IJsonSerializer
    {
        string Serialize(object? value);
        object? Deserialize(string json, Type type);
    }
}