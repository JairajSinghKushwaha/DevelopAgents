using Microsoft.Agents.AI.Workflows;

// Event Payload
public record CustomerPayload(string CompanyName, string Industry, bool IsValidated = false, string Status = "New");

public class Program
{
    public static async Task Main()
    {
        // The validator Node
        Func<CustomerPayload, CustomerPayload> validateFunc = payload =>
        {
            Console.WriteLine($"[Validator] Inspecting payload for: {payload.CompanyName}");
            bool isValid = !string.IsNullOrWhiteSpace(payload.CompanyName);

            return payload with { IsValidated = isValid, Status = isValid ? "Validated" : "Rejected" };
        };

        var validateExecutor = validateFunc.BindAsExecutor("ValidatingNode");

        // The Enrich Node
        Func<CustomerPayload, CustomerPayload> enrichFunc = payload =>
        {
            Console.WriteLine($"[Enricher] Applying '{payload.Industry}' enterprise templates...");
            return payload with { Status = "Enriched" };
        };

        var enricherExecutor = enrichFunc.BindAsExecutor("EnrichmentNode");

        // The Audit Node
        Func<CustomerPayload, CustomerPayload> auditFunc = payload =>
        {
            Console.WriteLine($"[Auditor] Logging final state to database. Final State: {payload.Status}");
            return payload;
        };

        var auditFuncExecutor = auditFunc.BindAsExecutor("AuditNode");

        // Construct the Workflow Graph
        var workflow = new WorkflowBuilder(validateExecutor)
            // Conditional Edge: Only enrich if valid
            .AddEdge<CustomerPayload>(validateExecutor, enricherExecutor, condition: p => p?.IsValidated == true)
            // Conditional Edge: If invalid, skip the audit
            .AddEdge<CustomerPayload>(validateExecutor, auditFuncExecutor, condition: p => p?.IsValidated == false)
            // Standard Edge: Enrichment always flows to Audit
            .AddEdge(enricherExecutor, auditFuncExecutor)
            .Build();

        Console.WriteLine("--- Starting Workflow Execusion ---\n");

        var initialPayload = new CustomerPayload("Contoso Pharmaceuticals","Healthcare");

        // Execute the Graph
        await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, initialPayload);

        // Listen to the stream to observve the nodes completing their work
        await foreach(WorkflowEvent @event in run.WatchStreamAsync())
        {
            if(@event is ExecutorCompletedEvent executorComplete)
            {
                Console.WriteLine($"[System] -> Node '{executorComplete.ExecutorId}' completed successfully.\n");
            }
        }
        Console.WriteLine("--- Workflow Complete ---");
    }
}