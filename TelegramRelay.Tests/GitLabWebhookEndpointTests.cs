namespace TelegramRelay.Tests;

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TelegramRelay.Models;
using TelegramRelay.Services;
using Xunit;

public class GitLabWebhookEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string WebhookSecret = "YOUR_GITLAB_WEBHOOK_SECRET";
    private readonly WebApplicationFactory<Program> _factory;
    private readonly Mock<IGitLabNotificationService> _mockNotificationService;

    public GitLabWebhookEndpointTests(WebApplicationFactory<Program> factory)
    {
        _mockNotificationService = new Mock<IGitLabNotificationService>();
        _mockNotificationService
            .Setup(s => s.SendNotificationAsync(
                It.IsAny<long>(),
                It.IsAny<GitLabWebhookPayload>(),
                It.IsAny<GitLabNotifyKind>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("TelegramRelay:TelegramBotToken", "0000000000:AAPlaceholderTokenForTestingOnly");
            builder.UseSetting("TelegramRelay:GitLabSecret", WebhookSecret);
            builder.UseSetting("TelegramRelay:DefaultChatId", "-100123456789");
            builder.UseSetting("TelegramRelay:GitLabProjectChatMappings:10", "-100123456789");
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped(_ => _mockNotificationService.Object);
            });
        });
    }

    [Fact]
    public async Task PostWebhook_WithoutToken_Returns401Unauthorized()
    {
        var client = _factory.CreateClient();
        var content = new StringContent("{\"object_kind\":\"merge_request\"}", Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/webhooks/gitlab", content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostWebhook_WithInvalidToken_Returns401Unauthorized()
    {
        var client = _factory.CreateClient();
        var response = await SendWebhookAsync(client, """{"object_kind":"merge_request"}""", token: "wrong-token");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostWebhook_WhenMergeRequestOpened_Returns200Ok()
    {
        _mockNotificationService.Invocations.Clear();
        var client = _factory.CreateClient();
        string json = """
        {
          "object_kind": "merge_request",
          "user": { "name": "Alex Rivera" },
          "project": { "id": 10, "path_with_namespace": "acme/api" },
          "object_attributes": {
            "iid": 12,
            "title": "Fix login",
            "action": "open",
            "source_branch": "feature/login",
            "target_branch": "main",
            "url": "https://gitlab.example.com/acme/api/-/merge_requests/12"
          }
        }
        """;

        var response = await SendWebhookAsync(client, json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("success", await response.Content.ReadAsStringAsync());
        _mockNotificationService.Verify(s => s.SendNotificationAsync(
            -100123456789,
            It.IsAny<GitLabWebhookPayload>(),
            GitLabNotifyKind.MergeRequestOpened,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PostWebhook_WhenMergeRequestCommented_Returns200Ok()
    {
        _mockNotificationService.Invocations.Clear();
        var client = _factory.CreateClient();
        string json = """
        {
          "object_kind": "note",
          "user": { "name": "Alex Rivera" },
          "project": { "id": 10, "path_with_namespace": "acme/api" },
          "object_attributes": {
            "note": "Please add tests",
            "noteable_type": "MergeRequest",
            "action": "create",
            "url": "https://gitlab.example.com/acme/api/-/merge_requests/12#note_9"
          },
          "merge_request": { "iid": 12, "title": "Fix login" }
        }
        """;

        var response = await SendWebhookAsync(client, json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _mockNotificationService.Verify(s => s.SendNotificationAsync(
            It.IsAny<long>(),
            It.IsAny<GitLabWebhookPayload>(),
            GitLabNotifyKind.MergeRequestCommented,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("approved", "MergeRequestApproved")]
    [InlineData("merge", "MergeRequestMerged")]
    public async Task PostWebhook_WhenApprovedOrMerged_Returns200Ok(string action, string expectedKind)
    {
        _mockNotificationService.Invocations.Clear();
        var client = _factory.CreateClient();
        string json = $$"""
        {
          "object_kind": "merge_request",
          "user": { "name": "Alex Rivera" },
          "project": { "id": 10, "path_with_namespace": "acme/api" },
          "object_attributes": {
            "iid": 12,
            "title": "Fix login",
            "action": "{{action}}"
          }
        }
        """;

        var response = await SendWebhookAsync(client, json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        GitLabNotifyKind kind = Enum.Parse<GitLabNotifyKind>(expectedKind);
        _mockNotificationService.Verify(s => s.SendNotificationAsync(
            It.IsAny<long>(),
            It.IsAny<GitLabWebhookPayload>(),
            kind,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PostWebhook_WhenMergeRequestUpdated_ReturnsIgnored()
    {
        _mockNotificationService.Invocations.Clear();
        var client = _factory.CreateClient();
        string json = """
        {
          "object_kind": "merge_request",
          "project": { "id": 10 },
          "object_attributes": { "iid": 12, "title": "Fix login", "action": "update" }
        }
        """;

        var response = await SendWebhookAsync(client, json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("ignored", await response.Content.ReadAsStringAsync());
        _mockNotificationService.Verify(s => s.SendNotificationAsync(
            It.IsAny<long>(),
            It.IsAny<GitLabWebhookPayload>(),
            It.IsAny<GitLabNotifyKind>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PostWebhook_WithInvalidJson_Returns400BadRequest()
    {
        var client = _factory.CreateClient();
        var response = await SendWebhookAsync(client, "not-a-valid-json-string");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> SendWebhookAsync(
        HttpClient client,
        string json,
        string? token = WebhookSecret)
    {
        byte[] bodyBytes = Encoding.UTF8.GetBytes(json);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/gitlab")
        {
            Content = new ByteArrayContent(bodyBytes)
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        if (token is not null)
        {
            request.Headers.Add("X-Gitlab-Token", token);
        }

        return await client.SendAsync(request);
    }
}
