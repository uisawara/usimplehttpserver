#nullable enable
namespace mmzkworks.SimpleHttpServer
{
    public sealed class ContentResult
    {
        public int StatusCode { get; }
        public string ContentType { get; }
        public string Content { get; }

        public ContentResult(string content, string contentType, int statusCode = 200)
        {
            Content = content;
            ContentType = contentType;
            StatusCode = statusCode;
        }
    }
}


