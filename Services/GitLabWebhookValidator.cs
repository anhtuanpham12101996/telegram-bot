namespace TelegramRelay.Services;

using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TelegramRelay.Configuration;

/// <summary>
/// Validates GitLab webhooks via shared Secret token (<c>X-Gitlab-Token</c>).
/// </summary>
public sealed class GitLabWebhookValidator(
    IOptions<RelayOptions> options,
    ILogger<GitLabWebhookValidator> logger) : IGitLabWebhookValidator
{
    private const string HeaderGitlabToken = "X-Gitlab-Token";
    private const string QuerySecretKey = "secret";
    private const string QueryKey = "key";

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

        byte[] secretBytes = Encoding.UTF8.GetBytes(secret);

        if (request.Headers.TryGetValue(HeaderGitlabToken, out var tokenValues))
        {
            string? token = tokenValues.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(token) &&
                SafeFixedTimeEquals(Encoding.UTF8.GetBytes(token.Trim()), secretBytes))
            {
                _logger.LogDebug("GitLab webhook request validated successfully via {Header}.", HeaderGitlabToken);
                return true;
            }
        }

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

        _logger.LogWarning("GitLab webhook validation failed: missing or mismatched '{Header}'.", HeaderGitlabToken);
        return false;
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
