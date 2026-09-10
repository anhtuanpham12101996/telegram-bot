namespace TelegramRelay.Tests;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TelegramRelay.Models;
using TelegramRelay.Services;
using Xunit;

public class GitLabNotificationWorkerTests
{
    [Fact]
    public async Task Worker_SendsQueuedJob()
    {
        var queue = new GitLabNotificationQueue();
        var mockNotifications = new Mock<IGitLabNotificationService>();
        mockNotifications
            .Setup(s => s.SendNotificationAsync(
                It.IsAny<long>(),
                It.IsAny<GitLabWebhookPayload>(),
                It.IsAny<GitLabNotifyKind>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var services = new ServiceCollection();
        services.AddScoped(_ => mockNotifications.Object);
        await using ServiceProvider provider = services.BuildServiceProvider();

        var worker = new GitLabNotificationWorker(
            queue,
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<GitLabNotificationWorker>.Instance);

        var payload = new GitLabWebhookPayload
        {
            ObjectKind = "merge_request",
            ObjectAttributes = new GitLabObjectAttributes { Action = "open", Iid = 12 }
        };
        var job = new GitLabNotificationJob(-100123456789, payload, GitLabNotifyKind.MergeRequestOpened);

        await worker.StartAsync(CancellationToken.None);
        await queue.QueueAsync(job);

        DateTime deadline = DateTime.UtcNow.AddSeconds(2);
        while (DateTime.UtcNow < deadline && mockNotifications.Invocations.Count == 0)
        {
            await Task.Delay(10);
        }

        await worker.StopAsync(CancellationToken.None);

        mockNotifications.Verify(s => s.SendNotificationAsync(
            -100123456789,
            payload,
            GitLabNotifyKind.MergeRequestOpened,
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
