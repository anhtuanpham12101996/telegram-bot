namespace TelegramRelay.Services;

using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using TelegramRelay.Models;

/// <summary>
/// Formats GitLab MR events as Telegram HTML and sends them via Bot API.
/// </summary>
public sealed class GitLabNotificationService(
    ITelegramBotClient botClient,
    ILogger<GitLabNotificationService> logger) : IGitLabNotificationService
{
    private const int MaxCommentLength = 280;

    private readonly ITelegramBotClient _botClient = botClient ?? throw new ArgumentNullException(nameof(botClient));
    private readonly ILogger<GitLabNotificationService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public string FormatMessage(GitLabWebhookPayload payload, GitLabNotifyKind kind)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (kind == GitLabNotifyKind.Ignored)
        {
            throw new ArgumentOutOfRangeException(nameof(kind), "Ignored events cannot be formatted.");
        }

        string projectName = WebUtility.HtmlEncode(payload.Project?.DisplayName ?? "GitLab Project");
        string author = WebUtility.HtmlEncode(payload.User?.DisplayName ?? "GitLab User");
        string mrTitle = WebUtility.HtmlEncode(ResolveMrTitle(payload));
        string mrRef = ResolveMrIid(payload) is int iid ? $"!{iid}" : "MR";
        string? link = ResolveLink(payload);
        string? sourceBranch = payload.ObjectAttributes?.SourceBranch ?? payload.MergeRequest?.SourceBranch;
        string? targetBranch = payload.ObjectAttributes?.TargetBranch ?? payload.MergeRequest?.TargetBranch;

        var sb = new StringBuilder();
        sb.AppendLine($"{ResolveEmoji(kind)} <b>[{projectName}]</b> {ResolveHeadline(kind, mrRef)}");
        sb.AppendLine($"📌 <b>Title:</b> {mrTitle}");

        if (!string.IsNullOrWhiteSpace(sourceBranch) && !string.IsNullOrWhiteSpace(targetBranch) &&
            kind is GitLabNotifyKind.MergeRequestOpened or GitLabNotifyKind.MergeRequestReopened or GitLabNotifyKind.MergeRequestMerged)
        {
            sb.AppendLine($"🌿 <code>{WebUtility.HtmlEncode(sourceBranch)}</code> → <code>{WebUtility.HtmlEncode(targetBranch)}</code>");
        }

        if (kind == GitLabNotifyKind.MergeRequestCommented)
        {
            string comment = Truncate(payload.ObjectAttributes?.Note ?? string.Empty);
            sb.AppendLine($"💬 <b>{author}:</b> {WebUtility.HtmlEncode(comment)}");
        }
        else
        {
            sb.AppendLine($"👤 <b>By:</b> {author}");
        }

        if (!string.IsNullOrWhiteSpace(link))
        {
            string encodedLink = WebUtility.HtmlEncode(link);
            if (Uri.TryCreate(link, UriKind.Absolute, out _))
            {
                sb.Append($"🔗 <a href=\"{encodedLink}\">View on GitLab</a>");
            }
            else
            {
                sb.Append($"🔗 <code>{encodedLink}</code>");
            }
        }

        return sb.ToString().TrimEnd();
    }

    public async Task<bool> SendNotificationAsync(
        long chatId,
        GitLabWebhookPayload payload,
        GitLabNotifyKind kind,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        string message = FormatMessage(payload, kind);

        try
        {
            await _botClient.SendMessage(
                chatId: chatId,
                text: message,
                parseMode: ParseMode.Html,
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Sent GitLab {Kind} notification to Chat ID {ChatId} for project {Project}.",
                kind, chatId, payload.Project?.DisplayName);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send GitLab Telegram notification to Chat ID {ChatId}.", chatId);
            return false;
        }
    }

    private static string ResolveHeadline(GitLabNotifyKind kind, string mrRef) => kind switch
    {
        GitLabNotifyKind.MergeRequestOpened => $"Merge Request {mrRef} opened",
        GitLabNotifyKind.MergeRequestReopened => $"Merge Request {mrRef} reopened",
        GitLabNotifyKind.MergeRequestCommented => $"New comment on {mrRef}",
        GitLabNotifyKind.MergeRequestApproved => $"Merge Request {mrRef} approved",
        GitLabNotifyKind.MergeRequestMerged => $"Merge Request {mrRef} merged",
        _ => $"Merge Request {mrRef} updated"
    };

    private static string ResolveEmoji(GitLabNotifyKind kind) => kind switch
    {
        GitLabNotifyKind.MergeRequestOpened => "🔀",
        GitLabNotifyKind.MergeRequestReopened => "🔁",
        GitLabNotifyKind.MergeRequestCommented => "💬",
        GitLabNotifyKind.MergeRequestApproved => "✅",
        GitLabNotifyKind.MergeRequestMerged => "🎉",
        _ => "📣"
    };

    private static string ResolveMrTitle(GitLabWebhookPayload payload) =>
        payload.ObjectAttributes?.Title
        ?? payload.MergeRequest?.Title
        ?? "Untitled";

    private static int? ResolveMrIid(GitLabWebhookPayload payload) =>
        payload.ObjectAttributes?.Iid ?? payload.MergeRequest?.Iid;

    private static string? ResolveLink(GitLabWebhookPayload payload) =>
        !string.IsNullOrWhiteSpace(payload.ObjectAttributes?.Url)
            ? payload.ObjectAttributes.Url
            : payload.MergeRequest?.Url;

    private static string Truncate(string value)
    {
        string trimmed = value.Replace("\r\n", "\n").Trim();
        if (trimmed.Length <= MaxCommentLength)
        {
            return trimmed;
        }

        return trimmed[..MaxCommentLength].TrimEnd() + "…";
    }
}
