using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using System.Text;

namespace AIChat.Services;

public class AzureSearchVectorStore
{
    private readonly SearchClient _searchClient;
    public AzureSearchVectorStore(SearchClient searchClient)
    {
        _searchClient = searchClient;
    }

    private static string EncodeDocumentKey(string originalKey)
    {
        var bytes = Encoding.UTF8.GetBytes(originalKey);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    public async Task UploadDocumentAsync(string id, float[] vector, string payloadJson)
    {
        var encodedId = EncodeDocumentKey(id);
        var doc = new Dictionary<string, object>
        {
            { "id", encodedId },
            { "embedding", vector },
            { "payloadJson", payloadJson }
        };
        await _searchClient.UploadDocumentsAsync(new[] { doc });
    }

    public async Task<SearchResults<T>> VectorSearchAsync<T>(float[] queryVector, int top = 10)
    {
        var searchOptions = new SearchOptions
        {
            VectorSearch = new()
            {
                Queries =
                {
                    new VectorizedQuery(queryVector)
                    {
                        KNearestNeighborsCount = top,
                        Fields = { "embedding" }
                    }
                }
            },
            Size = top
        };

        return await _searchClient.SearchAsync<T>("*", searchOptions);
    }
}
