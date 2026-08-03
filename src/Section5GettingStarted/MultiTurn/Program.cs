using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI.Chat;

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
    name: "HistoryBuff",
    instructions: @"You are a helpful history teacher. You answer questions and help students make connections between events."
    );

// Create the Session (The Memory Container)
// This object will accumulate the conversation history.
AgentSession session = await agent.CreateSessionAsync();

Console.WriteLine("History Teacher is online. Type 'exit' to quit.\n");

// Conversation Loop
while (true)
{
    Console.Write("User:");
    string? input = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(input) || input.ToLower() == "exit") 
        break;

    // We pass the 'session' into RunAsync.
    // The framework automatically appends the user's input to this session,
    // sends the full history to the cloud, and append the agent's response back to the session.
    AgentResponse response = await agent.RunAsync(input, session);

    Console.WriteLine($"Agent: {response.Text}\n");
}