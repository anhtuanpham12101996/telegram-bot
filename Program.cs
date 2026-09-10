using System.Text.Json;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using TelegramRelay.Configuration;
using TelegramRelay.Endpoints;
using TelegramRelay.Models;
using TelegramRelay.Services;

var builder = WebApplication.CreateBuilder(args);

// Render (and most PaaS hosts) inject the listen port via PORT.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://+:{port}");
}

// 1. Configuration & Options
builder.Services.Configure<RelayOptions>(
    builder.Configuration.GetSection(RelayOptions.SectionName));

// 2. HTTP Client & Telegram Bot Registration via Typed Client Pattern
builder.Services.AddHttpClient("TelegramBotClient")
    .AddTypedClient<ITelegramBotClient>((httpClient, sp) =>
    {
        var options = sp.GetRequiredService<IOptions<RelayOptions>>().Value;
        string token = string.IsNullOrWhiteSpace(options.TelegramBotToken) 
            ? "0000000000:AAPlaceholderTokenForTestingOnly" 
            : options.TelegramBotToken;

        return new TelegramBotClient(token, httpClient);
    });

// 3. Application Services
builder.Services.AddSingleton<ITelegramChatResolver, TelegramChatResolver>();
builder.Services.AddSingleton<ITaigaWebhookValidator, TaigaWebhookValidator>();
builder.Services.AddSingleton<IGitLabWebhookValidator, GitLabWebhookValidator>();
builder.Services.AddScoped<ITelegramNotificationService, TelegramNotificationService>();
builder.Services.AddScoped<IGitLabNotificationService, GitLabNotificationService>();

var app = builder.Build();

// Health check endpoints
app.MapGet("/", () => Results.Ok("Telegram Webhook Relay Service is running."));
app.MapGet("/healthz", () => Results.Ok(new { status = "healthy", timestamp = DateTimeOffset.UtcNow }));
app.MapGitLabWebhook();

// 4. Taiga Webhook Ingestion Endpoint
app.MapPost("/api/webhooks/taiga", async (
    HttpContext httpContext,
    ITaigaWebhookValidator validator,
    ITelegramChatResolver chatResolver,
    ITelegramNotificationService notificationService,
    IOptions<RelayOptions> options,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    // Enable buffering so request body can be read for HMAC verification
    httpContext.Request.EnableBuffering();

    byte[] rawBody;
    using (var memoryStream = new MemoryStream())
    {
        await httpContext.Request.Body.CopyToAsync(memoryStream, cancellationToken);
        rawBody = memoryStream.ToArray();
        httpContext.Request.Body.Position = 0;
    }

    // 1. Webhook Validation
    if (!validator.Validate(httpContext.Request, rawBody))
    {
        logger.LogWarning("Webhook validation failed for request from {RemoteIp}.", httpContext.Connection.RemoteIpAddress);
        return Results.Unauthorized();
    }

    // 2. Parse Event Payload
    TaigaWebhookPayload? payload;
    try
    {
        payload = JsonSerializer.Deserialize<TaigaWebhookPayload>(rawBody, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }
    catch (JsonException ex)
    {
        logger.LogError(ex, "Failed to parse incoming Taiga webhook JSON payload.");
        return Results.BadRequest(new { error = "Invalid JSON payload" });
    }

    if (payload == null)
    {
        logger.LogWarning("Received empty or null Taiga webhook payload.");
        return Results.BadRequest(new { error = "Empty payload" });
    }

    // 3. Find Telegram Chat ID
    int? projectId = payload.Data?.ResolvedProjectId;
    long? targetChatId = null;

    if (projectId.HasValue)
    {
        targetChatId = chatResolver.ResolveChatId(projectId.Value);
    }
    else if (options.Value.DefaultChatId.HasValue)
    {
        targetChatId = options.Value.DefaultChatId.Value;
        logger.LogInformation("No project ID found in payload; falling back to DefaultChatId {DefaultChatId}.", targetChatId.Value);
    }

    if (!targetChatId.HasValue)
    {
        logger.LogWarning("No target Telegram chat ID resolved for project {ProjectId}. Event will not be dispatched.", projectId);
        return Results.Ok(new
        {
            status = "ignored",
            reason = "No mapped or default Telegram Chat ID configured for this project."
        });
    }

    // 4. Send Telegram Message
    bool sent = await notificationService.SendNotificationAsync(targetChatId.Value, payload, cancellationToken);
    if (!sent)
    {
        logger.LogError("Failed to dispatch Telegram message to Chat ID {ChatId}.", targetChatId.Value);
        return Results.Problem(
            statusCode: StatusCodes.Status502BadGateway,
            title: "Telegram Dispatch Failed",
            detail: "Could not deliver the notification message to Telegram.");
    }

    return Results.Ok(new
    {
        status = "success",
        chatId = targetChatId.Value,
        action = payload.Action,
        type = payload.Type
    });
});

app.Run();

// Required for WebApplicationFactory in integration tests
public partial class Program { }

