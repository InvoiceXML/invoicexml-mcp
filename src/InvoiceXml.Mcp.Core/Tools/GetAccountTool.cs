using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using InvoiceXml.Mcp.Core.Interfaces;
using InvoiceXml.Mcp.Core.Services;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace InvoiceXml.Mcp.Core.Tools;

/// <summary>
/// MCP tool that wraps <c>GET /v1/me</c>: the caller's plan and credit
/// balance. Free to call (the API does not charge a credit), so an agent can
/// check the balance before a batch of billable calls.
/// </summary>
[McpServerToolType]
public sealed class GetAccountTool
{
    private readonly IInvoiceXmlClient _client;

    public GetAccountTool(IInvoiceXmlClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    [McpServerTool(Name = "get_account", Title = "Check Account and Credits", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description(
        "Show the InvoiceXML account's plan and credit balance: credits remaining, granted and used in the current " +
        "period, and used in total. This call is free, it uses no credit. Use it when the user asks about their " +
        "balance or plan, before processing a large batch of invoices, or after a tool failed because credits ran " +
        "out. On failure the result has isError=true and a JSON body { success:false, failureCategory, guidance }.")]
    public async Task<CallToolResult> GetAccountAsync(CancellationToken cancellationToken)
    {
        ToolFailure.Translation failure;
        try
        {
            var account = await _client.GetAccountAsync(cancellationToken).ConfigureAwait(false);

            // Invariant so the agent sees the same digits whatever the host's locale.
            var remaining = account.CreditsRemaining is { } left
                ? string.Create(CultureInfo.InvariantCulture, $"{left:N0}")
                : "unknown";
            var ofTotal = account.CreditsTotal is { } total
                ? string.Create(CultureInfo.InvariantCulture, $" of {total:N0} this period")
                : string.Empty;
            var summary = $"Plan: {account.Plan ?? "unknown"}. Credits remaining: {remaining}{ofTotal}.";

            return new CallToolResult
            {
                Content =
                [
                    new TextContentBlock { Text = summary },
                    new TextContentBlock { Text = JsonSerializer.Serialize(account, InvoiceXmlJsonOptions.Default) },
                ],
            };
        }
        catch (InvoiceXmlApiException ex)
        {
            failure = ToolFailure.FromApiException(ex);
        }
        catch (HttpRequestException ex)
        {
            failure = ToolFailure.FromNetworkException(ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            failure = ToolFailure.FromNetworkException(ex);
        }

        return ToolResults.ForFailure(new ToolFailurePayload
        {
            Success = false,
            Summary = failure.Summary,
            FailureCategory = failure.Category,
            StatusCode = failure.StatusCode,
            Errors = failure.Errors,
            Guidance = failure.Guidance,
        });
    }
}
