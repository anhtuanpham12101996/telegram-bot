namespace TelegramRelay.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Root payload received from a GitLab webhook (merge request or note events).
/// </summary>
public sealed class GitLabWebhookPayload
{
    [JsonPropertyName("object_kind")]
    public string? ObjectKind { get; set; }

    [JsonPropertyName("event_type")]
    public string? EventType { get; set; }

    [JsonPropertyName("user")]
    public GitLabUser? User { get; set; }

    [JsonPropertyName("project")]
    public GitLabProject? Project { get; set; }

    [JsonPropertyName("object_attributes")]
    public GitLabObjectAttributes? ObjectAttributes { get; set; }

    [JsonPropertyName("merge_request")]
    public GitLabMergeRequest? MergeRequest { get; set; }

    [JsonIgnore]
    public int? ResolvedProjectId => Project?.Id;
}

public sealed class GitLabUser
{
    [JsonPropertyName("id")]
    public int? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonIgnore]
    public string DisplayName => !string.IsNullOrWhiteSpace(Name)
        ? Name
        : (!string.IsNullOrWhiteSpace(Username) ? Username : "GitLab User");
}

public sealed class GitLabProject
{
    [JsonPropertyName("id")]
    public int? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("path_with_namespace")]
    public string? PathWithNamespace { get; set; }

    [JsonPropertyName("web_url")]
    public string? WebUrl { get; set; }

    [JsonIgnore]
    public string DisplayName => !string.IsNullOrWhiteSpace(PathWithNamespace)
        ? PathWithNamespace
        : (!string.IsNullOrWhiteSpace(Name) ? Name : "GitLab Project");
}

public sealed class GitLabObjectAttributes
{
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    [JsonPropertyName("iid")]
    public int? Iid { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("action")]
    public string? Action { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("source_branch")]
    public string? SourceBranch { get; set; }

    [JsonPropertyName("target_branch")]
    public string? TargetBranch { get; set; }

    [JsonPropertyName("note")]
    public string? Note { get; set; }

    [JsonPropertyName("noteable_type")]
    public string? NoteableType { get; set; }

    [JsonPropertyName("system")]
    public bool System { get; set; }
}

/// <summary>
/// Nested merge request object present on Note Hook events.
/// </summary>
public sealed class GitLabMergeRequest
{
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    [JsonPropertyName("iid")]
    public int? Iid { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("source_branch")]
    public string? SourceBranch { get; set; }

    [JsonPropertyName("target_branch")]
    public string? TargetBranch { get; set; }
}
