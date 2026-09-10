namespace TelegramRelay.Services;

using System.Threading.Channels;

public sealed class GitLabNotificationQueue : IGitLabNotificationQueue
{
    private readonly Channel<GitLabNotificationJob> _channel = Channel.CreateUnbounded<GitLabNotificationJob>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    public ValueTask QueueAsync(GitLabNotificationJob job, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        return _channel.Writer.WriteAsync(job, cancellationToken);
    }

    public IAsyncEnumerable<GitLabNotificationJob> DequeueAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
