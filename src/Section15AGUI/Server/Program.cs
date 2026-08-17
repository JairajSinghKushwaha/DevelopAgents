using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using Microsoft.Extensions.AI;
using OpenAI.Chat;
using OpenAI.Responses;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Register AG-UI services
builder.Services.AddHttpClient().AddLogging();
builder.Services.AddAGUIServer();

// Initialize the LLM Chat Client and Define the Backend Agent
var deploymentModelName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") ?? "gpt-5-mini";

var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
                ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set.");

// Initialize the Agent
AIAgent enterpriseAgent = new AzureOpenAIClient(new Uri(endpoint), new AzureCliCredential())
    .GetChatClient(deploymentModelName)
    .AsAIAgent(
       name: "EnterpriseSupportAgent",
       instructions: "You are a helpful enterprise support agent."
    );

var app = builder.Build();

// Configure the HTTP request pipeline.

// Expose the Agent via the AG-UI Protocol
// This single extension method automatically wires up HTTP POST processing and SSE streaming
app.MapAGUIServer("/agui/support", enterpriseAgent);

// Using this configured url connect with the client app.
app.Run("http://localhost:5000"); 


