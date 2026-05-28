using System.Net;

namespace InvoiceXml.Mcp.Core.Tests.TestSupport;

/// <summary>
/// Captures the last request the typed client sent and returns a canned response.
/// Used by tool tests to verify the client builds the right HTTP message without
/// hitting the real API.
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

    public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        _responder = responder;
    }

    public StubHttpMessageHandler(HttpResponseMessage response)
        : this(_ => response)
    {
    }

    public HttpRequestMessage? LastRequest { get; private set; }
    public string? LastRequestBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        LastRequest = request;
        if (request.Content is not null)
        {
            LastRequestBody = await request.Content
                .ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        return _responder(request);
    }

    public static HttpResponseMessage Json(HttpStatusCode statusCode, string json) =>
        new(statusCode)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        };

    public static HttpResponseMessage Binary(
        HttpStatusCode statusCode,
        byte[] payload,
        string contentType,
        string? fileName = null)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new ByteArrayContent(payload),
        };
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        if (fileName is not null)
        {
            response.Content.Headers.ContentDisposition =
                new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment") { FileName = fileName };
        }

        return response;
    }
}
