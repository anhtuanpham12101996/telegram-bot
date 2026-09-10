namespace TelegramRelay.Tests;

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
}
