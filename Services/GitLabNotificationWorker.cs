namespace TelegramRelay.Services;

public sealed class GitLabNotificationWorker(
    IGitLabNotificationQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<GitLabNotificationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (GitLabNotificationJob job in queue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                using IServiceScope scope = scopeFactory.CreateScope();
                IGitLabNotificationService notifications =
                    scope.ServiceProvider.GetRequiredService<IGitLabNotificationService>();

                bool sent = await notifications.SendNotificationAsync(
                    job.ChatId,
                    job.Payload,
                    job.Kind,
                    stoppingToken);

                if (!sent)
                {
                    logger.LogError(
                        "Failed to dispatch GitLab Telegram message to Chat ID {ChatId}.",
                        job.ChatId);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Unhandled error dispatching GitLab Telegram message to Chat ID {ChatId}.",
                    job.ChatId);
            }
        }
    }
}
