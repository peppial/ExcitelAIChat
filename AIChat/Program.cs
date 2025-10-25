using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using AIChat.Components;
using AIChat.Services;
using AIChat.Services.Ingestion;
using Azure.Search.Documents;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

AzureOpenAIClient azureClient = new(
    new Uri(Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")),
    new AzureKeyCredential(Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY")));
var chatClient = azureClient.GetChatClient("gpt-4o-mini").AsIChatClient();
var embeddingGenerator = azureClient.GetEmbeddingClient("text-embedding-3-small").AsIEmbeddingGenerator();

var azureSearchEndpoint = Environment.GetEnvironmentVariable("AZURE_SEARCH_ENDPOINT");
var azureSearchKey = Environment.GetEnvironmentVariable("AZURE_SEARCH_API_KEY");

var azureAISearchCredential = new AzureKeyCredential(azureSearchKey);
builder.Services.AddAzureAISearchCollection<IngestedChunk>("data-test-chunks", new Uri(azureSearchEndpoint), azureAISearchCredential);
builder.Services.AddAzureAISearchCollection<IngestedDocument>("data-test-documents", new Uri(azureSearchEndpoint), azureAISearchCredential);


builder.Services.AddScoped<DataIngestor>();
builder.Services.AddSingleton<SemanticSearch>();
builder.Services.AddChatClient(chatClient).UseFunctionInvocation().UseLogging();
builder.Services.AddEmbeddingGenerator(embeddingGenerator);

// Register the AI Agent using the Agent Framework
builder.AddAIAgent("ChatAgent", (sp, key) =>
{
    // Get required services
    var logger = sp.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Configuring AI Agent with key '{Key}' for model '{Model}'", key, "gpt-4o-mini");

    var searchFunctions = sp.GetRequiredService<SearchFunctions>();
    var chatClient = sp.GetRequiredService<IChatClient>();

    // Create and configure the AI agent
    var aiAgent = chatClient.CreateAIAgent(
        name: key,
        instructions: "You are a useful agent that helps users.",
        description: "An AI agent that helps users.",
        tools: [AIFunctionFactory.Create(searchFunctions.SearchAsync)]
        )
    .AsBuilder()
    .UseOpenTelemetry(configure: c =>
        c.EnableSensitiveData = builder.Environment.IsDevelopment())
    .Build();

    return aiAgent;
});
builder.Services.AddSingleton<SearchFunctions>();

builder.Services.AddHttpClient<JiraMcpClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddScoped<JiraMcpClient>();
builder.Services.AddScoped<JiraAIFunctions>();


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
