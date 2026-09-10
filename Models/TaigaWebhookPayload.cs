namespace TelegramRelay.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Root payload received from a Taiga webhook event.
/// </summary>
public sealed class TaigaWebhookPayload
{
    [JsonPropertyName("action")]
    public string? Action { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("by")]
    public TaigaUser? By { get; set; }

    [JsonPropertyName("date")]
    public string? Date { get; set; }

    [JsonPropertyName("data")]
    public TaigaItemData? Data { get; set; }
}

/// <summary>
/// Information about the user who triggered the Taiga action.
/// </summary>
public sealed class TaigaUser
{
    [JsonPropertyName("id")]
    public int? Id { get; set; }

    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("full_name")]
    public string? FullName { get; set; }

    [JsonPropertyName("photo")]
    public string? Photo { get; set; }
}

/// <summary>
/// Details of the entity (userstory, task, issue, epic, etc.) affected by the event.
/// </summary>
public sealed class TaigaItemData
{
    [JsonPropertyName("id")]
    public int? Id { get; set; }

    [JsonPropertyName("ref")]
    public int? Ref { get; set; }

    [JsonPropertyName("subject")]
    public string? Subject { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("permalink")]
    public string? Permalink { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("project")]
    public TaigaProject? Project { get; set; }

    [JsonPropertyName("project_id")]
    public int? ProjectId { get; set; }

    /// <summary>
    /// Gets the resolved title from either 'subject' or 'name'.
    /// </summary>
    [JsonIgnore]
    public string Title => !string.IsNullOrWhiteSpace(Subject) 
        ? Subject 
        : (!string.IsNullOrWhiteSpace(Name) ? Name : "Untitled");

    /// <summary>
    /// Gets the resolved URL from either 'permalink' or 'url'.
    /// </summary>
    [JsonIgnore]
    public string? Link => !string.IsNullOrWhiteSpace(Permalink) 
        ? Permalink 
        : Url;

    /// <summary>
    /// Gets the resolved project ID from the nested project object or project_id property.
    /// </summary>
    [JsonIgnore]
    public int? ResolvedProjectId => Project?.Id ?? ProjectId;
}

/// <summary>
/// Context regarding the Taiga project that the item belongs to.
/// </summary>
public sealed class TaigaProject
{
    [JsonPropertyName("id")]
    public int? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("slug")]
    public string? Slug { get; set; }

    [JsonPropertyName("permalink")]
    public string? Permalink { get; set; }
}

