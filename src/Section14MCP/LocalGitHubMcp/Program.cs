using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using OpenAI.Chat;

Console.WriteLine("--- Initializing Local DevOps Swarm ---");

// Establish the Local MCP Connection
// The framework will execute 'npx' to launch the GitHub MCP server locally.
// NOTE: This requires Node.js to be installed on the host machine and a GITHUB_PERSONAL_ACCESS_TOKEN environment variable.
await using var mcpClient = await McpClient.CreateAsync(new StdioClientTransport(new()
{
    Name = "MCPServer",
    Command = "npx",
    Arguments = ["-y", "--verbose", "@modelcontextprotocol/server-github"]
}));

// Capability Discovery
// Ask the MCP server what tools it exposes (e.g., search_repositories, get_commit, list_issues)
var mcpTools = await mcpClient.ListToolsAsync().ConfigureAwait(false);
Console.WriteLine($"[System] Discovered {mcpTools.Count} tool from the local GitHub MCP server.");

// Initialize the Enterprise Agent
// We cast the discovered MCP tools into native AITool definitions and inject them into the agent.
var deploymentModelName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") ?? "gpt-5-mini";

var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
                ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set.");

// Initialize the universal chat client with OpenTelemetry GenAI instrumentation
AIAgent releaseAgent = new AzureOpenAIClient(new Uri(endpoint), new AzureCliCredential())
    .GetChatClient(deploymentModelName)
    .AsAIAgent(
         name: "ReleaseManager",
         instructions: """
          You are a DevOps Release Manager. You must only answer questions related to GitHub repositories. 
          Use your tools to fetch commit history and summarize it into professional release notes.
         """,
         tools: [.. mcpTools.Cast<AITool>()]
    );

string prompt = "Fetch the last 3 commits from the microsoft/agent-framework repository and summarize them for our V1.5 release notes.";
Console.WriteLine($"User: {prompt}\n");
Console.WriteLine("[System] Agent is autonomously querying GitHub...");

AgentResponse response = await releaseAgent.RunAsync(prompt);
Console.WriteLine($"\nRelease Manager:\n{response.Text}");
