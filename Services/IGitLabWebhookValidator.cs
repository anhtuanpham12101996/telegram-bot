namespace TelegramRelay.Services;

using Microsoft.AspNetCore.Http;

/// <summary>
/// Validates incoming GitLab webhook requests.
/// </summary>
public interface IGitLabWebhookValidator
{
    /// <summary>
    /// Accepts Standard Webhooks HMAC (<c>webhook-signature</c>),
    /// the legacy <c>X-Gitlab-Token</c> header, or a <c>secret</c>/<c>key</c> query parameter.
    /// </summary>
    bool Validate(HttpRequest request, ReadOnlySpan<byte> rawBody);
}
