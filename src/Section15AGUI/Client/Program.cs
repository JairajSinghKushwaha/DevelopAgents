
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.AGUI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

Console.WriteLine("Connecting to AG-UI server\n");

using var httpclient = new HttpClient();

var aguiClient = new AGUIChatClient(
    httpclient,
    "http://localhost:5000/agui/support",
    NullLoggerFactory.Instance);

AIAgent agent = aguiClient.AsAIAgent(
    name:"SupportClient",
    description: "You are a helpful enterprise support agent."
    );

Console.WriteLine("User: How can I reset my corporate password?");
Console.WriteLine("Agent: ");

// Send the message and stream the UI-compliant response in real-time
await foreach(var update in agent.RunStreamingAsync("How can I reset my corporate password?"))
{
    Console.Write(update.Text);
}