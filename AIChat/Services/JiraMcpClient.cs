using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Client;

namespace AIChat.Services;

public class JiraMcpClient : IDisposable
{
    private readonly ILogger<JiraMcpClient> _logger;
    private readonly IConfiguration _configuration;
    private IMcpClient? _mcpClient;
    private bool _isInitialized = false;
    private readonly SemaphoreSlim _initSemaphore = new(1, 1);
    private bool _disposed = false;
    public JiraMcpClient(ILogger<JiraMcpClient> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<JiraIssue[]> SearchJiraIssuesAsync(string jql, int maxResults = 20)
    {
        try
        {
            var client = await EnsureInitializedAsync();
            if (client == null)
            {
                _logger.LogError("Failed to initialize MCP client");
                return Array.Empty<JiraIssue>();
            }

            var result = await client.CallToolAsync(
                $"jira_search",
                new Dictionary<string, object?>
                {
                    ["jql"] = jql,
                    ["limit"] = maxResults
                },
                cancellationToken: CancellationToken.None);

            var textContent = result.Content.FirstOrDefault(c => c.Type == "text");
            if (textContent?.Text == null)
            {
                _logger.LogWarning("No text content returned from Jira search");
                return Array.Empty<JiraIssue>();
            }

            // Log the raw JSON response for debugging
            _logger.LogInformation("Raw Jira search response: {RawResponse}", textContent.Text);

            try
            {
                var searchResponse = JsonSerializer.Deserialize<JiraSearchResponse>(textContent.Text, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                });

                return searchResponse?.Issues ?? Array.Empty<JiraIssue>();
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize Jira search response. Raw response: {RawResponse}", textContent.Text);
                return Array.Empty<JiraIssue>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching Jira issues with JQL: {Jql}", jql);
            return Array.Empty<JiraIssue>();
        }
    }

    public async Task<JiraIssue?> GetJiraIssueAsync(string issueKey)
    {
        try
        {
            var client = await EnsureInitializedAsync();
            if (client == null)
            {
                _logger.LogError("Failed to initialize MCP client");
                return null;
            }

            var result = await client.CallToolAsync(
                $"jira_get_issue",
                new Dictionary<string, object?>
                {
                    ["issue_key"] = issueKey
                },
                cancellationToken: CancellationToken.None);

            var textContent = result.Content.FirstOrDefault(c => c.Type == "text");
            if (textContent?.Text == null)
            {
                _logger.LogWarning("No text content returned from Jira get issue");
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<JiraIssue>(textContent.Text, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize Jira issue response. Raw response: {RawResponse}", textContent.Text);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Jira issue: {IssueKey}", issueKey);
            return null;
        }
    }

    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            var client = await EnsureInitializedAsync();
            if (client == null)
            {
                return false;
            }

            // Try to list tools to check if the connection is working
            var tools = await client.ListToolsAsync();
            return tools.Any();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MCP client health check failed");
            return false;
        }
    }

    public async Task<string[]> GetAvailableToolsAsync()
    {
        try
        {
            var client = await EnsureInitializedAsync();
            if (client == null)
            {
                _logger.LogError("Failed to initialize MCP client");
                return Array.Empty<string>();
            }

            var tools = await client.ListToolsAsync();
            return tools.Select(t => t.Name).ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available tools");
            return Array.Empty<string>();
        }
    }


    private async Task<IMcpClient?> EnsureInitializedAsync()
    {
        if (_isInitialized && _mcpClient != null)
            return _mcpClient;

        await _initSemaphore.WaitAsync();
        try
        {
            if (_isInitialized && _mcpClient != null)
                return _mcpClient;

            var clientTransport = new StdioClientTransport(new StdioClientTransportOptions
            {
                Name = "Jira MCP Server",
                Command = "docker",
                Arguments = [
                    "run",
                    "-i",
                    "--rm",
                    "--env-file",
                    "mcp-atlassian.env",
                    "ghcr.io/sooperset/mcp-atlassian:latest",
                    "-vv"
                ]
            });

            _mcpClient = await McpClientFactory.CreateAsync(clientTransport);
            _isInitialized = true;

            _logger.LogInformation("MCP client initialized successfully");

            // Log available tools for debugging
            var tools = await _mcpClient.ListToolsAsync();
            _logger.LogInformation("Available MCP tools count: {Count}", tools.Count());
            foreach (var tool in tools.Take(5)) // Only log first 5 tools to avoid spam
            {
                _logger.LogDebug("Available tool: {Name}", tool.Name);
            }

            return _mcpClient;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing MCP client");
            return null;
        }
        finally
        {
            _initSemaphore.Release();
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_mcpClient is IDisposable disposableClient)
            {
                disposableClient.Dispose();
            }
            _initSemaphore.Dispose();
            _disposed = true;
        }
    }
}

// MCP Protocol Models
public class McpRequest
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    [JsonPropertyName("params")]
    public object? Params { get; set; }
}

public class McpCallParams
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("arguments")]
    public Dictionary<string, object> Arguments { get; set; } = new();
}

public class McpResponse<T>
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = string.Empty;

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("result")]
    public T? Result { get; set; }

    [JsonPropertyName("error")]
    public McpError? Error { get; set; }
}

public class McpError
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public object? Data { get; set; }
}

// Jira Models
public class JiraSearchResponse
{
    [JsonPropertyName("issues")]
    public JiraIssue[] Issues { get; set; } = Array.Empty<JiraIssue>();

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("start_at")]
    public int StartAt { get; set; }

    [JsonPropertyName("max_results")]
    public int MaxResults { get; set; }
}

public class JiraIssue
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("status")]
    public JiraStatus? Status { get; set; }

    [JsonPropertyName("issue_type")]
    public JiraIssueType? IssueType { get; set; }

    [JsonPropertyName("priority")]
    public JiraPriority? Priority { get; set; }

    [JsonPropertyName("assignee")]
    public JiraUser? Assignee { get; set; }

    [JsonPropertyName("reporter")]
    public JiraUser? Reporter { get; set; }

    [JsonPropertyName("created")]
    public string? Created { get; set; }

    [JsonPropertyName("updated")]
    public string? Updated { get; set; }
}

public class JiraStatus
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public class JiraIssueType
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public class JiraPriority
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public class JiraUser
{
    [JsonPropertyName("account_id")]
    public string AccountId { get; set; } = string.Empty;

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string? Email { get; set; }
}

public class JiraTransition
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

// MCP Initialization Models
public class McpInitializeParams
{
    [JsonPropertyName("protocolVersion")]
    public string ProtocolVersion { get; set; } = string.Empty;

    [JsonPropertyName("capabilities")]
    public McpCapabilities Capabilities { get; set; } = new();

    [JsonPropertyName("clientInfo")]
    public McpClientInfo ClientInfo { get; set; } = new();
}

public class McpCapabilities
{
    // Add capabilities as needed
}

public class McpClientInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;
}
