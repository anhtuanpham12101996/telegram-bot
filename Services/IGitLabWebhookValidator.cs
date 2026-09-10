namespace TelegramRelay.Services;

using Microsoft.AspNetCore.Http;

/// <summary>
/// Validates incoming GitLab webhook requests.
/// </summary>
public interface IGitLabWebhookValidator
{
    /// <summary>
    /// Verifies the shared Secret token in <c>X-Gitlab-Token</c> (or <c>secret</c>/<c>key</c> query).
    /// </summary>
    bool Validate(HttpRequest request, ReadOnlySpan<byte> rawBody);
}
