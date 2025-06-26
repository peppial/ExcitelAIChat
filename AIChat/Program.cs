using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using AIChat.Components;
using AIChat.Services;
using AIChat.Services.Ingestion;
using Azure.Search.Documents;
using Azure;
using Azure.AI.OpenAI;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

AzureOpenAIClient azureClient = new(
    new Uri(Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")),
    new AzureKeyCredential(Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY")));
var chatClient = azureClient.GetChatClient("gpt-4o-mini").AsIChatClient();
var embeddingGenerator = azureClient.GetEmbeddingClient("text-embedding-3-small").AsIEmbeddingGenerator();

var azureSearchEndpoint = Environment.GetEnvironmentVariable("AZURE_SEARCH_ENDPOINT");
var azureSearchKey = Environment.GetEnvironmentVariable("AZURE_SEARCH_API_KEY");
var azureSearchIndex = "prd";

if (string.IsNullOrEmpty(azureSearchEndpoint) || string.IsNullOrEmpty(azureSearchKey))
{
    throw new InvalidOperationException("Missing Azure Search endpoint or API key. Set AZURE_SEARCH_ENDPOINT and AZURE_SEARCH_API_KEY in environment variables.");
}
var searchClient = new SearchClient(new Uri(azureSearchEndpoint), azureSearchIndex, new AzureKeyCredential(azureSearchKey));
builder.Services.AddSingleton(searchClient);
builder.Services.AddSingleton<AzureSearchVectorStore>();

builder.Services.AddScoped<DataIngestor>();
builder.Services.AddSingleton<SemanticSearch>();

// Configure HTTP client for Jira MCP
builder.Services.AddHttpClient<JiraMcpClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Register Jira services
builder.Services.AddScoped<JiraMcpClient>();
builder.Services.AddScoped<JiraAIFunctions>();

// Configure AI chat client with function calling
builder.Services.AddChatClient(chatClient)
    .UseFunctionInvocation()
    .UseLogging();

builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(embeddingGenerator);
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
