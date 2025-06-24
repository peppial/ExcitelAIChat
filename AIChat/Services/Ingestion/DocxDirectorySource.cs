using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;


namespace AIChat.Services.Ingestion;

public class DocxDirectorySource(string sourceDirectory) : IIngestionSource
{
    public static string SourceFileId(string path) => Path.GetFileName(path);
    public static string SourceFileVersion(string path) => File.GetLastWriteTimeUtc(path).ToString("o");

    public string SourceId => $"{nameof(DocxDirectorySource)}:{sourceDirectory}";

    public Task<IEnumerable<IngestedDocument>> GetNewOrModifiedDocumentsAsync(IReadOnlyList<IngestedDocument> existingDocuments)
    {
        var results = new List<IngestedDocument>();
        var sourceFiles = Directory.GetFiles(sourceDirectory, "*.docx").OrderBy(f => f);

        var existingDocumentsById = existingDocuments.ToDictionary(d => d.DocumentId);

        foreach (var sourceFile in sourceFiles)
        {
            var sourceFileId = SourceFileId(sourceFile);
            var sourceFileVersion = SourceFileVersion(sourceFile);
            var existingDocumentVersion = existingDocumentsById.TryGetValue(sourceFileId, out var existingDocument) ? existingDocument.DocumentVersion : null;
            if (existingDocumentVersion != sourceFileVersion)
            {
                results.Add(new() { Key = Guid.NewGuid().ToString(), SourceId = SourceId, DocumentId = sourceFileId, DocumentVersion = sourceFileVersion });
            }
        }

        return Task.FromResult((IEnumerable<IngestedDocument>)results);
    }

    public Task<IEnumerable<IngestedDocument>> GetDeletedDocumentsAsync(IReadOnlyList<IngestedDocument> existingDocuments)
    {
        var currentFiles = Directory.GetFiles(sourceDirectory, "*.docx");
        var currentFileIds = currentFiles.ToLookup(SourceFileId);
        var deletedDocuments = existingDocuments.Where(d => !currentFileIds.Contains(d.DocumentId));
        return Task.FromResult(deletedDocuments);
    }

    public Task<IEnumerable<IngestedChunk>> CreateChunksForDocumentAsync(IngestedDocument document)
    {
        var filePath = Path.Combine(sourceDirectory, document.DocumentId);
        var text = ExtractTextFromDocx(filePath);
        var paragraphs = SplitTextIntoParagraphs(text, 200).ToList();

        return Task.FromResult(paragraphs.Select((p, idx) => new IngestedChunk
        {
            Key = Guid.NewGuid().ToString(),
            DocumentId = document.DocumentId,
            PageNumber = idx + 1,
            Text = p,
        }));
    }

    private static string ExtractTextFromDocx(string filePath)
    {
        using var wordDoc = WordprocessingDocument.Open(filePath, false);
        var body = wordDoc.MainDocumentPart?.Document.Body;
        if (body == null) return string.Empty;
        return string.Join("\n", body.Descendants<Paragraph>().Select(p => p.InnerText));
    }

    private static IEnumerable<string> SplitTextIntoParagraphs(string text, int maxLength)
    {
        var paragraphs = new List<string>();
        var current = string.Empty;
        foreach (var line in text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
        {
            if ((current.Length + line.Length + 1) > maxLength && !string.IsNullOrWhiteSpace(current))
            {
                paragraphs.Add(current);
                current = string.Empty;
            }
            current += (current.Length > 0 ? " " : string.Empty) + line.Trim();
        }
        if (!string.IsNullOrWhiteSpace(current))
            paragraphs.Add(current);
        return paragraphs;
    }
}
