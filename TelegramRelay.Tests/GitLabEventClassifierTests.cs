namespace TelegramRelay.Tests;

using TelegramRelay.Models;
using TelegramRelay.Services;
using Xunit;

public class GitLabEventClassifierTests
{
    [Theory]
    [InlineData("open", GitLabNotifyKind.MergeRequestOpened)]
    [InlineData("reopen", GitLabNotifyKind.MergeRequestReopened)]
    [InlineData("approved", GitLabNotifyKind.MergeRequestApproved)]
    [InlineData("approval", GitLabNotifyKind.MergeRequestApproved)]
    [InlineData("merge", GitLabNotifyKind.MergeRequestMerged)]
    [InlineData("update", GitLabNotifyKind.Ignored)]
    [InlineData("close", GitLabNotifyKind.Ignored)]
    public void Classify_MergeRequestActions(string action, GitLabNotifyKind expected)
    {
        var payload = new GitLabWebhookPayload
        {
            ObjectKind = "merge_request",
            ObjectAttributes = new GitLabObjectAttributes { Action = action, Iid = 12, Title = "Fix login" }
        };

        Assert.Equal(expected, GitLabEventClassifier.Classify(payload));
    }

    [Fact]
    public void Classify_NoteOnMergeRequest_ReturnsCommented()
    {
        var payload = new GitLabWebhookPayload
        {
            ObjectKind = "note",
            ObjectAttributes = new GitLabObjectAttributes
            {
                Action = "create",
                NoteableType = "MergeRequest",
                Note = "Looks good"
            },
            MergeRequest = new GitLabMergeRequest { Iid = 12, Title = "Fix login" }
        };

        Assert.Equal(GitLabNotifyKind.MergeRequestCommented, GitLabEventClassifier.Classify(payload));
    }

    [Fact]
    public void Classify_NoteOnIssue_ReturnsIgnored()
    {
        var payload = new GitLabWebhookPayload
        {
            ObjectKind = "note",
            ObjectAttributes = new GitLabObjectAttributes
            {
                Action = "create",
                NoteableType = "Issue",
                Note = "Not an MR comment"
            }
        };

        Assert.Equal(GitLabNotifyKind.Ignored, GitLabEventClassifier.Classify(payload));
    }

    [Fact]
    public void Classify_SystemNoteOnMergeRequest_ReturnsIgnored()
    {
        var payload = new GitLabWebhookPayload
        {
            ObjectKind = "note",
            ObjectAttributes = new GitLabObjectAttributes
            {
                Action = "create",
                NoteableType = "MergeRequest",
                Note = "changed the description",
                System = true
            }
        };

        Assert.Equal(GitLabNotifyKind.Ignored, GitLabEventClassifier.Classify(payload));
    }

    [Fact]
    public void Classify_PushEvent_ReturnsIgnored()
    {
        var payload = new GitLabWebhookPayload { ObjectKind = "push" };

        Assert.Equal(GitLabNotifyKind.Ignored, GitLabEventClassifier.Classify(payload));
    }
}
