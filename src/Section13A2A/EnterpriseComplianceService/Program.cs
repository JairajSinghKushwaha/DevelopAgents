using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddOpenApi();

// Define the variables we extracted from Microsoft Foundry 
var deploymentModelName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") ?? "gpt-5-mini";

var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
                ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set.");

// Instantiate the universal chat client with OpenTelemetry GenAI instrumentation
IChatClient chatClient = new AzureOpenAIClient(new Uri(endpoint), new AzureCliCredential())
    .GetChatClient(deploymentModelName)
    .AsIChatClient()
    .AsBuilder()
    .UseOpenTelemetry(configure: c => c.EnableSensitiveData = true)
    .Build();

builder.Services.AddSingleton(chatClient);

// Register the Specialized Enterprize Agent
var agent = builder.AddAIAgent(
    name: "Compliance",
    instructions: """
        You are a strict enterprise compliance auditor. Review the provided text for GDPR violations.
        Be concise and authoritative.
    """
    );

// Register A2A server for the agent (required by latest A2A endpoint mapping APIs)
agent.AddA2AServer();

var app = builder.Build();

// Configure the HTTP request pipeline.

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Expose the agent via the A2A HTTP+JSON protocol
app.MapA2AHttpJson(agent, path:"/a2a/Compliance");

app.Run();