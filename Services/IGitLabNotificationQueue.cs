namespace TelegramRelay.Services;

public interface IGitLabNotificationQueue
{
    ValueTask QueueAsync(GitLabNotificationJob job, CancellationToken cancellationToken = default);

    IAsyncEnumerable<GitLabNotificationJob> DequeueAllAsync(CancellationToken cancellationToken);
}
