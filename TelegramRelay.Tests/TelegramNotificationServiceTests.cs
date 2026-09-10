namespace TelegramRelay.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramRelay.Models;
using TelegramRelay.Services;
using Xunit;

public class TelegramNotificationServiceTests
{
    private readonly Mock<ITelegramBotClient> _mockBotClient;
    private readonly TelegramNotificationService _service;

    public TelegramNotificationServiceTests()
    {
        _mockBotClient = new Mock<ITelegramBotClient>();
        _service = new TelegramNotificationService(
            _mockBotClient.Object,
            NullLogger<TelegramNotificationService>.Instance);
    }

    [Fact]
    public void FormatMessage_MatchesSpecification()
    {
        // Arrange
        var payload = new TaigaWebhookPayload
        {
            Action = "create",
            Type = "userstory",
            By = new TaigaUser
            {
                FullName = "Alex Rivera"
            },
            Data = new TaigaItemData
            {
                Id = 42,
                Subject = "Fix user authentication flow",
                Permalink = "https://taiga.io/project/sample/us/42",
                Project = new TaigaProject
                {
                    Id = 101,
                    Name = "Sample Project"
                }
            }
        };

        // Act
        string message = _service.FormatMessage(payload);

        // Assert
        string expected =
            "🚀 <b>[Sample Project]</b> New <code>USERSTORY</code> action: <b>create</b>\n" +
            "📌 <b>Title:</b> Fix user authentication flow\n" +
            "👤 <b>By:</b> Alex Rivera\n" +
            "🔗 <a href=\"https://taiga.io/project/sample/us/42\">View in Taiga</a>";

        // Normalize newlines for cross-platform comparison
        Assert.Equal(expected.Replace("\r\n", "\n"), message.Replace("\r\n", "\n"));
    }

    [Fact]
    public void FormatMessage_SafelyEscapesHtmlEntities()
    {
        // Arrange
        var payload = new TaigaWebhookPayload
        {
            Action = "test & run <now>",
            Type = "issue",
            By = new TaigaUser
            {
                FullName = "Hacker <script>alert(1)</script>"
            },
            Data = new TaigaItemData
            {
                Subject = "Error: A < B & C > D",
                Permalink = "https://taiga.io/project/sample/issue/1",
                Project = new TaigaProject
                {
                    Name = "Dev & Ops <Team>"
                }
            }
        };

        // Act
        string message = _service.FormatMessage(payload);

        // Assert
        Assert.DoesNotContain("<script>", message);
        Assert.Contains("Hacker &lt;script&gt;alert(1)&lt;/script&gt;", message);
        Assert.Contains("Dev &amp; Ops &lt;Team&gt;", message);
        Assert.Contains("Error: A &lt; B &amp; C &gt; D", message);
    }

    [Fact]
    public void FormatMessage_FallsBackToNameWhenSubjectIsNull()
    {
        // Arrange
        var payload = new TaigaWebhookPayload
        {
            Action = "create",
            Type = "epic",
            By = new TaigaUser { FullName = "Jane Doe" },
            Data = new TaigaItemData
            {
                Name = "Core Architecture Redesign",
                Project = new TaigaProject { Name = "Backend" }
            }
        };

        // Act
        string message = _service.FormatMessage(payload);

        // Assert
        Assert.Contains("📌 <b>Title:</b> Core Architecture Redesign", message);
    }

    [Fact]
    public async Task SendNotificationAsync_DispatchesViaBotClientWithHtmlParseMode()
    {
        // Arrange
        long chatId = -100123456789;
        var payload = new TaigaWebhookPayload
        {
            Action = "create",
            Type = "task",
            By = new TaigaUser { FullName = "Alex Rivera" },
            Data = new TaigaItemData
            {
                Subject = "Implement unit tests",
                Project = new TaigaProject { Name = "My Project" }
            }
        };

        _mockBotClient
            .Setup(c => c.SendRequest(
                It.Is<SendMessageRequest>(r => r.ChatId == chatId && r.ParseMode == ParseMode.Html),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Message());

        // Act
        bool result = await _service.SendNotificationAsync(chatId, payload);

        // Assert
        Assert.True(result);
        _mockBotClient.Verify(c => c.SendRequest(
            It.Is<SendMessageRequest>(r => r.ChatId == chatId && r.Text.Contains("Implement unit tests") && r.ParseMode == ParseMode.Html),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendNotificationAsync_WhenBotClientThrows_ReturnsFalse()
    {
        long chatId = -100123456789;
        var payload = new TaigaWebhookPayload
        {
            Action = "create",
            Type = "task",
            Data = new TaigaItemData { Subject = "Will fail" }
        };

        _mockBotClient
            .Setup(c => c.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Telegram API unavailable"));

        bool result = await _service.SendNotificationAsync(chatId, payload);

        Assert.False(result);
    }
}

