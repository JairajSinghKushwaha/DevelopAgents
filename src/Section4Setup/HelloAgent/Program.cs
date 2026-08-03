using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using OpenAI.Chat;
using System.ClientModel;

// Define the variables we extracted from Microsoft Foundry 
var deploymentModelName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") ?? "gpt-5-mini";

// Admin > All Projects > Name (or Parent resource) > Endpoint (Base URL)
var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT") 
                ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set.");

//var apikey = Environment.GetEnvironmentVariable("AZURE_OPENAI_APIKEY")
//    ?? throw new InvalidOperationException("AZURE_OPENAI_APIKEY is not set.");

// Create the Agent using MFA
AIAgent agent = new AzureOpenAIClient(
   new Uri(endpoint), new AzureCliCredential())
   .GetChatClient(deploymentModelName)
   .AsAIAgent(instructions: "You are a friendly assistant. Keep your answers brief.");

// Use for CI/CD scenarios, Get it from: Admin > All Projects > Parent resource > Api Key
//AIAgent agent = new AzureOpenAIClient(
//    new Uri(endpoint), new ApiKeyCredential(apikey))
//    .GetChatClient(deploymentModelName)
//    .AsAIAgent(instructions: "You are a friendly assistant. Keep your answers brief.");

// Invoke the Agent 
Console.WriteLine(await agent.RunAsync("What are the most famous cities in India?"));