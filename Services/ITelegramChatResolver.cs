namespace TelegramRelay.Services;

/// <summary>
/// Resolves the destination Telegram Chat ID for a given Taiga project.
/// </summary>
public interface ITelegramChatResolver
{
    /// <summary>
    /// Resolves the Telegram Chat ID for the specified Taiga project ID.
    /// Returns the mapped chat ID if present, otherwise the configured DefaultChatId, or null if unresolvable.
    /// </summary>
    /// <param name="projectId">The Taiga project identifier.</param>
    /// <returns>The target Telegram Chat ID, or null if no mapping or fallback exists.</returns>
    long? ResolveChatId(int projectId);

    /// <summary>
    /// Resolves the Telegram Chat ID for the specified GitLab project ID.
    /// Returns the mapped chat ID if present, otherwise the configured DefaultChatId, or null if unresolvable.
    /// </summary>
    long? ResolveGitLabChatId(int projectId);
}

