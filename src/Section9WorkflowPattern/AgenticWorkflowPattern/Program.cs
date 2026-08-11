using AgenticWorkflowPattern;
using AgenticWorkflowPattern.Enum;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using System.Numerics;
using System.Xml.Linq;

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

        Console.WriteLine("Select Enterprise Topology:");
        Console.WriteLine("1. Sequential (Localization Pipeline)");
        Console.WriteLine("2. Concurrent (Parallel Analysis)");
        Console.WriteLine("3. HandOff (Triage & Support Routing)");
        Console.WriteLine("4. Group Chat (Crisis Management)");
        Console.WriteLine("Choice: ");

        var pattern = Console.ReadLine() switch
        {
            "1" => WorkflowType.Sequential,
            "2" => WorkflowType.Concurrent,
            "3" => WorkflowType.HandOff,
            "4" => WorkflowType.GroupChat,
            _ => throw new ArgumentException("Invalid worflow exception.")
        };

        var translationAgent = ((string[])["French", "Spanish", "English"]).Select(x => GetTranslationAgent(x, chatClient));

        switch (pattern)
        {
            case WorkflowType.Sequential:
                // ---- SEQUENTION PIPELINE using BuildSequential() ----
                // Date flows strictly from French -> Spanish -> English
                var sequentialWorkflow = AgentWorkflowBuilder.BuildSequential(translationAgent);
                await EnterpriseOrchestrator.RunWorkflowAsync(sequentialWorkflow, [new ChatMessage(ChatRole.User, "The new enterprise software update will be deployed at midnight.")]);
            
            break;

            case WorkflowType.Concurrent:
                // ---- CONCURRENT PIPELINE using BuildConcurrent() ----
                // All three agents process the identical payload simultaneously to reduce latency
                var concurrentWorkflow = AgentWorkflowBuilder.BuildConcurrent(translationAgent);
                await EnterpriseOrchestrator.RunWorkflowAsync(concurrentWorkflow, [new ChatMessage(ChatRole.User, "The new enterprise software update will be deployed at midnight.")]);
            
            break;

            case WorkflowType.HandOff:
                // --- HANDOFF ROUTING ---
                // Triage analyzes the user intent and delegates execution to the correct specialist.

                /* Testing Step:
                   [ This question targets Network_Admin Agent ]
                   1.Our office network keeps disconnecting that affects Wi-Fi then how to fix that? 

                   [ This question targets Billing_Support Agent ]
                   2.How many licenses are included in our subscription? 
                */

                ChatClientAgent networkAdmin = new(chatClient,
                    "You resolve network connectivity and DNS issues. Explain technical steps clearly.",
                    "Network_Admin", "Specialist for networking");

                ChatClientAgent billingSupport = new(chatClient,
                    "You handle enterprise invoice and licensing queries.",
                    "Billing_Support", "Specialist for licensing and billing");

                ChatClientAgent triageRouter = new(chatClient,
                    "Determine if the user needs Network or Billing support. ALWAYS handoff to the appropriate agent.",
                    "Triage_Router", "Routes messages to specialists");

                var handOffWorkflow = AgentWorkflowBuilder.CreateHandoffBuilderWith(triageRouter)
                    // Define the forward transition edges
                    .WithHandoffs(triageRouter, [networkAdmin, billingSupport])
                    // Define the reverse transition edges to return to triage if needed
                    .WithHandoffs([networkAdmin, billingSupport], triageRouter)
                    .Build();

                List<ChatMessage> conversation = [];

                while (true)
                {
                    Console.Write("Enterprise User: ");
                    conversation.Add(new ChatMessage(ChatRole.User, Console.ReadLine()));

                    var newMessages = await EnterpriseOrchestrator.RunWorkflowAsync(handOffWorkflow, conversation);
                    conversation.AddRange(newMessages);
                }

            case WorkflowType.GroupChat:
                // ---- GROUP CHAT (COLLABORATIVE SWARM) using BuildConcurrent() ----
                // Agent converse in a shared context window untill iteration limit is reached
                ChatClientAgent secOps = new(chatClient, "You are SecOps. Focus on security liabilities.", "SecOps");
                ChatClientAgent devOps = new(chatClient, "You are DevOps. Focus on security liabilities.", "DevOps");
                ChatClientAgent legalReview = new(chatClient, "You are LegalReview. Focus on security liabilities.", "LegalReview");

                var groupChatWorkflow = AgentWorkflowBuilder.CreateGroupChatBuilderWith(
                    agents => new RoundRobinGroupChatManager(agents) { MaximumIterationCount = 4 })
                    .AddParticipants([secOps, devOps, legalReview])
                    .Build();

                await EnterpriseOrchestrator.RunWorkflowAsync(groupChatWorkflow,
                    [new ChatMessage(ChatRole.User, "We need to push an emergency hotfix to the payment gateway database. Review the implications.")]);
                
            break;
        }
    }

    // Helper method to rapidly generate localization agents
    private static ChatClientAgent GetTranslationAgent(string targetLanguage, IChatClient chatClient) =>
        new(chatClient, $"""
            You are a localization expert. Translate the input into {targetLanguage}.
            Prepend your response with '[{targetLanguage}]:'.,name: ${targetLanguage}_Translator
            """);
}