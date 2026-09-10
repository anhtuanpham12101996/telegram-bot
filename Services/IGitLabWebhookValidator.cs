namespace TelegramRelay.Services;

using Microsoft.AspNetCore.Http;

/// <summary>
/// Validates incoming GitLab webhook requests using the shared secret token.
/// </summary>
public interface IGitLabWebhookValidator
{
    /// <summary>
    /// Verifies the <c>X-Gitlab-Token</c> header (or <c>secret</c>/<c>key</c> query) against the configured secret.
    /// </summary>
    bool Validate(HttpRequest request);
}
