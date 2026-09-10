namespace TelegramRelay.Services;

using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TelegramRelay.Configuration;

/// <summary>
/// Validates incoming Taiga webhook requests using HMAC-SHA1 signature verification
/// and constant-time string comparison to prevent timing attacks.
/// </summary>
public sealed class TaigaWebhookValidator(
    IOptions<RelayOptions> options,
    ILogger<TaigaWebhookValidator> logger) : ITaigaWebhookValidator
{
    private const string HeaderTaigaKey = "X-TAIGA-KEY";
    private const string HeaderTaigaSignature = "X-TAIGA-WEBHOOK-SIGNATURE";
    private const string QuerySecretKey = "secret";
    private const string QueryKey = "key";

    private readonly RelayOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    private readonly ILogger<TaigaWebhookValidator> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public bool Validate(HttpRequest request, ReadOnlySpan<byte> rawBody)
    {
        ArgumentNullException.ThrowIfNull(request);

        string secret = _options.TaigaSecret;
        if (string.IsNullOrWhiteSpace(secret))
        {
            _logger.LogError("Webhook validation failed: 'TaigaSecret' is not configured in application settings.");
            return false;
        }

        byte[] secretBytes = Encoding.UTF8.GetBytes(secret);

        // 1. Check query parameter secret (e.g. ?secret=... or ?key=...)
        if (request.Query.TryGetValue(QuerySecretKey, out var querySecretValues) ||
            request.Query.TryGetValue(QueryKey, out querySecretValues))
        {
            string? querySecret = querySecretValues.FirstOrDefault();
            if (!string.IsNullOrEmpty(querySecret))
            {
                byte[] querySecretBytes = Encoding.UTF8.GetBytes(querySecret);
                if (SafeFixedTimeEquals(querySecretBytes, secretBytes))
                {
                    _logger.LogDebug("Taiga webhook request validated successfully via query parameter.");
                    return true;
                }
            }
        }

        // 2. Check HMAC signature headers (X-TAIGA-KEY or X-TAIGA-WEBHOOK-SIGNATURE)
        string? signatureHeader = null;
        if (request.Headers.TryGetValue(HeaderTaigaKey, out var taigaKeyValues))
        {
            signatureHeader = taigaKeyValues.FirstOrDefault();
        }
        else if (request.Headers.TryGetValue(HeaderTaigaSignature, out var taigaSigValues))
        {
            signatureHeader = taigaSigValues.FirstOrDefault();
        }

        if (string.IsNullOrWhiteSpace(signatureHeader))
        {
            _logger.LogWarning("Webhook validation failed: Missing '{HeaderKey}', '{HeaderSig}', or query secret.",
                HeaderTaigaKey, HeaderTaigaSignature);
            return false;
        }

        string trimmedHeader = signatureHeader.Trim();

        // 2a. Check if header directly matches the secret (plain secret header fallback)
        byte[] headerBytes = Encoding.UTF8.GetBytes(trimmedHeader);
        if (SafeFixedTimeEquals(headerBytes, secretBytes))
        {
            _logger.LogDebug("Taiga webhook request validated successfully via direct secret header match.");
            return true;
        }

        // 2b. Compute HMAC-SHA1 over the raw payload and compare with header
        Span<byte> computedHmac = stackalloc byte[20]; // SHA-1 is 160 bits (20 bytes)
        HMACSHA1.HashData(secretBytes, rawBody, computedHmac);

        // Header signature is hex-encoded (40 hex characters)
        string computedHexLower = Convert.ToHexString(computedHmac).ToLowerInvariant();
        byte[] computedBytes = Encoding.UTF8.GetBytes(computedHexLower);
        byte[] headerBytesLower = Encoding.UTF8.GetBytes(trimmedHeader.ToLowerInvariant());

        if (SafeFixedTimeEquals(computedBytes, headerBytesLower))
        {
            _logger.LogDebug("Taiga webhook request validated successfully via HMAC-SHA1 signature.");
            return true;
        }

        _logger.LogWarning("Webhook validation failed: HMAC-SHA1 signature mismatch.");
        return false;
    }

    /// <summary>
    /// Constant-time compare that returns false when lengths differ, instead of throwing.
    /// </summary>
    private static bool SafeFixedTimeEquals(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(left, right);
    }
}
