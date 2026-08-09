using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI.Chat;
using System.ComponentModel;
using System.Text.Json;

public static class FinanceTools
{
    [Description("Issue a financial refund to a customer. Use this ONLY when the user explicitly requests a refund and provides an Order ID.")]
    public static string IssueRefund(
      [Description("The Order ID to refund (e.g., ORD-12345).")] string orderId,
      [Description("The decimal amount to refund.")] decimal amount
    )
    {
        // Simulating a deterministic call to a payment gateway (e.g., Stripe or PayPal)
        Console.WriteLine($"\n[SYSTEM LOG] Executing secure transaction: Refunded ${amount} to {orderId}.\n");
        return $"SUCCESS: ${amount} has been refunded to order {orderId}.";
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

        // Wrap the Tool with Approval Requirements
        AIFunction rawRefundedFuncrion = AIFunctionFactory.Create(FinanceTools.IssueRefund);

        // This is a Human-in-the-loop pattern so it pauses for human operator approval. 
        AIFunction secureRefundTool = new ApprovalRequiredAIFunction(rawRefundedFuncrion);

        // Instantiate the Agent and Equip the Tool
        AIAgent agent = new AzureOpenAIClient(new Uri(endpoint), new AzureCliCredential())
           .GetChatClient(deploymentModelName)
           .AsAIAgent(
                name: "FinanceSupport",
                instructions: "You are a customer support agent with billing privileges. You must help users process refund.",
                tools: [secureRefundTool]
            );

        // Must use session/thread so the agent remembers the context after the human pause it.
        AgentSession session = await agent.CreateSessionAsync();
        Console.WriteLine($"Agent '{agent.Name}' initialized. Ready for secure requests.\n");

        string userPrompt = "I was charge twice for order ORD-99999. Please issue a refund for $45.50.";
        Console.WriteLine($"User: {userPrompt}");

        // Execute the Agent (First Pass)
        AgentResponse response = await agent.RunAsync(userPrompt, session);

        // Check if the Agent pause to request human approval

        var approvalRequests = response.Messages
            .SelectMany(x => x.Contents)
            .OfType<ToolApprovalRequestContent>()
            .ToList();
        if (approvalRequests.Any())
        {
            ToolApprovalRequestContent request = approvalRequests.First();

            var requestToolCall = (FunctionCallContent)request.ToolCall;
            string toolName = requestToolCall.Name;
            string toolArguments = JsonSerializer.Serialize(requestToolCall.Arguments);

            // Display the AI's intent to the human mannager
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"\n[SECURITY ALERT] Agent requests permission to execute '{toolName}'");

            Console.WriteLine($"Proposed Arguments: {toolArguments}");
            Console.WriteLine("Do you approve this action? [Y/N]: ");
            Console.ResetColor();

            string? input = Console.ReadLine();
            bool isApproved = input?.Trim().ToUpper() == "Y";

            // Send the human's decision back to the Agent to resume execution
            var approvalMessage = new Microsoft.Extensions.AI.ChatMessage(
                ChatRole.User,
                [request.CreateResponse(isApproved) ]
            );

            response = await agent.RunAsync(approvalMessage, session);

            // Print the final synthesis
            Console.WriteLine($"\nAgent: {response.Text}");
        }
    }
}