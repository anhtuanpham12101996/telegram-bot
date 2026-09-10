namespace TelegramRelay.Endpoints;

using System.Text.Json;
using Microsoft.Extensions.Options;
using TelegramRelay.Configuration;
using TelegramRelay.Models;
using TelegramRelay.Services;

public static class GitLabWebhookEndpoints
{
    public static void MapGitLabWebhook(this WebApplication app)
    {
        app.MapPost("/api/webhooks/gitlab", HandleAsync);
    }

    private static async Task<IResult> HandleAsync(
        HttpContext httpContext,
        IGitLabWebhookValidator validator,
        ITelegramChatResolver chatResolver,
        IGitLabNotificationService notificationService,
        IOptions<RelayOptions> options,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        ILogger logger = loggerFactory.CreateLogger("GitLabWebhook");

        httpContext.Request.EnableBuffering();

        byte[] rawBody;
        using (var memoryStream = new MemoryStream())
        {
            await httpContext.Request.Body.CopyToAsync(memoryStream, cancellationToken);
            rawBody = memoryStream.ToArray();
            httpContext.Request.Body.Position = 0;
        }

        if (!validator.Validate(httpContext.Request, rawBody))
        {
            logger.LogWarning("GitLab webhook validation failed for request from {RemoteIp}.", httpContext.Connection.RemoteIpAddress);
            return Results.Json(
                new
                {
                    error = "unauthorized",
                    hint = "GitLab.com signs webhooks with webhook-signature (whsec_ signing token). Put that token in TelegramRelay__GitLabSecret, or use a legacy Secret token matching X-Gitlab-Token."
                },
                statusCode: StatusCodes.Status401Unauthorized);
        }

        GitLabWebhookPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<GitLabWebhookPayload>(rawBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Failed to parse incoming GitLab webhook JSON payload.");
            return Results.BadRequest(new { error = "Invalid JSON payload" });
        }

        if (payload == null)
        {
            logger.LogWarning("Received empty or null GitLab webhook payload.");
            return Results.BadRequest(new { error = "Empty payload" });
        }

        GitLabNotifyKind kind = GitLabEventClassifier.Classify(payload);
        if (kind == GitLabNotifyKind.Ignored)
        {
            logger.LogInformation(
                "Ignoring GitLab webhook object_kind={ObjectKind} action={Action} noteable={Noteable}.",
                payload.ObjectKind, payload.ObjectAttributes?.Action, payload.ObjectAttributes?.NoteableType);

            return Results.Ok(new
            {
                status = "ignored",
                reason = "Event is not a merge request open, comment, approval, or merge."
            });
        }

        int? projectId = payload.ResolvedProjectId;
        long? targetChatId = null;

        if (projectId.HasValue)
        {
            targetChatId = chatResolver.ResolveGitLabChatId(projectId.Value);
        }
        else if (options.Value.DefaultChatId.HasValue)
        {
            targetChatId = options.Value.DefaultChatId.Value;
            logger.LogInformation("No GitLab project ID found; falling back to DefaultChatId {DefaultChatId}.", targetChatId.Value);
        }

        if (!targetChatId.HasValue)
        {
            logger.LogWarning("No target Telegram chat ID resolved for GitLab project {ProjectId}.", projectId);
            return Results.Ok(new
            {
                status = "ignored",
                reason = "No mapped or default Telegram Chat ID configured for this GitLab project."
            });
        }

        bool sent = await notificationService.SendNotificationAsync(targetChatId.Value, payload, kind, cancellationToken);
        if (!sent)
        {
            logger.LogError("Failed to dispatch GitLab Telegram message to Chat ID {ChatId}.", targetChatId.Value);
            return Results.Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "Telegram Dispatch Failed",
                detail: "Could not deliver the GitLab notification message to Telegram.");
        }

        return Results.Ok(new
        {
            status = "success",
            chatId = targetChatId.Value,
            kind = kind.ToString(),
            objectKind = payload.ObjectKind,
            action = payload.ObjectAttributes?.Action
        });
    }
}
