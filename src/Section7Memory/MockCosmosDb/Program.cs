using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using OpenAI.Chat;
using System.Text.Json;

public interface ISessionRepository
{
    Task<string?> GetSessionJsonAsync(string sessionId);
    Task SaveSessionJsonAsync(string sessionId, string jsonPayload);
}

// Mock implementation of a database like Cosmos DB or SQL Server
public class MockCosmosDbRepository : ISessionRepository
{
    private readonly Dictionary<string, string> _dataStore = new();

    public Task<string?> GetSessionJsonAsync(string sessionId) =>
     Task.FromResult(_dataStore.TryGetValue(sessionId, out var json) ? json : null);

    public Task SaveSessionJsonAsync(string sessionId, string jsonPayload)
    {
        _dataStore[sessionId] = jsonPayload;
        return Task.CompletedTask;
    }
}

public class SessionlessAgentService
{
    private readonly AIAgent _agent;
    private readonly ISessionRepository _repository;

    public SessionlessAgentService(ISessionRepository repository)
    {
        _repository = repository;

        // Define the variables we extracted from Microsoft Foundry 
        var deploymentModelName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") ?? "gpt-5-mini";

        var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
                        ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set.");

        _agent = new AzureOpenAIClient(new Uri(endpoint), new AzureCliCredential())
            .GetChatClient(deploymentModelName)
            .AsAIAgent(
              name: "PersistentGuide",
              instructions: "You are a helpful assistant. You remember details across long periods of time."
            );
    }

    public async Task<string> HandleUserMessageAsync(string sessionId, string userMessage)
    {
        AgentSession session;

        // Attempt to retrieve historical state from the database
        string? savedSessionJson = await _repository.GetSessionJsonAsync(sessionId);

        if (!string.IsNullOrEmpty(savedSessionJson))
        {
            // Parse the database string back into a JsonElement
            using JsonDocument doc = JsonDocument.Parse(savedSessionJson);

            // Deserialize the session, restore the agent's memory
            session = await _agent.DeserializeSessionAsync(doc.RootElement);
            Console.WriteLine($"[SYSTEM LOG] Successful restored session {sessionId} from database.");
        }
        else
        {
            // Fallback - Create a brand new session if no history exists.
            session = await _agent.CreateSessionAsync();
            Console.WriteLine($"[SYSTEM LOG] Created a new session for {sessionId}");
        }

        // Execute the agent with loaded session state
        AgentResponse response = await _agent.RunAsync(userMessage, session);

        // Serialize the newly updated session state
        JsonElement updatedSesionElement = await _agent.SerializeSessionAsync(session);
        string updateJsonString = JsonSerializer.Serialize(session);

        // Persist the updated state back to the database
        await _repository.SaveSessionJsonAsync(sessionId, updateJsonString);

        return response.Text;
    }
}

class Program
{
    public static async Task Main()
    {
        // Setup our mock database and agent service
        var repository = new MockCosmosDbRepository();
        var agentService = new SessionlessAgentService(repository);

        string userId = "user-778899";
        Console.WriteLine("----- Monday Morning -----");
        string response = await agentService.HandleUserMessageAsync(userId, "Hi, I am planning a trip to Tokyo next month.");
        Console.WriteLine($"Agent: {response}\n");

        // The Application could completely shutdown or restart here.
        // The memory is safely store in the repository.

        Console.WriteLine("--- Friday Afternoon (Simulating a new server request) ---");
        string resp = await agentService.HandleUserMessageAsync(userId, "Do you remember where I said I was travelling?");

        Console.WriteLine($"Agent: {resp}\n");
    }
}