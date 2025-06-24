using Microsoft.Extensions.AI;
using System.ComponentModel;

namespace AIChat.Services;

public class JiraAIFunctions
{
    private readonly JiraMcpClient _jiraMcpClient;
    private readonly ILogger<JiraAIFunctions> _logger;
    private readonly IConfiguration _configuration;

    public JiraAIFunctions(JiraMcpClient jiraMcpClient, ILogger<JiraAIFunctions> logger, IConfiguration configuration)
    {
        _jiraMcpClient = jiraMcpClient;
        _logger = logger;
        _configuration = configuration;
    }

    private string GetJiraUrl()
    {
        return Environment.GetEnvironmentVariable("JIRA_URL");
    }

    private string GetJiraIssueUrl(string issueKey)
    {
        var baseUrl = GetJiraUrl().TrimEnd('/');
        return $"{baseUrl}/browse/{issueKey}";
    }

    [Description("Search for Jira issues using JQL (Jira Query Language)")]
    public async Task<string> SearchJiraIssues(
        [Description("JQL query string to search for issues. Examples: 'assignee = currentUser()', 'project = PROJ AND status = \"In Progress\"', 'created >= -7d'")]
        string jql,
        [Description("Maximum number of results to return (default: 10)")]
        int maxResults = 10)
    {
        try
        {
            _logger.LogInformation("Searching Jira issues with JQL: {Jql}", jql);

            var issues = await _jiraMcpClient.SearchJiraIssuesAsync(jql, maxResults);

            if (issues.Length == 0)
            {
                return "No issues found matching the search criteria.";
            }

            var result = $"Found {issues.Length} issue(s):\n\n";

            foreach (var issue in issues)
            {
                result += $"**[{issue.Key}]({GetJiraIssueUrl(issue.Key)}** - {issue.Summary}\n";
                result += $"Status: {issue.Status?.Name ?? "Unknown"}\n";
                result += $"Type: {issue.IssueType?.Name ?? "Unknown"}\n";
                result += $"Priority: {issue.Priority?.Name ?? "Unknown"}\n";
                result += $"Assignee: {issue.Assignee?.DisplayName ?? "Unassigned"}\n";

                if (!string.IsNullOrEmpty(issue.Description))
                {
                    var shortDescription = issue.Description.Length > 200
                        ? issue.Description.Substring(0, 200) + "..."
                        : issue.Description;
                    result += $"Description: {shortDescription}\n";
                }

                result += $"Created: {issue.Created}\n";
                result += $"Updated: {issue.Updated}\n\n";
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching Jira issues");
            return $"Error searching Jira issues: {ex.Message}";
        }
    }

    [Description("Get detailed information about a specific Jira issue")]
    public async Task<string> GetJiraIssue(
        [Description("The Jira issue key (e.g., 'PROJ-123')")]
        string issueKey)
    {
        try
        {
            _logger.LogInformation("Getting Jira issue: {IssueKey}", issueKey);

            var issue = await _jiraMcpClient.GetJiraIssueAsync(issueKey);

            if (issue == null)
            {
                return $"Issue {issueKey} not found.";
            }

            var result = $"**[{issue.Key}]({GetJiraIssueUrl(issue.Key)}** - {issue.Summary}\n\n";
            result += $"**Status:** {issue.Status?.Name ?? "Unknown"}\n";
            result += $"**Type:** {issue.IssueType?.Name ?? "Unknown"}\n";
            result += $"**Priority:** {issue.Priority?.Name ?? "Unknown"}\n";
            result += $"**Assignee:** {issue.Assignee?.DisplayName ?? "Unassigned"}\n";
            result += $"**Reporter:** {issue.Reporter?.DisplayName ?? "Unknown"}\n";
            result += $"**Created:** {issue.Created}\n";
            result += $"**Updated:** {issue.Updated}\n";

            if (!string.IsNullOrEmpty(issue.Description))
            {
                result += $"**Description:**\n{issue.Description}\n";
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Jira issue {IssueKey}", issueKey);
            return $"Error getting Jira issue: {ex.Message}";
        }
    }


    [Description("Get recent Jira issues updated in the last week")]
    public async Task<string> GetRecentJiraIssues(
        [Description("Number of days to look back (default: 7)")]
        int days = 7)
    {
        var jql = $"updated >= -{days}d ORDER BY updated DESC";
        return await SearchJiraIssues(jql, 15);
    }

    [Description("Get Jira issues by project")]
    public async Task<string> GetJiraIssuesByProject(
        [Description("The project key (e.g., 'PROJ')")]
        string projectKey,
        [Description("Maximum number of results (default: 10)")]
        int maxResults = 10)
    {
        var jql = $"project = {projectKey} ORDER BY updated DESC";
        return await SearchJiraIssues(jql, maxResults);
    }

}
