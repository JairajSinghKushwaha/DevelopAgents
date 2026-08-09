using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.DevUI;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.AddServiceDefaults();

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
   .AsIChatClient()
   .AsBuilder()
   .UseOpenTelemetry(configure: c => c.EnableSensitiveData = true)
   .Build();

builder.Services.AddSingleton(chatClient);

// Define and Register the Agents
builder.AddAIAgent(
    name: "NetworkSupportAgent",
    instructions:
        """
        You are a Tier 1 IT Support Agent.
        Your answers must be concise, professional, and limited strictly to troubleshooting network and VPN connectivity.        
        Always check their VPN status if they report a disconnection.
        Keep responses concise — 3-5 sentences per turn. Be direct and opinionated.        
        """, 
     chatClient)
    .WithAITool(AIFunctionFactory.Create(NetworkTools.CheckVpnStatus));

// Register DevUI services
builder.AddDevUI();
builder.Services.AddOpenAIResponses();
builder.Services.AddOpenAIConversations(); 

var app = builder.Build();

// Configure the HTTP request pipeline
app.MapDefaultEndpoints();

// Map DevUI endpoints
app.MapDevUI();
app.MapOpenAIResponses();
app.MapOpenAIConversations();

// Map chat endpoint to trigget the agent
app.MapGet("/api/chat", async ([FromServices] ChatRequest request,
    [FromKeyedServices("NetworkSupportAgent")] AIAgent networkSupportAgent) =>
{
    var response = await networkSupportAgent.RunAsync(request.Message);
    return Results.Ok(new { respose = response.Text });
});

app.Run();

record ChatRequest(string Message);

// Define the Enterprise Tool
public static class NetworkTools
{
    [Description("Checks the current status of the corporate VPN for a specific user.")]
    public static string CheckVpnStatus([Description("The username of the employee, e.g., jsmith")] string userName)
    {
        // Simulating a deterministic API call to a firewall or directory service.
        return $"User {userName} is currently DISCONNECTED. Error: IPsec tunnel timeout.";
    }
}