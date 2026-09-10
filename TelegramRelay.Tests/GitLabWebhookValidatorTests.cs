namespace TelegramRelay.Tests;

using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TelegramRelay.Configuration;
using TelegramRelay.Services;
using Xunit;

public class GitLabWebhookValidatorTests
{
    private const string TestSecret = "gitlab_webhook_secret_12345";
    private static readonly byte[] EmptyBody = Encoding.UTF8.GetBytes("{}");
    private readonly GitLabWebhookValidator _validator;

    public GitLabWebhookValidatorTests()
    {
        _validator = new GitLabWebhookValidator(
            Options.Create(new RelayOptions { GitLabSecret = TestSecret }),
            NullLogger<GitLabWebhookValidator>.Instance);
    }

    [Fact]
    public void Validate_WithMatchingTokenHeader_ReturnsTrue()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Gitlab-Token"] = TestSecret;

        Assert.True(_validator.Validate(context.Request, EmptyBody));
    }

    [Fact]
    public void Validate_WithValidQuerySecret_ReturnsTrue()
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString($"?secret={TestSecret}");

        Assert.True(_validator.Validate(context.Request, EmptyBody));
    }

    [Fact]
    public void Validate_WithWrongToken_ReturnsFalse()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Gitlab-Token"] = "wrong-token";

        Assert.False(_validator.Validate(context.Request, EmptyBody));
    }

    [Fact]
    public void Validate_WithMissingToken_ReturnsFalse()
    {
        var context = new DefaultHttpContext();

        Assert.False(_validator.Validate(context.Request, EmptyBody));
    }

    [Fact]
    public void Validate_WhenSecretIsNotConfigured_ReturnsFalse()
    {
        var validator = new GitLabWebhookValidator(
            Options.Create(new RelayOptions { GitLabSecret = "" }),
            NullLogger<GitLabWebhookValidator>.Instance);

        var context = new DefaultHttpContext();
        context.Request.Headers["X-Gitlab-Token"] = "anything";

        Assert.False(validator.Validate(context.Request, EmptyBody));
    }

    [Fact]
    public void Validate_WhenSecretIsPlaceholder_ReturnsFalse()
    {
        var validator = new GitLabWebhookValidator(
            Options.Create(new RelayOptions { GitLabSecret = "YOUR_GITLAB_WEBHOOK_SECRET" }),
            NullLogger<GitLabWebhookValidator>.Instance);

        var context = new DefaultHttpContext();
        context.Request.Headers["X-Gitlab-Token"] = "YOUR_GITLAB_WEBHOOK_SECRET";

        Assert.False(validator.Validate(context.Request, EmptyBody));
    }

    [Fact]
    public void Validate_WithMatchingTokenAndWhitespace_ReturnsTrue()
    {
        var validator = new GitLabWebhookValidator(
            Options.Create(new RelayOptions { GitLabSecret = $"  {TestSecret}  " }),
            NullLogger<GitLabWebhookValidator>.Instance);

        var context = new DefaultHttpContext();
        context.Request.Headers["X-Gitlab-Token"] = TestSecret;

        Assert.True(validator.Validate(context.Request, EmptyBody));
    }

    [Fact]
    public void Validate_WithStandardWebhookSignature_ReturnsTrue()
    {
        byte[] rawKey = Encoding.UTF8.GetBytes("gitlab-signing-key-32-bytes!!");
        string signingToken = "whsec_" + Convert.ToBase64String(rawKey);
        byte[] body = Encoding.UTF8.GetBytes("""{"object_kind":"merge_request","object_attributes":{"action":"open"}}""");
        string messageId = "56a51d98-b54a-45c2-8cb2-f39a421ffd98";
        string timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

        var validator = new GitLabWebhookValidator(
            Options.Create(new RelayOptions { GitLabSecret = signingToken }),
            NullLogger<GitLabWebhookValidator>.Instance);

        var context = new DefaultHttpContext();
        context.Request.Headers["webhook-id"] = messageId;
        context.Request.Headers["webhook-timestamp"] = timestamp;
        context.Request.Headers["webhook-signature"] = ComputeStandardWebhookSignature(rawKey, messageId, timestamp, body);

        Assert.True(validator.Validate(context.Request, body));
    }

    [Fact]
    public void Validate_WithWrongStandardWebhookSignature_ReturnsFalse()
    {
        byte[] rawKey = Encoding.UTF8.GetBytes("gitlab-signing-key-32-bytes!!");
        string signingToken = "whsec_" + Convert.ToBase64String(rawKey);
        byte[] body = Encoding.UTF8.GetBytes("""{"object_kind":"merge_request"}""");

        var validator = new GitLabWebhookValidator(
            Options.Create(new RelayOptions { GitLabSecret = signingToken }),
            NullLogger<GitLabWebhookValidator>.Instance);

        var context = new DefaultHttpContext();
        context.Request.Headers["webhook-id"] = "56a51d98-b54a-45c2-8cb2-f39a421ffd98";
        context.Request.Headers["webhook-timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        context.Request.Headers["webhook-signature"] = "v1,d9mR7RjATJ/VOzQfaTq/XVTSK9NwJmLefSPwMwblfMM=";

        Assert.False(validator.Validate(context.Request, body));
    }

    private static string ComputeStandardWebhookSignature(byte[] rawKey, string messageId, string timestamp, byte[] body)
    {
        byte[] prefix = Encoding.UTF8.GetBytes($"{messageId}.{timestamp}.");
        byte[] message = new byte[prefix.Length + body.Length];
        prefix.CopyTo(message, 0);
        body.CopyTo(message, prefix.Length);
        byte[] digest = HMACSHA256.HashData(rawKey, message);
        return "v1," + Convert.ToBase64String(digest);
    }
}
