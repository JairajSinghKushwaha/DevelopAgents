using Azure.AI.OpenAI;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI.Chat;
using OpenAI.Files;
using OpenAI.Responses;
using OpenAI.VectorStores;

// Define the variables we extracted from Microsoft Foundry 
var deploymentModelName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") ?? "gpt-5-mini";

// Admin > All Projects > Name (or Parent resource) > Endpoint (Base URL)
var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
                ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set.");

// Build the tools list and add thr native Code Interpreter ResponseTool via the AITool bridge extension
IList<AITool> tools = [];

// ********************************************************|| Code Interpreter Tool ||*****************************************************************************

#pragma warning disable OPENAI001

// CreateCodeInterpreterTool() that runs Python code in a secure sandbox. The sandbox container is created automatically.
tools.Add(ResponseTool.CreateCodeInterpreterTool(
    new CodeInterpreterToolContainer(CodeInterpreterToolContainerConfiguration.CreateAutomaticContainerConfiguration([]))
    ));

#pragma warning restore OPENAI001

// Initialize the Agent and inject the native Code Interprator tool
AIAgent agent = new AzureOpenAIClient(new Uri(endpoint), new AzureCliCredential())
    .GetChatClient(deploymentModelName)
    .AsAIAgent(
     name: "DataAnalyst",
     instructions: """
          You are a data analyst. You must write and execute Python code to answer complex math and data questions.
          Never guess the answer.
         """,
     tools: tools
    );

Console.WriteLine($"Agent: '{agent.Name}' is online with a Python sandbox.\n");

// The agent will autonomously write a script to solve this, run it, and return the result.
string prompt = "Colculate the 10th Fibonacci number and determine if it is a prime number.";
Console.WriteLine($"User: {prompt}");

AgentResponse response = await agent.RunAsync(prompt);
Console.WriteLine($"Agent: {response.Text}");


//// ******************************************************|| File Search Tool ||*********************************************************************


//// File Search Tool - In a real scenario, this ID is retrived from your Azure AI Foundry project


// Create project client to call Foundry API
AIProjectClient projectClient = new(new Uri(endpoint), new DefaultAzureCredential());

// Create a toy example file and upload it using OpenAI mechanism.
string filePath = @"Q3Report.JSON";

string content = """
[{
  "title": "Q3 Report",
  "Company": "ABC IT",
  "timestamp": "2026-07-02T09:14:32Z",
  "risk_factor": "Revenue concentration",
  "severity": "High",
  "metric": "Top 5 customers share of revenue",
  "value": 42.7,
  "unit": "percent",
  "threshold": 35.0,
  "status": "Above threshold",
  "source": "ERP"
},
{
  "title": "Q3 Report",
  "Company": "XYZ IT",
  "timestamp": "2026-07-08T15:42:11Z",
  "risk_factor": "Customer churn",
  "severity": "Medium",
  "metric": "Quarterly churn rate",
  "value": 6.8,
  "unit": "percent",
  "threshold": 5.0,
  "status": "Above threshold",
  "source": "CRM"
}]
""";

// Attaching the file and writing content on that file and then upload.
File.WriteAllText(filePath, content);

OpenAIFileClient fileClient = projectClient.ProjectOpenAIClient.GetOpenAIFileClient();
OpenAIFile uploadedFile = fileClient.UploadFile(filePath: filePath, purpose: FileUploadPurpose.Assistants);

#pragma warning disable OPENAI001

// Create the VectorStore and provide it with uploaded file ID.
VectorStoreClient vectorStoreClient = projectClient.ProjectOpenAIClient.GetVectorStoreClient();
VectorStoreCreationOptions options = new()
{
    Name = "VestorStore",
    FileIds = { uploadedFile.Id }
};

VectorStore vectorStore = vectorStoreClient.CreateVectorStore(options);

#pragma warning disable OPENAI001

ResponseTool fileSearchTool = ResponseTool.CreateFileSearchTool(vectorStoreIds: [vectorStore.Id]);

var responseClient = projectClient.ProjectOpenAIClient.GetProjectResponsesClientForModel(deploymentModelName);

prompt = "What were the key risk factors in the Q3 Report?";

CreateResponseOptions responseOptions = new()
{
    Instructions = "You are a helpful agent that can help fetch data from any files you know about.",
    Tools = { fileSearchTool },
    InputItems = { ResponseItem.CreateUserMessageItem(prompt) }
};

Console.WriteLine($"\nUser: {prompt}");

ResponseResult result = responseClient.CreateResponse(responseOptions);

#pragma warning restore OPENAI001

Console.WriteLine($"Agent: {result.GetOutputText()}");

// Remove all the resources created in this sample.
vectorStoreClient.DeleteVectorStore(vectorStoreId: vectorStore.Id);
fileClient.DeleteFile(uploadedFile.Id);

// *********************************************************|| Web Search Tool ||*********************************************************************

#pragma warning disable OPENAI001

var webSearchTool = ResponseTool.CreateWebSearchTool(searchContextSize: WebSearchToolContextSize.Medium).AsAITool();

#pragma warning restore OPENAI001

// Web Search Tool, Initialize the Agent with live internet access.
agent = new AzureOpenAIClient(new Uri(endpoint), new AzureCliCredential())
        .GetChatClient(deploymentModelName)
        .AsAIAgent(
            name: "MarketResearcher",
            instructions: """
            You are a market researcher. Always verify current event using the web search tool before providing an answer. 
            Cite your sources. 
            """,
            tools: [webSearchTool]
         );

prompt = "What were the yesterday's major top 3 tech announcements from a global/international news?";
Console.WriteLine($"\nUser: {prompt}");

var resp = await agent.RunAsync(prompt);
Console.WriteLine($"Agent: {resp.Text}");