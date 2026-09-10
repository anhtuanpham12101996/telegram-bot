namespace TelegramRelay.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TelegramRelay.Configuration;
using TelegramRelay.Services;
using Xunit;

public class TelegramChatResolverTests
{
    [Fact]
    public void ResolveChatId_WhenProjectIsExplicitlyMapped_ReturnsMappedChatId()
    {
        // Arrange
        var options = new RelayOptions
        {
            DefaultChatId = -100111111111,
            ProjectChatMappings = new Dictionary<string, long>
            {
                ["101"] = -100123456789,
                ["102"] = -100987654321
            }
        };

        var resolver = new TelegramChatResolver(
            Options.Create(options),
            NullLogger<TelegramChatResolver>.Instance);

        // Act
        long? chatId = resolver.ResolveChatId(101);

        // Assert
        Assert.Equal(-100123456789, chatId);
    }

    [Fact]
    public void ResolveChatId_WhenProjectIsNotMapped_FallsBackToDefaultChatId()
    {
        // Arrange
        var options = new RelayOptions
        {
            DefaultChatId = -100111111111,
            ProjectChatMappings = new Dictionary<string, long>
            {
                ["101"] = -100123456789
            }
        };

        var resolver = new TelegramChatResolver(
            Options.Create(options),
            NullLogger<TelegramChatResolver>.Instance);

        // Act
        long? chatId = resolver.ResolveChatId(999);

        // Assert
        Assert.Equal(-100111111111, chatId);
    }

    [Fact]
    public void ResolveChatId_WhenNotMappedAndNoDefaultChatId_ReturnsNull()
    {
        // Arrange
        var options = new RelayOptions
        {
            DefaultChatId = null,
            ProjectChatMappings = new Dictionary<string, long>
            {
                ["101"] = -100123456789
            }
        };

        var resolver = new TelegramChatResolver(
            Options.Create(options),
            NullLogger<TelegramChatResolver>.Instance);

        // Act
        long? chatId = resolver.ResolveChatId(999);

        // Assert
        Assert.Null(chatId);
    }

    [Fact]
    public void ResolveGitLabChatId_WhenProjectIsExplicitlyMapped_ReturnsMappedChatId()
    {
        var options = new RelayOptions
        {
            DefaultChatId = -100111111111,
            GitLabProjectChatMappings = new Dictionary<string, long>
            {
                ["10"] = -100123456789
            }
        };

        var resolver = new TelegramChatResolver(
            Options.Create(options),
            NullLogger<TelegramChatResolver>.Instance);

        Assert.Equal(-100123456789, resolver.ResolveGitLabChatId(10));
    }

    [Fact]
    public void ResolveGitLabChatId_WhenProjectIsNotMapped_FallsBackToDefaultChatId()
    {
        var options = new RelayOptions
        {
            DefaultChatId = -100111111111,
            GitLabProjectChatMappings = new Dictionary<string, long>()
        };

        var resolver = new TelegramChatResolver(
            Options.Create(options),
            NullLogger<TelegramChatResolver>.Instance);

        Assert.Equal(-100111111111, resolver.ResolveGitLabChatId(99));
    }
}

