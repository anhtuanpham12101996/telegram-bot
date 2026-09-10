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

        string projectName = WebUtility.HtmlEncode(payload.Project?.DisplayName ?? payload.ProjectName ?? "GitLab Project");
        string author = WebUtility.HtmlEncode(payload.User?.DisplayName ?? "GitLab User");
        string? link = ResolveLink(payload, kind);

        var sb = new StringBuilder();
        sb.AppendLine($"<b>{projectName}</b>");
        sb.AppendLine($"{ResolveEmoji(kind)} {ResolveHeadline(kind, payload)}");

        if (IsMergeRequestKind(kind))
        {
            AppendMergeRequestDetails(sb, payload, kind, author);
        }
        else
        {
            AppendCiDetails(sb, payload, kind, author);
        }

        AppendLink(sb, link);
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

    private static void AppendMergeRequestDetails(
        StringBuilder sb,
        GitLabWebhookPayload payload,
        GitLabNotifyKind kind,
        string author)
    {
        string mrTitle = WebUtility.HtmlEncode(ResolveMrTitle(payload));
        sb.AppendLine($"📌 <b>Title:</b> {mrTitle}");

        string? sourceBranch = payload.ObjectAttributes?.SourceBranch ?? payload.MergeRequest?.SourceBranch;
        string? targetBranch = payload.ObjectAttributes?.TargetBranch ?? payload.MergeRequest?.TargetBranch;
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
    }

    private static void AppendCiDetails(
        StringBuilder sb,
        GitLabWebhookPayload payload,
        GitLabNotifyKind kind,
        string author)
    {
        string? gitRef = payload.ObjectAttributes?.Ref ?? payload.Ref;
        if (!string.IsNullOrWhiteSpace(gitRef))
        {
            sb.AppendLine($"🌿 <b>Ref:</b> <code>{WebUtility.HtmlEncode(gitRef)}</code>");
        }

        if (kind is GitLabNotifyKind.JobSucceeded or GitLabNotifyKind.JobFailed)
        {
            if (!string.IsNullOrWhiteSpace(payload.BuildName))
            {
                string stage = string.IsNullOrWhiteSpace(payload.BuildStage) ? "" : $" ({WebUtility.HtmlEncode(payload.BuildStage)})";
                sb.AppendLine($"🧱 <b>Job:</b> <code>{WebUtility.HtmlEncode(payload.BuildName)}</code>{stage}");
            }

            if (kind == GitLabNotifyKind.JobFailed &&
                !string.IsNullOrWhiteSpace(payload.BuildFailureReason) &&
                !string.Equals(payload.BuildFailureReason, "unknown_failure", StringComparison.OrdinalIgnoreCase))
            {
                sb.AppendLine($"⚠️ <b>Reason:</b> {WebUtility.HtmlEncode(payload.BuildFailureReason)}");
            }
        }

        if ((kind is GitLabNotifyKind.DeploymentSucceeded or GitLabNotifyKind.DeploymentFailed) &&
            !string.IsNullOrWhiteSpace(payload.Environment))
        {
            sb.AppendLine($"🌍 <b>Environment:</b> {WebUtility.HtmlEncode(payload.Environment)}");
        }

        string? commitId = ResolveCommitId(payload);
        string? commitUrl = payload.CommitUrl ?? payload.Commit?.Url;
        string? commitTitle = payload.CommitTitle ?? payload.Commit?.Title;
        if (!string.IsNullOrWhiteSpace(commitId))
        {
            string encodedId = WebUtility.HtmlEncode(commitId);
            if (!string.IsNullOrWhiteSpace(commitUrl) && Uri.TryCreate(commitUrl, UriKind.Absolute, out _))
            {
                sb.AppendLine($"🔖 <b>Commit:</b> <a href=\"{WebUtility.HtmlEncode(commitUrl)}\"><code>{encodedId}</code></a>");
            }
            else
            {
                sb.AppendLine($"🔖 <b>Commit:</b> <code>{encodedId}</code>");
            }
        }

        if (!string.IsNullOrWhiteSpace(commitTitle))
        {
            sb.AppendLine($"📌 <b>Message:</b> {WebUtility.HtmlEncode(commitTitle)}");
        }

        sb.AppendLine($"👤 <b>By:</b> {author}");
    }

    private static void AppendLink(StringBuilder sb, string? link)
    {
        if (string.IsNullOrWhiteSpace(link))
        {
            return;
        }

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

    private static string ResolveHeadline(GitLabNotifyKind kind, GitLabWebhookPayload payload)
    {
        string mrRef = ResolveMrIid(payload) is int iid ? $"!{iid}" : "MR";
        return kind switch
        {
            GitLabNotifyKind.MergeRequestOpened => $"Merge Request {mrRef} opened",
            GitLabNotifyKind.MergeRequestReopened => $"Merge Request {mrRef} reopened",
            GitLabNotifyKind.MergeRequestCommented => $"New comment on {mrRef}",
            GitLabNotifyKind.MergeRequestApproved => $"Merge Request {mrRef} approved",
            GitLabNotifyKind.MergeRequestMerged => $"Merge Request {mrRef} merged",
            GitLabNotifyKind.PipelineSucceeded => "Pipeline succeeded",
            GitLabNotifyKind.PipelineFailed => "Pipeline failed",
            GitLabNotifyKind.JobSucceeded => "Job succeeded",
            GitLabNotifyKind.JobFailed => "Job failed",
            GitLabNotifyKind.DeploymentSucceeded => "Deployment succeeded",
            GitLabNotifyKind.DeploymentFailed => "Deployment failed",
            _ => "GitLab event"
        };
    }

    private static string ResolveEmoji(GitLabNotifyKind kind) => kind switch
    {
        GitLabNotifyKind.MergeRequestOpened => "🔀",
        GitLabNotifyKind.MergeRequestReopened => "🔁",
        GitLabNotifyKind.MergeRequestCommented => "💬",
        GitLabNotifyKind.MergeRequestApproved => "✅",
        GitLabNotifyKind.MergeRequestMerged => "🎉",
        GitLabNotifyKind.PipelineSucceeded or GitLabNotifyKind.JobSucceeded or GitLabNotifyKind.DeploymentSucceeded => "✅",
        GitLabNotifyKind.PipelineFailed or GitLabNotifyKind.JobFailed or GitLabNotifyKind.DeploymentFailed => "❌",
        _ => "📣"
    };

    private static bool IsMergeRequestKind(GitLabNotifyKind kind) => kind is
        GitLabNotifyKind.MergeRequestOpened or
        GitLabNotifyKind.MergeRequestReopened or
        GitLabNotifyKind.MergeRequestCommented or
        GitLabNotifyKind.MergeRequestApproved or
        GitLabNotifyKind.MergeRequestMerged;

    private static string ResolveMrTitle(GitLabWebhookPayload payload) =>
        payload.ObjectAttributes?.Title
        ?? payload.MergeRequest?.Title
        ?? "Untitled";

    private static int? ResolveMrIid(GitLabWebhookPayload payload) =>
        payload.ObjectAttributes?.Iid ?? payload.MergeRequest?.Iid;

    private static string? ResolveLink(GitLabWebhookPayload payload, GitLabNotifyKind kind)
    {
        if (!string.IsNullOrWhiteSpace(payload.ObjectAttributes?.Url))
        {
            return payload.ObjectAttributes.Url;
        }

        if (!string.IsNullOrWhiteSpace(payload.MergeRequest?.Url))
        {
            return payload.MergeRequest.Url;
        }

        if (!string.IsNullOrWhiteSpace(payload.DeployableUrl))
        {
            return payload.DeployableUrl;
        }

        if (!string.IsNullOrWhiteSpace(payload.CommitUrl))
        {
            return payload.CommitUrl;
        }

        if (!string.IsNullOrWhiteSpace(payload.Commit?.Url))
        {
            return payload.Commit.Url;
        }

        string? projectUrl = payload.Project?.WebUrl?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(projectUrl))
        {
            return null;
        }

        if ((kind is GitLabNotifyKind.JobSucceeded or GitLabNotifyKind.JobFailed) && payload.BuildId.HasValue)
        {
            return $"{projectUrl}/-/jobs/{payload.BuildId.Value}";
        }

        if ((kind is GitLabNotifyKind.PipelineSucceeded or GitLabNotifyKind.PipelineFailed) &&
            payload.ObjectAttributes?.Id is long pipelineId)
        {
            return $"{projectUrl}/-/pipelines/{pipelineId}";
        }

        return projectUrl;
    }

    private static string? ResolveCommitId(GitLabWebhookPayload payload)
    {
        string? sha = FirstNonEmpty(
            payload.ShortSha,
            payload.Sha,
            payload.ObjectAttributes?.Sha,
            payload.Commit?.ResolvedSha);

        if (string.IsNullOrWhiteSpace(sha))
        {
            return null;
        }

        return sha.Length > 8 ? sha[..8] : sha;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (string? value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

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
