namespace TelegramRelay.Configuration;

/// <summary>
/// Strongly-typed configuration for the Telegram webhook relay (Taiga, GitLab, and shared bot settings).
/// Maps to the "TelegramRelay" configuration section.
/// </summary>
public sealed class RelayOptions
{
    public const string SectionName = "TelegramRelay";

    /// <summary>
    /// The Telegram Bot API token obtained from @BotFather.
    /// </summary>
    public string TelegramBotToken { get; set; } = string.Empty;

    /// <summary>
    /// The secret key configured in Taiga webhook settings used for HMAC-SHA1 signature verification.
    /// </summary>
    public string TaigaSecret { get; set; } = string.Empty;

    /// <summary>
    /// Shared GitLab webhook Secret token, sent as <c>X-Gitlab-Token</c>. Reuse this value on every project webhook.
    /// </summary>
    public string GitLabSecret { get; set; } = string.Empty;

    /// <summary>
    /// Fallback Telegram Chat ID used when no specific project mapping matches.
    /// </summary>
    public long? DefaultChatId { get; set; }

    /// <summary>
    /// Map of Taiga Project ID (as string key) to target Telegram Chat ID.
    /// Example: { "101": -100123456789, "102": -100987654321 }
    /// </summary>
    public Dictionary<string, long> ProjectChatMappings { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Map of GitLab Project ID (as string key) to target Telegram Chat ID.
    /// Example: { "101": -100123456789 }
    /// </summary>
    public Dictionary<string, long> GitLabProjectChatMappings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
