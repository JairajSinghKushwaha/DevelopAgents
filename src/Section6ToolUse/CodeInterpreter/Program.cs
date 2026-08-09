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

//string corporateVectorStoreId = "vs-987654321";

//#pragma warning disable OPENAI001

//tools.Add(ResponseTool.CreateFileSearchTool(vectorStoreIds: [corporateVectorStoreId]));

//#pragma warning restore OPENAI001

//// Initialize the Agent with the File Search capability pointing to your data
//agent = new AzureOpenAIClient(new Uri(endpoint), new AzureCliCredential())
//        .GetChatClient(deploymentModelName)
//        .AsAIAgent(
//            name: "FinancialAnalyst",
//            instructions: "You are a financial analyst. Answer questions strictly based on the provided corporate decumnet",
//            tools: [tools.LastOrDefault()]
//        );

//prompt = "What were the key risk factors in the Q3 Report?";
//Console.WriteLine($"User: {prompt}");

//response = await agent.RunAsync(prompt);
//Console.WriteLine($"Agent: {response.Text}");

// -----------------------------------------------------------------------------------------------

// Create project client to call Foundry API
AIProjectClient projectClient = new(new Uri(endpoint), new DefaultAzureCredential());

// Create a toy example file and upload it using OpenAI mechanism.
string filePath = @"UserDetailsLog.txt";

string content = """
The word 'apple' uses the code 442345.
The word 'banana' uses the code 673457.
The word 'orange' uses the code 781234.
""";

// Attaching the file and writing content on that file and then upload.
File.WriteAllText(filePath, content);

OpenAIFileClient fileClient = projectClient.ProjectOpenAIClient.GetOpenAIFileClient();
OpenAIFile uploadedFile = fileClient.UploadFile(filePath: filePath, purpose: FileUploadPurpose.Assistants);

#pragma warning disable OPENAI001

// Create the VectorStore and provide it with uploaded file ID.
VectorStoreClient vctStoreClient = projectClient.ProjectOpenAIClient.GetVectorStoreClient();
VectorStoreCreationOptions options = new()
{
    Name = "MySampleStore",
    FileIds = { uploadedFile.Id }
};

VectorStore vectorStore = vctStoreClient.CreateVectorStore(options);

#pragma warning disable OPENAI001

ResponseTool fileSearchTool = ResponseTool.CreateFileSearchTool(vectorStoreIds: [vectorStore.Id]);

var responseClient = projectClient.ProjectOpenAIClient.GetProjectResponsesClientForModel(deploymentModelName);

prompt = "Can you give me the documented codes for 'banana' and 'orange'?";

CreateResponseOptions responseOptions = new() 
{
    Instructions = "You are a helpful agent that can help fetch data from files you know about.",
    Tools = { fileSearchTool },
    InputItems = { ResponseItem.CreateUserMessageItem(prompt) }
};

Console.WriteLine($"User: {prompt}");

ResponseResult result = responseClient.CreateResponse(responseOptions);

#pragma warning restore OPENAI001

Console.WriteLine($"Agent: {result.GetOutputText()}");

// Remove all the resources created in this sample.
vctStoreClient.DeleteVectorStore(vectorStoreId: vectorStore.Id);
fileClient.DeleteFile(uploadedFile.Id);

// *********************************************************************************************************************************

//#pragma warning disable OPENAI001

//tools.Add(ResponseTool.CreateWebSearchTool(
//    new CodeInterpreterToolContainer(CodeInterpreterToolContainerConfiguration.CreateAutomaticContainerConfiguration([]))
//    ));
//#pragma warning restore OPENAI001

//// Web Search Tool, Initialize the Agent with live internet access.
//agent = new AzureOpenAIClient(new Uri(endpoint), new AzureCliCredential())
//        .GetChatClient(deploymentModelName)
//        .AsAIAgent(
//            name: "MarketResearcher",
//            instructions: "You are a market researcher. Always verify current event using the web search tool before providing an answer. Cite your sources.",
//            tools: [new WebSearchToolDefinition()]
//         );

//prompt = "What were the yesterday's major tech announcements?";
