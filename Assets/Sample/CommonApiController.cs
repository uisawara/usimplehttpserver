#nullable enable
using System;
using System.Linq;
using mmzkworks.SimpleHttpServer.OpenApi;

namespace UnityPackages.mmzkworks.SimpleHttpServer.Runtime
{
    [RoutePrefix("/api")]
    public sealed class CommonApiController
    {
        // GET /api/echo/hello?times=3 -> "hellohellohello"
        [HttpGet("/echo/{text}")]
        [Response(200, description: "request parameter")]
        public string Echo(string text, int times = 1)
        {
            return string.Concat(Enumerable.Repeat(text, Math.Max(1, times)));
        }

        // GET /api/add/12/30 -> { "a":12, "b":30, "sum":42 }
        [HttpGet("/add/{a}/{b}")]
        [Response(200, description: "jsonlized a + b")]
        public object Add(int a, int b)
        {
            return new { a, b, sum = a + b };
        }

        // POST /api/users  Body: {"name":"Alice","age":20}
        [HttpPost("/users")]
        [Response(200, typeof(User))]
        public User CreateUser([FromBody] User req)
        {
            return new User { Id = Guid.NewGuid(), Name = req.Name, Age = req.Age };
        }

        // GET /api/users/{id}
        [HttpGet("/users/{id}")]
        [Response(200, typeof(User))]
        public User GetUser(Guid id)
        {
            return new User { Id = id, Name = "Sample", Age = 42 };
        }
    }

    public sealed class User
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public int Age { get; set; }
    }
}