using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI.Chat;
using System.Text.Json.Serialization;

// Define the variables we extracted from Microsoft Foundry 
var deploymentModelName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") ?? "gpt-5-mini";

// Admin > All Projects > Name (or Parent resource) > Endpoint (Base URL)
var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
                ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set.");

// Initialize the Agent from AzureOpenAIClient via IChatClient
AIAgent agent = new AzureOpenAIClient(new Uri(endpoint), new AzureCliCredential())
   .GetChatClient(deploymentModelName)
   .AsAIAgent(
     name: "MeetingAnalyst",
     instructions: "You are an AI analyst. Extract the topic, action item, and overall sentiment from the provided transcript."
    );

// Execute the Agent with RunAsync<T> for strongly-typed structured output
string transcript = """
                     We discussed the Q4 marketing push. Sarah need to finalize the budget by Tuesday. 
                     John will contact the ad agency. Overall, everyone felt very optimistic about the campaign.
                    """;

Console.WriteLine($"Analyzing Transcript: {transcript}\n");

AgentResponse<MeetingAnalysis> response = await agent.RunAsync<MeetingAnalysis>(transcript);

// Access the strongly-typed Result direct (no manual deserialization needed)
MeetingAnalysis meetingAnalysis = response.Result;

// Utilize deterministic C# objects
if (meetingAnalysis is not null)
{
    Console.WriteLine($"Full Analysis: {meetingAnalysis}\n");
    Console.WriteLine($"Topic: {meetingAnalysis.Topic}\n");
    Console.WriteLine($"Sentiment: {meetingAnalysis.Sentiment}\n");
    Console.WriteLine($"Action Item Count: {meetingAnalysis.ActionItems.Length}\n");
    Console.WriteLine($"Action Item:\n{string.Join("", meetingAnalysis?.ActionItems?.Select(x => $"- {x}\n"))}");
}

// Data Contract
public record MeetingAnalysis(
 [property: JsonPropertyName("topic")] string Topic,    
 [property: JsonPropertyName("actionItems")] string[] ActionItems,    
 [property: JsonPropertyName("sentiment")] string Sentiment
);
