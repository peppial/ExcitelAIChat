using System.Text.Json;
using Microsoft.Extensions.AI;

namespace AIChat.Services;

public class SemanticSearch
{
    private readonly AzureSearchVectorStore _vectorStore;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;

    public SemanticSearch(AzureSearchVectorStore vectorStore, IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
    {
        _vectorStore = vectorStore;
        _embeddingGenerator = embeddingGenerator;
    }
    public async Task<IReadOnlyList<IngestedChunk>> SearchAsync(string text, string? documentIdFilter, int maxResults)
    {
        var embeddingResult = await _embeddingGenerator.GenerateAsync(text);
        var vector = embeddingResult.Vector.ToArray();
        var searchResults = await _vectorStore.VectorSearchAsync<Dictionary<string, object>>(vector, maxResults);
        var chunks = new List<IngestedChunk>(); 
        await foreach (var result in searchResults.GetResultsAsync())
        {
            if (result.Document.TryGetValue("payloadJson", out var payloadObj))
            {
                string payloadJson = null;

                if (payloadObj is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.String)
                {
                    payloadJson = jsonElement.GetString();
                }

                if (!string.IsNullOrEmpty(payloadJson))
                {
                    var chunk = JsonSerializer.Deserialize<IngestedChunk>(payloadJson);
                    if (chunk != null && (string.IsNullOrEmpty(documentIdFilter) || chunk.DocumentId == documentIdFilter))
                        chunks.Add(chunk);
                }
            }
        }
        return chunks;
    }
}
