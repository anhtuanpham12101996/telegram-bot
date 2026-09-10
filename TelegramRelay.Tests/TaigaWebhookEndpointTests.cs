namespace TelegramRelay.Tests;

using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TelegramRelay.Models;
using TelegramRelay.Services;
using Xunit;

public class TaigaWebhookEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string WebhookSecret = "YOUR_TAIGA_WEBHOOK_SECRET";
    private readonly WebApplicationFactory<Program> _factory;
    private readonly Mock<ITelegramNotificationService> _mockNotificationService;

    public TaigaWebhookEndpointTests(WebApplicationFactory<Program> factory)
    {
        _mockNotificationService = new Mock<ITelegramNotificationService>();
        _mockNotificationService
            .Setup(s => s.SendNotificationAsync(It.IsAny<long>(), It.IsAny<TaigaWebhookPayload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("TelegramRelay:TelegramBotToken", "0000000000:AAPlaceholderTokenForTestingOnly");
            builder.UseSetting("TelegramRelay:TaigaSecret", WebhookSecret);
            builder.UseSetting("TelegramRelay:DefaultChatId", "-100123456789");
            builder.UseSetting("TelegramRelay:ProjectChatMappings:101", "-100123456789");
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped(_ => _mockNotificationService.Object);
            });
        });
    }

    private static string ComputeHmacSha1Hex(string secret, byte[] body)
    {
        byte[] keyBytes = Encoding.UTF8.GetBytes(secret);
        byte[] hash = HMACSHA1.HashData(keyBytes, body);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    [Fact]
    public async Task PostWebhook_WithoutSignature_Returns401Unauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();
        var content = new StringContent("{\"action\": \"create\"}", Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/api/webhooks/taiga", content);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostWebhook_WithInvalidSignature_Returns401Unauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();
        byte[] bodyBytes = Encoding.UTF8.GetBytes("{\"action\": \"create\"}");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/taiga")
        {
            Content = new ByteArrayContent(bodyBytes)
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        request.Headers.Add("X-TAIGA-KEY", "invalid_signature_hex_1234567890abcdef");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostWebhook_WithValidSignature_Returns200Ok()
    {
        // Arrange
        var client = _factory.CreateClient();
        string json = """
        {
          "action": "create",
          "type": "userstory",
          "by": { "full_name": "Alex Rivera" },
          "data": {
            "id": 1,
            "subject": "Fix user authentication flow",
            "project": { "id": 101, "name": "Taiga Project" }
          }
        }
        """;

        byte[] bodyBytes = Encoding.UTF8.GetBytes(json);
        string signature = ComputeHmacSha1Hex(WebhookSecret, bodyBytes);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/taiga")
        {
            Content = new ByteArrayContent(bodyBytes)
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        request.Headers.Add("X-TAIGA-KEY", signature);

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string responseBody = await response.Content.ReadAsStringAsync();
        Assert.Contains("success", responseBody);
    }

    [Fact]
    public async Task PostWebhook_WithValidQuerySecret_Returns200Ok()
    {
        // Arrange
        var client = _factory.CreateClient();
        string json = """
        {
          "action": "create",
          "type": "task",
          "by": { "full_name": "Alex Rivera" },
          "data": {
            "id": 2,
            "subject": "Setup database",
            "project": { "id": 101, "name": "Taiga Project" }
          }
        }
        """;

        byte[] bodyBytes = Encoding.UTF8.GetBytes(json);
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/webhooks/taiga?secret={WebhookSecret}")
        {
            Content = new ByteArrayContent(bodyBytes)
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PostWebhook_WithInvalidJson_Returns400BadRequest()
    {
        // Arrange
        var client = _factory.CreateClient();
        byte[] malformedBytes = Encoding.UTF8.GetBytes("not-a-valid-json-string");
        string signature = ComputeHmacSha1Hex(WebhookSecret, malformedBytes);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/taiga")
        {
            Content = new ByteArrayContent(malformedBytes)
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        request.Headers.Add("X-TAIGA-KEY", signature);

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetHealthCheck_Returns200Ok()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/healthz");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("healthy", body);
    }

    [Fact]
    public async Task PostWebhook_WhenTelegramDispatchFails_Returns502()
    {
        _mockNotificationService
            .Setup(s => s.SendNotificationAsync(It.IsAny<long>(), It.IsAny<TaigaWebhookPayload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        try
        {
            var client = _factory.CreateClient();
            string json = """{"action":"create","type":"task","data":{"subject":"Fail send","project":{"id":101,"name":"Taiga Project"}}}""";
            var response = await SendSignedWebhookAsync(client, json);

            Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        }
        finally
        {
            _mockNotificationService
                .Setup(s => s.SendNotificationAsync(It.IsAny<long>(), It.IsAny<TaigaWebhookPayload>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
        }
    }

    [Fact]
    public async Task PostWebhook_WhenProjectHasNoChatMapping_ReturnsIgnored()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.Configure<TelegramRelay.Configuration.RelayOptions>(options =>
                {
                    options.DefaultChatId = null;
                    options.ProjectChatMappings = new Dictionary<string, long>();
                });
                services.AddScoped(_ => _mockNotificationService.Object);
            });
        });

        var client = factory.CreateClient();
        string json = """{"action":"create","type":"task","data":{"subject":"Unmapped","project":{"id":999,"name":"Unknown"}}}""";
        var response = await SendSignedWebhookAsync(client, json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("ignored", body);
    }

    private async Task<HttpResponseMessage> SendSignedWebhookAsync(HttpClient client, string json)
    {
        byte[] bodyBytes = Encoding.UTF8.GetBytes(json);
        string signature = ComputeHmacSha1Hex(WebhookSecret, bodyBytes);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/taiga")
        {
            Content = new ByteArrayContent(bodyBytes)
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        request.Headers.Add("X-TAIGA-KEY", signature);

        return await client.SendAsync(request);
    }
}

