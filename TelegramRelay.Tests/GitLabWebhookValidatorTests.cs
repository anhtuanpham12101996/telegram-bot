namespace TelegramRelay.Tests;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TelegramRelay.Configuration;
using TelegramRelay.Services;
using Xunit;

public class GitLabWebhookValidatorTests
{
    private const string TestSecret = "gitlab_webhook_secret_12345";
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

        Assert.True(_validator.Validate(context.Request));
    }

    [Fact]
    public void Validate_WithValidQuerySecret_ReturnsTrue()
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString($"?secret={TestSecret}");

        Assert.True(_validator.Validate(context.Request));
    }

    [Fact]
    public void Validate_WithWrongToken_ReturnsFalse()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Gitlab-Token"] = "wrong-token";

        Assert.False(_validator.Validate(context.Request));
    }

    [Fact]
    public void Validate_WithMissingToken_ReturnsFalse()
    {
        var context = new DefaultHttpContext();

        Assert.False(_validator.Validate(context.Request));
    }

    [Fact]
    public void Validate_WhenSecretIsNotConfigured_ReturnsFalse()
    {
        var validator = new GitLabWebhookValidator(
            Options.Create(new RelayOptions { GitLabSecret = "" }),
            NullLogger<GitLabWebhookValidator>.Instance);

        var context = new DefaultHttpContext();
        context.Request.Headers["X-Gitlab-Token"] = "anything";

        Assert.False(validator.Validate(context.Request));
    }
}
