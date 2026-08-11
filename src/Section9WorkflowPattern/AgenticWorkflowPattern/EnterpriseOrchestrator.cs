using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using System.Text.Json;

namespace AgenticWorkflowPattern;

public class EnterpriseOrchestrator
{
    // The universal execution loop for any Agentic Workflow
    public static async Task<IReadOnlyList<ChatMessage>> RunWorkflowAsync(Workflow workflow, IList<ChatMessage> chatMessage)
    {
        string? lastExecutorId = default;

        // Push the conversational state into the workflow
        await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, chatMessage);

        // Instruct the engin to emit events for the active turn
        await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

        await foreach (WorkflowEvent @event in run.WatchStreamAsync())
        {
            // Stream token generation in real-time
            if (@event is AgentResponseUpdateEvent responseUpdateEvent)
            {
                if (responseUpdateEvent.ExecutorId != lastExecutorId)
                {
                    lastExecutorId = responseUpdateEvent.ExecutorId;
                    Console.WriteLine($"\n\n--- [Active Node: {responseUpdateEvent.ExecutorId}] ---");
                }

                Console.Write(responseUpdateEvent.Update.Text);
                
                // Log autonomous tool executions
                if (responseUpdateEvent.Update.Contents.OfType<FunctionCallContent>().FirstOrDefault() is FunctionCallContent call)
                {
                    Console.WriteLine($"\n [System] -> Executing Tool '{call.Name}' with payload: {JsonSerializer.Serialize(call.Arguments)}");
                }
            }
            // Capture the final payload when the graph terminates
            else if (@event is WorkflowOutputEvent outputEvent)
            {
                Console.WriteLine("\n\n--- Workflow Terminated ---");
                return outputEvent.As<List<ChatMessage>>()!;
            }
        }

        return [];
    }
}
