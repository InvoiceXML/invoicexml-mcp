using System.Net;
using System.Net.Http;
using InvoiceXml.Mcp.Host.Auth.OAuth;
using Microsoft.AspNetCore.Http;

namespace InvoiceXml.Mcp.Host.Tests.OAuth;

public class OAuthForwardingHandlerTests
{
    [Fact]
    public async Task ForwardsInboundBearerHeaderToOutboundRequest()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers.Authorization = "Bearer user-secret-abc";

        var inner = new CapturingInnerHandler();
        var handler = new OAuthForwardingHandler(new StubHttpContextAccessor(ctx)) { InnerHandler = inner };
        var invoker = new HttpMessageInvoker(handler);

        using var outbound = new HttpRequestMessage(HttpMethod.Post, "https://api.invoicexml.test/v1/create/ubl");
        using var response = await invoker.SendAsync(outbound, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Bearer user-secret-abc", inner.CapturedAuthHeader);
    }

    [Fact]
    public async Task ThrowsWhenNoInboundBearer()
    {
        var ctx = new DefaultHttpContext(); // no Authorization header

        var inner = new CapturingInnerHandler();
        var handler = new OAuthForwardingHandler(new StubHttpContextAccessor(ctx)) { InnerHandler = inner };
        var invoker = new HttpMessageInvoker(handler);

        using var outbound = new HttpRequestMessage(HttpMethod.Get, "https://api.invoicexml.test/anything");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            invoker.SendAsync(outbound, CancellationToken.None));
    }

    private sealed class StubHttpContextAccessor(HttpContext ctx) : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get => ctx; set => throw new NotSupportedException(); }
    }

    private sealed class CapturingInnerHandler : HttpMessageHandler
    {
        public string? CapturedAuthHeader { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // OAuthForwardingHandler uses TryAddWithoutValidation, which keeps the
            // raw header string but bypasses Headers.Authorization parsing. Read
            // the raw header values to assert the verbatim forwarding.
            CapturedAuthHeader = request.Headers.TryGetValues("Authorization", out var values)
                ? string.Join(',', values)
                : null;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
