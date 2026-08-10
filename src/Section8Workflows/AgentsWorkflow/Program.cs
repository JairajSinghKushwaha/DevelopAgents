using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

public record TicketState(string UserQuery, string Category = "Unassigned", string FinalResolution = "");

public class Program
{
    public static async Task Main()
    {
        // Define the variables we extracted from Microsoft Foundry 
        var deploymentModelName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") ?? "gpt-5-mini";

        var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
                        ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set.");

        IChatClient chatClient = new AzureOpenAIClient(new Uri(endpoint), new AzureCliCredential())
            .GetChatClient(deploymentModelName)
            .AsIChatClient();

        AIAgent triageAgent = chatClient.AsAIAgent(
            name: "Triage",
            instructions: "Analyze the user's IT request. Categorize it strictely as ether 'Hardware' or 'Software'. Output only the category word."
            );

        AIAgent hardwareAgent = chatClient.AsAIAgent(
            name: "Hardware",
            instructions: "You are an enterprise hardware specilist. Provide concise troubleshooting step for physical device issues."
            );

        AIAgent softwareAgent = chatClient.AsAIAgent(
            name: "Software",
            instructions: "You are an enterprise software specilist. Provide concise troubleshooting step for application, OS, and network issues."
            );

        // Triage Node Exection Logic
        Func<TicketState, TicketState> triageFunc = state =>
        {
            Console.WriteLine($"[Triage] Analyzing ticket: {state.UserQuery}");
            AgentResponse response = triageAgent.RunAsync(state.UserQuery).GetAwaiter().GetResult();

            string category = response.Text.Trim();
            Console.WriteLine($"[Triage] Decision: Routed to {category} Department.");

            // Return a mutated copy of the state with the new category
            return state with { Category = category };
        };

        var triageNode = triageFunc.BindAsExecutor("TriageNode");

        // Hardware Node Exection Logic
        Func<TicketState, TicketState> hardwareFunc = state =>
        {
            Console.WriteLine($"[Hardware Support]: Generating resolution...");
            AgentResponse response = hardwareAgent.RunAsync(state.UserQuery).GetAwaiter().GetResult();

            return state with { FinalResolution = response.Text };
        };

        var hardwareNode = hardwareFunc.BindAsExecutor("HardwareNode");

        // Hardware Node Exection Logic
        Func<TicketState, TicketState> softwareFunc = state =>
        {
            Console.WriteLine($"[Software Support]: Generating resolution...");
            AgentResponse response = softwareAgent.RunAsync(state.UserQuery).GetAwaiter().GetResult();

            return state with { FinalResolution = response.Text };
        };

        var softwareNode = softwareFunc.BindAsExecutor("SoftwareNode");

        // Build the Graph with Conditional Edges
        var workflow = new WorkflowBuilder(triageNode)
            .AddEdge<TicketState>(triageNode, hardwareNode,
                condition: state => state != null &&
                state.Category.Contains("Hardware", StringComparison.OrdinalIgnoreCase))
            .AddEdge<TicketState>(triageNode, softwareNode,
                condition: state => state != null &&
                state.Category.Contains("Software", StringComparison.OrdinalIgnoreCase))
            .Build();

        Console.WriteLine("--- Incomming Enterprises IT Ticket ---\n");

        var initialTicket = new TicketState("My laptop screen is flickering agressive and the hinge feels loose");

        // Execute the Workflow Graph
        await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, initialTicket);

        TicketState? finalState = default;

        // Observer the event as the payload
        await foreach (WorkflowEvent @event in run.WatchStreamAsync())
        {
            if (@event is ExecutorCompletedEvent executorCompletedEvent)
            {
                Console.WriteLine($"[System] => Node '{executorCompletedEvent.ExecutorId}' complete.");

                if (executorCompletedEvent.Data is TicketState ticket)
                {
                    finalState = ticket;
                    Console.WriteLine($"""
                        State: Category='{ticket.Category}', 
                        Resolution='{(string.IsNullOrWhiteSpace(finalState.FinalResolution) ? "Pending" : "Set")}'
                        """);
                }
            }
        }
        Console.WriteLine($"Final Resolution: {finalState?.FinalResolution}");
    }
}