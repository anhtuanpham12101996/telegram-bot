namespace TelegramRelay.Tests;

using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TelegramRelay.Configuration;
using TelegramRelay.Services;
using Xunit;

public class TaigaWebhookValidatorTests
{
    private const string TestSecret = "my_super_secret_webhook_key_12345";
    private readonly RelayOptions _options;
    private readonly TaigaWebhookValidator _validator;

    public TaigaWebhookValidatorTests()
    {
        _options = new RelayOptions
        {
            TaigaSecret = TestSecret
        };

        _validator = new TaigaWebhookValidator(
            Options.Create(_options),
            NullLogger<TaigaWebhookValidator>.Instance);
    }

    private static string ComputeHmacSha1Hex(string secret, byte[] body)
    {
        byte[] keyBytes = Encoding.UTF8.GetBytes(secret);
        byte[] hash = HMACSHA1.HashData(keyBytes, body);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    [Fact]
    public void Validate_WithValidHmacInXTaigaKeyHeader_ReturnsTrue()
    {
        // Arrange
        byte[] rawBody = Encoding.UTF8.GetBytes("{\"action\": \"create\", \"type\": \"userstory\"}");
        string signature = ComputeHmacSha1Hex(TestSecret, rawBody);

        var context = new DefaultHttpContext();
        context.Request.Headers["X-TAIGA-KEY"] = signature;

        // Act
        bool isValid = _validator.Validate(context.Request, rawBody);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void Validate_WithValidHmacInXTaigaWebhookSignatureHeader_ReturnsTrue()
    {
        // Arrange
        byte[] rawBody = Encoding.UTF8.GetBytes("{\"action\": \"create\", \"type\": \"task\"}");
        string signature = ComputeHmacSha1Hex(TestSecret, rawBody);

        var context = new DefaultHttpContext();
        context.Request.Headers["X-TAIGA-WEBHOOK-SIGNATURE"] = signature;

        // Act
        bool isValid = _validator.Validate(context.Request, rawBody);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void Validate_WithDirectSecretInHeader_ReturnsTrue()
    {
        // Arrange
        byte[] rawBody = Encoding.UTF8.GetBytes("{\"action\": \"change\"}");

        var context = new DefaultHttpContext();
        context.Request.Headers["X-TAIGA-KEY"] = TestSecret;

        // Act
        bool isValid = _validator.Validate(context.Request, rawBody);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void Validate_WithValidQuerySecret_ReturnsTrue()
    {
        // Arrange
        byte[] rawBody = Encoding.UTF8.GetBytes("{\"action\": \"delete\"}");

        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString($"?secret={TestSecret}");

        // Act
        bool isValid = _validator.Validate(context.Request, rawBody);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void Validate_WithTamperedBody_ReturnsFalse()
    {
        // Arrange
        byte[] originalBody = Encoding.UTF8.GetBytes("{\"action\": \"create\"}");
        string signature = ComputeHmacSha1Hex(TestSecret, originalBody);

        byte[] tamperedBody = Encoding.UTF8.GetBytes("{\"action\": \"create\", \"malicious\": true}");

        var context = new DefaultHttpContext();
        context.Request.Headers["X-TAIGA-KEY"] = signature;

        // Act
        bool isValid = _validator.Validate(context.Request, tamperedBody);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void Validate_WithInvalidSignature_ReturnsFalse()
    {
        // Arrange
        byte[] rawBody = Encoding.UTF8.GetBytes("{\"action\": \"create\"}");

        var context = new DefaultHttpContext();
        context.Request.Headers["X-TAIGA-KEY"] = "deadbeef1234567890abcdef1234567890abcdef";

        // Act
        bool isValid = _validator.Validate(context.Request, rawBody);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void Validate_WithDifferentLengthSignature_ReturnsFalse()
    {
        byte[] rawBody = Encoding.UTF8.GetBytes("{\"action\": \"create\"}");

        var context = new DefaultHttpContext();
        context.Request.Headers["X-TAIGA-KEY"] = "short-key";

        bool isValid = _validator.Validate(context.Request, rawBody);

        Assert.False(isValid);
    }

    [Fact]
    public void Validate_WithMissingHeaderAndQuery_ReturnsFalse()
    {
        // Arrange
        byte[] rawBody = Encoding.UTF8.GetBytes("{\"action\": \"create\"}");
        var context = new DefaultHttpContext();

        // Act
        bool isValid = _validator.Validate(context.Request, rawBody);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void Validate_WhenTaigaSecretIsNotConfigured_ReturnsFalse()
    {
        // Arrange
        var options = new RelayOptions { TaigaSecret = "" };
        var validator = new TaigaWebhookValidator(
            Options.Create(options),
            NullLogger<TaigaWebhookValidator>.Instance);

        byte[] rawBody = Encoding.UTF8.GetBytes("{\"action\": \"create\"}");
        var context = new DefaultHttpContext();
        context.Request.Headers["X-TAIGA-KEY"] = "some-key";

        // Act
        bool isValid = validator.Validate(context.Request, rawBody);

        // Assert
        Assert.False(isValid);
    }
}

