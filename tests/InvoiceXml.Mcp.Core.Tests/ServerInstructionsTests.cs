using InvoiceXml.Mcp.Core.Extensions;

namespace InvoiceXml.Mcp.Core.Tests;

public class ServerInstructionsTests
{
    /// <summary>
    /// ChatGPT only delivers a returned PDF once the user approves file attachments for the
    /// session. The per-call approval loses the turn although the document was created, so the
    /// server tells the agent which option to point the user to, and not to repeat the call unasked.
    /// </summary>
    [Fact]
    public void TellsTheAgentHowChatGptUsersReceivePdfs()
    {
        var instructions = McpServerBuilderExtensions.ServerInstructions;

        Assert.Contains("PDF results in ChatGPT", instructions);
        Assert.Contains("'Allow for this session'", instructions);
        Assert.Contains("ask before creating it again", instructions);
    }
}
