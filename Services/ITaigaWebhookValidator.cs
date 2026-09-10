namespace TelegramRelay.Services;

using Microsoft.AspNetCore.Http;

/// <summary>
/// Service responsible for validating the authenticity of incoming Taiga webhook requests.
/// </summary>
public interface ITaigaWebhookValidator
{
    /// <summary>
    /// Validates the authenticity of an incoming Taiga webhook request.
    /// Verifies the HMAC-SHA1 signature provided in headers (X-TAIGA-KEY / X-TAIGA-WEBHOOK-SIGNATURE)
    /// or query parameter (secret / key) against the configured secret using constant-time comparison.
    /// </summary>
    /// <param name="request">The incoming HTTP request.</param>
    /// <param name="rawBody">The raw, unmodified body bytes.</param>
    /// <returns>True if the request is authentic; otherwise, false.</returns>
    bool Validate(HttpRequest request, ReadOnlySpan<byte> rawBody);
}

