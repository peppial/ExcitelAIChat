using Microsoft.Extensions.AI;
using Azure.Search.Documents;

namespace AIChat.Services.Ingestion;

public class DataIngestor
{
    private readonly ILogger<DataIngestor> _logger;
    private readonly AzureSearchVectorStore _vectorStore;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;

    public DataIngestor(
        ILogger<DataIngestor> logger,
        AzureSearchVectorStore vectorStore,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
    {
        _logger = logger;
        _vectorStore = vectorStore;
        _embeddingGenerator = embeddingGenerator;
    }

    public static async Task IngestDataAsync(IServiceProvider services, IIngestionSource source)
    {
        using var scope = services.CreateScope();
        var ingestor = scope.ServiceProvider.GetRequiredService<DataIngestor>();
        await ingestor.IngestDataAsync(source);
    }
    public async Task IngestDataAsync(IIngestionSource source)
    {
        // Get documents that need to be processed
        var modifiedDocuments = await source.GetNewOrModifiedDocumentsAsync(new List<IngestedDocument>());

        int count = 0;
        foreach (var doc in modifiedDocuments)
        {
            _logger.LogInformation("Processing document number  {count} - {documentId}", ++count, doc.DocumentId);

            try
            {
                // Create chunks for the document
                var chunks = await source.CreateChunksForDocumentAsync(doc);

                foreach (var chunk in chunks)
                {
                    _logger.LogInformation("Upserting chunk {chunkId} from document {documentId}", chunk.Key, doc.DocumentId);

                    try
                    {
                        await Task.Delay(1000);
                        // Generate embedding for the chunk text
                        var embeddingResult = await _embeddingGenerator.GenerateAsync(chunk.Text);
                        var vector = embeddingResult.Vector.ToArray();

                        // Serialize the chunk as payload
                        var payloadJson = System.Text.Json.JsonSerializer.Serialize(chunk);

                        // Upload to vector store using chunk key as ID
                        await _vectorStore.UploadDocumentAsync(chunk.Key, vector, payloadJson);

                        _logger.LogDebug("Successfully uploaded chunk {chunkId}", chunk.Key);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to process chunk {chunkId} from document {documentId}",
                            chunk.Key, doc.DocumentId);
                        // Continue with next chunk instead of failing entire ingestion
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process document {documentId}", doc.DocumentId);
                // Continue with next document instead of failing entire ingestion
            }
        }

        _logger.LogInformation("Ingestion is up-to-date");
    }
}
