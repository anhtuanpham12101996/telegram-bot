namespace TelegramRelay.Services;

using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TelegramRelay.Configuration;

/// <summary>
/// Validates GitLab webhooks via Standard Webhooks HMAC-SHA256 and legacy secret token.
/// </summary>
public sealed class GitLabWebhookValidator(
    IOptions<RelayOptions> options,
    ILogger<GitLabWebhookValidator> logger) : IGitLabWebhookValidator
{
    private const string HeaderGitlabToken = "X-Gitlab-Token";
    private const string HeaderWebhookSignature = "webhook-signature";
    private const string HeaderWebhookId = "webhook-id";
    private const string HeaderWebhookTimestamp = "webhook-timestamp";
    private const string QuerySecretKey = "secret";
    private const string QueryKey = "key";
    private const string SigningTokenPrefix = "whsec_";
    private static readonly TimeSpan TimestampTolerance = TimeSpan.FromMinutes(10);

    private readonly RelayOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    private readonly ILogger<GitLabWebhookValidator> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public bool Validate(HttpRequest request, ReadOnlySpan<byte> rawBody)
    {
        ArgumentNullException.ThrowIfNull(request);

        string secret = RelaySecrets.Normalize(_options.GitLabSecret);
        if (!RelaySecrets.IsConfigured(secret))
        {
            _logger.LogError("GitLab webhook validation failed: 'TelegramRelay__GitLabSecret' is not configured (or still a YOUR_ placeholder).");
            return false;
        }

        if (TryValidateSigningToken(request, rawBody, secret))
        {
            return true;
        }

        byte[] secretBytes = Encoding.UTF8.GetBytes(secret);

        if (request.Query.TryGetValue(QuerySecretKey, out var querySecretValues) ||
            request.Query.TryGetValue(QueryKey, out querySecretValues))
        {
            string? querySecret = querySecretValues.FirstOrDefault();
            if (!string.IsNullOrEmpty(querySecret) &&
                SafeFixedTimeEquals(Encoding.UTF8.GetBytes(querySecret), secretBytes))
            {
                _logger.LogDebug("GitLab webhook request validated successfully via query parameter.");
                return true;
            }
        }

        if (!request.Headers.TryGetValue(HeaderGitlabToken, out var tokenValues))
        {
            _logger.LogWarning(
                "GitLab webhook validation failed: missing '{TokenHeader}' and HMAC '{SignatureHeader}' did not match.",
                HeaderGitlabToken, HeaderWebhookSignature);
            return false;
        }

        string? token = tokenValues.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("GitLab webhook validation failed: Empty '{Header}'.", HeaderGitlabToken);
            return false;
        }

        if (SafeFixedTimeEquals(Encoding.UTF8.GetBytes(token.Trim()), secretBytes))
        {
            _logger.LogDebug("GitLab webhook request validated successfully via {Header}.", HeaderGitlabToken);
            return true;
        }

        _logger.LogWarning("GitLab webhook validation failed: token mismatch.");
        return false;
    }

    private bool TryValidateSigningToken(HttpRequest request, ReadOnlySpan<byte> rawBody, string signingToken)
    {
        if (!request.Headers.TryGetValue(HeaderWebhookSignature, out var signatureValues))
        {
            return false;
        }

        string? signatures = signatureValues.FirstOrDefault();
        string? messageId = request.Headers[HeaderWebhookId].FirstOrDefault();
        string? timestamp = request.Headers[HeaderWebhookTimestamp].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(signatures) ||
            string.IsNullOrWhiteSpace(messageId) ||
            string.IsNullOrWhiteSpace(timestamp))
        {
            return false;
        }

        if (!IsTimestampFresh(timestamp))
        {
            _logger.LogWarning("GitLab webhook-timestamp '{Timestamp}' is outside the allowed window.", timestamp);
            return false;
        }

        byte[] key;
        try
        {
            key = DecodeSigningKey(signingToken);
        }
        catch (FormatException ex)
        {
            _logger.LogError(ex, "GitLab signing token is not a valid whsec_ / base64 key.");
            return false;
        }

        byte[] prefix = Encoding.UTF8.GetBytes($"{messageId}.{timestamp}.");
        byte[] message = new byte[prefix.Length + rawBody.Length];
        prefix.CopyTo(message, 0);
        rawBody.CopyTo(message.AsSpan(prefix.Length));

        byte[] digest = HMACSHA256.HashData(key, message);
        string expected = "v1," + Convert.ToBase64String(digest);

        foreach (string candidate in signatures.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (SafeFixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(candidate)))
            {
                _logger.LogDebug("GitLab webhook request validated successfully via {Header}.", HeaderWebhookSignature);
                return true;
            }
        }

        _logger.LogWarning("GitLab webhook validation failed: HMAC signature mismatch.");
        return false;
    }

    private static bool IsTimestampFresh(string timestamp)
    {
        if (!long.TryParse(timestamp, out long unixSeconds))
        {
            return false;
        }

        DateTimeOffset sentAt = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        return Math.Abs((DateTimeOffset.UtcNow - sentAt).TotalSeconds) <= TimestampTolerance.TotalSeconds;
    }

    private static byte[] DecodeSigningKey(string signingToken)
    {
        string token = signingToken.Trim();
        if (token.StartsWith(SigningTokenPrefix, StringComparison.Ordinal))
        {
            token = token[SigningTokenPrefix.Length..];
        }

        return Convert.FromBase64String(token);
    }

    private static bool SafeFixedTimeEquals(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(left, right);
    }
}
