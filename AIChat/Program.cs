using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using AIChat.Components;
using AIChat.Services;
using AIChat.Services.Ingestion;
using Azure.Search.Documents;
using Azure;
using Microsoft.Agents.AI;
using OpenAI;
using System.ClientModel;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

// Azure OpenAI v1 endpoint, used through the OpenAI SDK
OpenAIClient openAIClient = new(
    new ApiKeyCredential(Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY")!),
    new OpenAIClientOptions
    {
        Endpoint = new Uri($"{Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")!.TrimEnd('/')}/openai/v1/")
    });
var chatClient = openAIClient.GetChatClient("gpt-4o-mini").AsIChatClient();
var embeddingGenerator = openAIClient.GetEmbeddingClient("text-embedding-3-small").AsIEmbeddingGenerator();

var azureSearchEndpoint = Environment.GetEnvironmentVariable("AZURE_SEARCH_ENDPOINT");
var azureSearchKey = Environment.GetEnvironmentVariable("AZURE_SEARCH_API_KEY");

var azureAISearchCredential = new AzureKeyCredential(azureSearchKey);
builder.Services.AddAzureAISearchCollection<IngestedChunk>("data-test-chunks", new Uri(azureSearchEndpoint), azureAISearchCredential);
builder.Services.AddAzureAISearchCollection<IngestedDocument>("data-test-documents", new Uri(azureSearchEndpoint), azureAISearchCredential);


builder.Services.AddScoped<DataIngestor>();
builder.Services.AddSingleton<SemanticSearch>();
builder.Services.AddChatClient(chatClient).UseFunctionInvocation().UseLogging();
builder.Services.AddEmbeddingGenerator(embeddingGenerator);

builder.Services.AddSingleton<SearchFunctions>();
builder.Services.AddSingleton<JiraMcpClient>();
builder.Services.AddSingleton<JiraAIFunctions>();

// Register the chat agent as a harness agent. Long-running-task features (plan/execute modes,
// todos, file memory, skills, web search) are disabled to keep plain Q&A chat behavior.
builder.Services.AddKeyedSingleton<AIAgent>("ChatAgent", (sp, key) =>
{
    var searchFunctions = sp.GetRequiredService<SearchFunctions>();
    var jiraFunctions = sp.GetRequiredService<JiraAIFunctions>();

    // The harness adds its own function invocation and OpenTelemetry, so it wraps the raw chat client
    return chatClient.AsHarnessAgent(new HarnessAgentOptions
    {
        Name = (string)key!,
        Description = "Answers questions about Excitel software systems and Jira issues.",
        ChatOptions = new ChatOptions
        {
            Instructions = AgentPrompts.System,
            Tools =
            [
                AIFunctionFactory.Create(searchFunctions.SearchAsync),
                AIFunctionFactory.Create(jiraFunctions.SearchJiraIssues),
                AIFunctionFactory.Create(jiraFunctions.GetJiraIssue),
                AIFunctionFactory.Create(jiraFunctions.GetRecentJiraIssues),
                AIFunctionFactory.Create(jiraFunctions.GetJiraIssuesByProject),
            ],
        },
        // Token limits enable in-loop compaction; still marked evaluation-only in 1.21
#pragma warning disable MAAI001
        MaxContextWindowTokens = 128_000,
        MaxOutputTokens = 16_384,
#pragma warning restore MAAI001
        DisableAgentModeProvider = true,
        DisableTodoProvider = true,
        DisableFileMemory = true,
        DisableAgentSkillsProvider = true,
        DisableWebSearch = true,
    });
});


builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromDays(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();

app.UseStaticFiles();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.UseSession();
app.UseMiddleware<SimpleAuthMiddleware>();

var pdfSource = new PDFDirectorySource(Path.Combine(app.Environment.ContentRootPath, "wwwroot/Data"));
var docxSource = new DocxDirectorySource(Path.Combine(app.Environment.ContentRootPath, "wwwroot/Data"));

using (var scope = app.Services.CreateScope())
{
    var dataIngestor = scope.ServiceProvider.GetRequiredService<DataIngestor>();

    // Uncomment the following lines to ingest data from PDF and DOCX files

    // Ingest PDF files
    // dataIngestor.IngestDataAsync(pdfSource).GetAwaiter().GetResult();

    // Ingest DOCX files
    //dataIngestor.IngestDataAsync(docxSource).GetAwaiter().GetResult();
}

app.Run();
