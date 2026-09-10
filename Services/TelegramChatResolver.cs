namespace TelegramRelay.Services;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TelegramRelay.Configuration;

/// <summary>
/// Default implementation of <see cref="ITelegramChatResolver"/> that resolves chat IDs
/// based on configured project mappings with fallback to the default chat ID.
/// </summary>
public sealed class TelegramChatResolver(
    IOptions<RelayOptions> options,
    ILogger<TelegramChatResolver> logger) : ITelegramChatResolver
{
    private readonly RelayOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    private readonly ILogger<TelegramChatResolver> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public long? ResolveChatId(int projectId)
    {
        string projectKey = projectId.ToString();

        // 1. Check for specific project mapping
        if (_options.ProjectChatMappings != null &&
            _options.ProjectChatMappings.TryGetValue(projectKey, out long specificChatId))
        {
            _logger.LogInformation("Resolved chat ID {ChatId} for Taiga project {ProjectId} via explicit mapping.", specificChatId, projectId);
            return specificChatId;
        }

        // 2. Fall back to configured DefaultChatId
        if (_options.DefaultChatId.HasValue)
        {
            _logger.LogInformation("Using fallback DefaultChatId {DefaultChatId} for Taiga project {ProjectId}.", _options.DefaultChatId.Value, projectId);
            return _options.DefaultChatId.Value;
        }

        // 3. No mapping or fallback found
        _logger.LogWarning("No Telegram Chat ID mapping or DefaultChatId configured for Taiga project {ProjectId}.", projectId);
        return null;
    }

    public long? ResolveGitLabChatId(int projectId)
    {
        string projectKey = projectId.ToString();

        if (_options.GitLabProjectChatMappings != null &&
            _options.GitLabProjectChatMappings.TryGetValue(projectKey, out long specificChatId))
        {
            _logger.LogInformation("Resolved chat ID {ChatId} for GitLab project {ProjectId} via explicit mapping.", specificChatId, projectId);
            return specificChatId;
        }

        if (_options.DefaultChatId.HasValue)
        {
            _logger.LogInformation("Using fallback DefaultChatId {DefaultChatId} for GitLab project {ProjectId}.", _options.DefaultChatId.Value, projectId);
            return _options.DefaultChatId.Value;
        }

        _logger.LogWarning("No Telegram Chat ID mapping or DefaultChatId configured for GitLab project {ProjectId}.", projectId);
        return null;
    }
}

