using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI.Chat;
using System.ComponentModel;

// Define the Enterprise Tool
public static class LogisticsTools
{
    [Description("Retrieves the current shipping status of an enterprise logistics order. Invoke this tool ONLY when the user explicitly provides an Order ID.")]
    public static string GetOrderStatus(
        [Description("The exact, case-sensitive alphanumeric order identifer. Formate must be 'ORD-' followed by 5 digit (e.g., ORD-12345).")] string orderId)
    {
        // Simulating a deterministic database or external API call
        if (orderId == "ORD-12345") return "IN TRANSIT - Estimated Delivery Tommorrow";
        if (orderId == "ORD-99999") return "PENDING - Awaiting Stock Validation";
        return "UNKNOWN - Order ID not found in the logistics system.";
    }
}

public class Program
{
    public static async Task Main()
    {
        // Define the variables we extracted from Microsoft Foundry 
        var deploymentModelName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") ?? "gpt-5-mini";

        // Admin > All Projects > Name (or Parent resource) > Endpoint (Base URL)
        var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
                        ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set.");

        // Instantiate the Agent and Equip the Tool
        AIAgent agent = new AzureOpenAIClient(new Uri(endpoint), new AzureCliCredential())
           .GetChatClient(deploymentModelName)
           .AsAIAgent(
            name: "LogisticsSupport",
            instructions: "You are a custoner support agent. Help users track their orders concisely.",
            // We dynamically generate the AITool and pass it into the agent's capabilities.
            tools: [AIFunctionFactory.Create(LogisticsTools.GetOrderStatus)]
            );

        Console.WriteLine($"Agent '{agent.Name}' initialized. Ready to assist. \n");

        // --- Execution Pattern 1: Synchronous (Non-Streaming) ---
        Console.WriteLine("------------| Synchronous Exection |-------------");

        string prompt = "What is the status of order ORD-12345?";
        Console.WriteLine($"User: {prompt}");

        AgentResponse response = await agent.RunAsync(prompt);
        Console.WriteLine($"Agent: {response.Text}\n");

        // ---- Execution Pattern 2: Real-Time (Streaming) ----
        Console.WriteLine("---- Streaming Execution ----");
        string prompt2 = "I need an update on ORD-99999, please.";
        Console.WriteLine($"User: {prompt2}");
        Console.WriteLine("Agent: ");

        await foreach (AgentResponseUpdate update in agent.RunStreamingAsync(prompt2))
        {
            Console.Write(update.Text);
        }

        Console.WriteLine("\n");
    }
}
