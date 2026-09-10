namespace TelegramRelay.Services;

using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TelegramRelay.Configuration;

/// <summary>
/// Validates GitLab webhooks via constant-time comparison of <c>X-Gitlab-Token</c>.
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

    public bool Validate(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        string secret = _options.GitLabSecret;
        if (string.IsNullOrWhiteSpace(secret))
        {
            _logger.LogError("GitLab webhook validation failed: 'GitLabSecret' is not configured.");
            return false;
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
            _logger.LogWarning("GitLab webhook validation failed: Missing '{Header}'.", HeaderGitlabToken);
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

    private static bool SafeFixedTimeEquals(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(left, right);
    }
}
