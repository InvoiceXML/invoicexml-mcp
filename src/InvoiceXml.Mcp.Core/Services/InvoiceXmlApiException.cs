using System.Net;
using System.Text.Json;
using InvoiceXml.Mcp.Core.Models;

namespace InvoiceXml.Mcp.Core.Services;

/// <summary>
/// Thrown when the InvoiceXML API returns a non-success status code. Carries
/// the raw response body so tool layers can surface a useful message to the LLM
/// without losing detail; <see cref="TryParseProblem"/> turns the body into the
/// API's structured error shape when it follows RFC 7807.
/// </summary>
public sealed class InvoiceXmlApiException : Exception
{
    public InvoiceXmlApiException(HttpStatusCode statusCode, string responseBody)
        : base(BuildMessage(statusCode, responseBody))
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    /// <summary>HTTP status code returned by the API.</summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>Raw response body, typically a <c>ProblemDetails</c> JSON document.</summary>
    public string ResponseBody { get; }

    /// <summary>
    /// Attempts to deserialise <see cref="ResponseBody"/> into the API's
    /// structured error shape. Returns <see langword="null"/> when the body
    /// is empty or not valid JSON in that shape (e.g. an HTML 502 page from
    /// an intermediate proxy); callers should treat that as "no parsed
    /// details available" rather than an error.
    /// </summary>
    public InvoiceXmlApiProblem? TryParseProblem()
    {
        if (string.IsNullOrWhiteSpace(ResponseBody))
            return null;

        try
        {
            return JsonSerializer.Deserialize<InvoiceXmlApiProblem>(
                ResponseBody, InvoiceXmlJsonOptions.Default);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string BuildMessage(HttpStatusCode statusCode, string body)
    {
        var snippet = body.Length > 400 ? body[..400] + "…" : body;
        return $"InvoiceXML API returned {(int)statusCode} {statusCode}. Body: {snippet}";
    }
}
