namespace TelegramRelay.Services;

/// <summary>
/// GitLab webhook events that should be forwarded to Telegram.
/// </summary>
public enum GitLabNotifyKind
{
    Ignored = 0,
    MergeRequestOpened,
    MergeRequestReopened,
    MergeRequestCommented,
    MergeRequestApproved,
    MergeRequestMerged
}
