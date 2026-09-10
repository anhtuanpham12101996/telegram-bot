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

    private static bool IsMergeRequestEvent(string kind) =>
        string.Equals(kind, "merge_request", StringComparison.OrdinalIgnoreCase);

    private static bool IsNoteEvent(string kind) =>
        string.Equals(kind, "note", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(kind, "confidential_note", StringComparison.OrdinalIgnoreCase);
}
