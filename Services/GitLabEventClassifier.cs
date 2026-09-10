namespace TelegramRelay.Services;

using TelegramRelay.Models;

/// <summary>
/// Classifies GitLab webhook payloads into the MR events we notify on.
/// </summary>
public static class GitLabEventClassifier
{
    public static GitLabNotifyKind Classify(GitLabWebhookPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        string kind = payload.ObjectKind ?? payload.EventType ?? string.Empty;

        if (IsMergeRequestEvent(kind))
        {
            return ClassifyMergeRequestAction(payload.ObjectAttributes?.Action);
        }

        if (IsNoteEvent(kind))
        {
            return ClassifyNote(payload);
        }

        if (IsPipelineEvent(kind))
        {
            return ClassifySuccessOrFailure(payload.ObjectAttributes?.Status, GitLabNotifyKind.PipelineSucceeded, GitLabNotifyKind.PipelineFailed);
        }

        if (IsJobEvent(kind))
        {
            return ClassifySuccessOrFailure(payload.BuildStatus, GitLabNotifyKind.JobSucceeded, GitLabNotifyKind.JobFailed);
        }

        if (IsDeploymentEvent(kind))
        {
            return ClassifySuccessOrFailure(payload.Status, GitLabNotifyKind.DeploymentSucceeded, GitLabNotifyKind.DeploymentFailed);
        }

        return GitLabNotifyKind.Ignored;
    }

    private static GitLabNotifyKind ClassifyMergeRequestAction(string? action)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            return GitLabNotifyKind.Ignored;
        }

        return action.ToLowerInvariant() switch
        {
            "open" => GitLabNotifyKind.MergeRequestOpened,
            "reopen" => GitLabNotifyKind.MergeRequestReopened,
            "approved" or "approval" => GitLabNotifyKind.MergeRequestApproved,
            "merge" => GitLabNotifyKind.MergeRequestMerged,
            _ => GitLabNotifyKind.Ignored
        };
    }

    private static GitLabNotifyKind ClassifyNote(GitLabWebhookPayload payload)
    {
        GitLabObjectAttributes? attributes = payload.ObjectAttributes;
        if (attributes is null)
        {
            return GitLabNotifyKind.Ignored;
        }

        if (!string.Equals(attributes.NoteableType, "MergeRequest", StringComparison.OrdinalIgnoreCase))
        {
            return GitLabNotifyKind.Ignored;
        }

        if (attributes.System)
        {
            return GitLabNotifyKind.Ignored;
        }

        if (string.IsNullOrWhiteSpace(attributes.Action) ||
            string.Equals(attributes.Action, "create", StringComparison.OrdinalIgnoreCase))
        {
            return GitLabNotifyKind.MergeRequestCommented;
        }

        return GitLabNotifyKind.Ignored;
    }

    private static GitLabNotifyKind ClassifySuccessOrFailure(
        string? status,
        GitLabNotifyKind success,
        GitLabNotifyKind failure)
    {
        return (status ?? string.Empty).ToLowerInvariant() switch
        {
            "success" => success,
            "failed" or "failure" => failure,
            _ => GitLabNotifyKind.Ignored
        };
    }

    private static bool IsMergeRequestEvent(string kind) =>
        string.Equals(kind, "merge_request", StringComparison.OrdinalIgnoreCase);

    private static bool IsNoteEvent(string kind) =>
        string.Equals(kind, "note", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(kind, "confidential_note", StringComparison.OrdinalIgnoreCase);

    private static bool IsPipelineEvent(string kind) =>
        string.Equals(kind, "pipeline", StringComparison.OrdinalIgnoreCase);

    private static bool IsJobEvent(string kind) =>
        string.Equals(kind, "build", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(kind, "job", StringComparison.OrdinalIgnoreCase);

    private static bool IsDeploymentEvent(string kind) =>
        string.Equals(kind, "deployment", StringComparison.OrdinalIgnoreCase);
}
