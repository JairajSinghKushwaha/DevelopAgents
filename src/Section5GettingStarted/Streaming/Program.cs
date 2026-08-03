using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

// Define the variables we extracted from Microsoft Foundry 
var deploymentModelName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") ?? "gpt-5-mini";

// Admin > All Projects > Name (or Parent resource) > Endpoint (Base URL)
var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
                ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set.");

// Instantiate the universal chat client
IChatClient chatClient = new AzureOpenAIClient(
   new Uri(endpoint),
   new AzureCliCredential())
   .GetChatClient(deploymentModelName)
   .AsIChatClient();

// Define the Agent's Anatomy
AIAgent agent = chatClient.AsAIAgent(
    name: "NetworkSupport",
    instructions: @"You are a Tier 1 IT Support Agent. Your answers must be a concise, professional, and 
                    limited strictly to trobleshoting network and VPN connectivity. Keep your answer brief."
    );

Console.WriteLine($"Agent '{agent.Name}' is online...\n");

// Execute the Agent
string userIssue = "I'm getting a DNS resolution error when connecting to the corporate VPN from a coffee shop. Keep your answer brief.";
Console.WriteLine($"User: {userIssue}\n");


Console.Write($"Agent: ");

// IAsyncEnumerable used in RunStreamingAsync in a non-blocking way to process the items
// in chunkes instead of waiting for complete response at once.
await foreach (var resp in agent.RunStreamingAsync(userIssue))
{
    Console.Write(resp);
}