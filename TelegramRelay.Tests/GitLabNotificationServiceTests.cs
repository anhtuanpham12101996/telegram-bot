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

public class GitLabNotificationServiceTests
{
    private readonly Mock<ITelegramBotClient> _mockBotClient;
    private readonly GitLabNotificationService _service;

    public GitLabNotificationServiceTests()
    {
        _mockBotClient = new Mock<ITelegramBotClient>();
        _service = new GitLabNotificationService(
            _mockBotClient.Object,
            NullLogger<GitLabNotificationService>.Instance);
    }

    [Fact]
    public void FormatMessage_OpenedMergeRequest_IncludesBranchesAndLink()
    {
        var payload = CreateMergeRequestPayload("open");

        string message = _service.FormatMessage(payload, GitLabNotifyKind.MergeRequestOpened);

        Assert.Contains("Merge Request !12 opened", message);
        Assert.Contains("<b>acme/api</b>", message);
        int projectIndex = message.IndexOf("<b>acme/api</b>", StringComparison.Ordinal);
        int headlineIndex = message.IndexOf("Merge Request !12 opened", StringComparison.Ordinal);
        Assert.True(projectIndex >= 0 && headlineIndex > projectIndex);
        Assert.Contains("Fix user authentication", message);
        Assert.Contains("feature/login", message);
        Assert.Contains("main", message);
        Assert.Contains("Alex Rivera", message);
        Assert.Contains("https://gitlab.example.com/acme/api/-/merge_requests/12", message);
        Assert.DoesNotContain("<script>", message);
    }

    [Fact]
    public void FormatMessage_Comment_EscapesHtmlAndShowsAuthorQuote()
    {
        var payload = new GitLabWebhookPayload
        {
            ObjectKind = "note",
            User = new GitLabUser { Name = "Hacker <script>alert(1)</script>" },
            Project = new GitLabProject { PathWithNamespace = "acme/api" },
            ObjectAttributes = new GitLabObjectAttributes
            {
                Action = "create",
                NoteableType = "MergeRequest",
                Note = "Looks good & ready <now>",
                Url = "https://gitlab.example.com/acme/api/-/merge_requests/12#note_1"
            },
            MergeRequest = new GitLabMergeRequest { Iid = 12, Title = "Fix login" }
        };

        string message = _service.FormatMessage(payload, GitLabNotifyKind.MergeRequestCommented);

        Assert.Contains("New comment on !12", message);
        Assert.Contains("Hacker &lt;script&gt;alert(1)&lt;/script&gt;", message);
        Assert.Contains("Looks good &amp; ready &lt;now&gt;", message);
        Assert.DoesNotContain("<script>", message);
    }

    [Fact]
    public void FormatMessage_ApprovedAndMerged_UseExpectedHeadlines()
    {
        var payload = CreateMergeRequestPayload("approved");

        string approved = _service.FormatMessage(payload, GitLabNotifyKind.MergeRequestApproved);
        string merged = _service.FormatMessage(payload, GitLabNotifyKind.MergeRequestMerged);

        Assert.Contains("Merge Request !12 approved", approved);
        Assert.Contains("Merge Request !12 merged", merged);
    }

    [Fact]
    public void FormatMessage_PipelineFailed_IncludesRefAndLink()
    {
        var payload = new GitLabWebhookPayload
        {
            ObjectKind = "pipeline",
            User = new GitLabUser { Name = "Alex Rivera" },
            Project = new GitLabProject
            {
                PathWithNamespace = "HieuTN5/evn-genco3",
                WebUrl = "https://gitlab.com/HieuTN5/evn-genco3"
            },
            ObjectAttributes = new GitLabObjectAttributes
            {
                Id = 99,
                Status = "failed",
                Ref = "main",
                Sha = "bcbb5ec396a2c0f828686f14fac9b80b780504f2",
                Url = "https://gitlab.com/HieuTN5/evn-genco3/-/pipelines/99"
            }
        };

        string message = _service.FormatMessage(payload, GitLabNotifyKind.PipelineFailed);

        Assert.StartsWith("<b>HieuTN5/evn-genco3</b>", message.Replace("\r\n", "\n"));
        Assert.Contains("❌ Pipeline failed", message);
        Assert.Contains("main", message);
        Assert.Contains("https://gitlab.com/HieuTN5/evn-genco3/-/pipelines/99", message);
        Assert.Contains("bcbb5ec3", message);
    }

    [Fact]
    public void FormatMessage_JobSucceeded_IncludesJobName()
    {
        var payload = new GitLabWebhookPayload
        {
            ObjectKind = "build",
            User = new GitLabUser { Name = "Alex Rivera" },
            Project = new GitLabProject { PathWithNamespace = "HieuTN5/evn-genco3" },
            BuildName = "deploy-prod",
            BuildStage = "deploy",
            BuildStatus = "success",
            Ref = "main",
            Sha = "2293ada6b400935a1378653304eaf6221e0fdb8f"
        };

        string message = _service.FormatMessage(payload, GitLabNotifyKind.JobSucceeded);

        Assert.Contains("✅ Job succeeded", message);
        Assert.Contains("deploy-prod", message);
        Assert.Contains("deploy", message);
        Assert.Contains("2293ada6", message);
    }

    [Fact]
    public async Task SendNotificationAsync_DispatchesViaBotClientWithHtmlParseMode()
    {
        long chatId = -100123456789;
        var payload = CreateMergeRequestPayload("open");

        _mockBotClient
            .Setup(c => c.SendRequest(
                It.Is<SendMessageRequest>(r => r.ChatId == chatId && r.ParseMode == ParseMode.Html),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Message());

        bool result = await _service.SendNotificationAsync(chatId, payload, GitLabNotifyKind.MergeRequestOpened);

        Assert.True(result);
        _mockBotClient.Verify(c => c.SendRequest(
            It.Is<SendMessageRequest>(r => r.ChatId == chatId && r.Text.Contains("Fix user authentication") && r.ParseMode == ParseMode.Html),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static GitLabWebhookPayload CreateMergeRequestPayload(string action) => new()
    {
        ObjectKind = "merge_request",
        User = new GitLabUser { Name = "Alex Rivera" },
        Project = new GitLabProject
        {
            Id = 10,
            PathWithNamespace = "acme/api"
        },
        ObjectAttributes = new GitLabObjectAttributes
        {
            Iid = 12,
            Title = "Fix user authentication",
            Action = action,
            SourceBranch = "feature/login",
            TargetBranch = "main",
            Url = "https://gitlab.example.com/acme/api/-/merge_requests/12"
        }
    };
}
