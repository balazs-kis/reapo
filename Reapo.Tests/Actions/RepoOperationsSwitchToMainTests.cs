using LibGit2Sharp;
using Reapo.Actions;
using Reapo.Discovery;
using Reapo.Git;
using Reapo.Tests.Support;

namespace Reapo.Tests.Actions;

public sealed class RepoOperationsSwitchToMainTests : GitRepoTestBase
{
    [Fact]
    public async Task Switches_from_feature_to_default_on_clean_worktree()
    {
        var repoPath = InitRepo("repo");
        string defaultBranch;
        using (var repo = new Repository(repoPath))
        {
            SetIdentity(repo);
            CommitFile(repo, "x.txt", "base\n", "init");
            defaultBranch = repo.Head.FriendlyName;
            var feature = repo.CreateBranch("feature");
            Commands.Checkout(repo, feature);
        }

        var outcome = await new RepoOperations(new GitFacade())
            .SwitchToMainAsync(new RepoInfo("repo", repoPath), CancellationToken.None);

        Assert.Equal(RepoOutcomeKind.Processed, outcome.Kind);
        var step = Assert.Single(outcome.Branches);
        Assert.Equal(defaultBranch, step.Branch);
        Assert.Equal(BranchOutcomeKind.Updated, step.Kind);

        using var check = new Repository(repoPath);
        Assert.Equal(defaultBranch, check.Head.FriendlyName);
    }

    [Fact]
    public async Task Already_on_main_reports_as_already_up_to_date()
    {
        var repoPath = InitRepo("repo");
        string defaultBranch;
        using (var repo = new Repository(repoPath))
        {
            SetIdentity(repo);
            CommitFile(repo, "x.txt", "base\n", "init");
            defaultBranch = repo.Head.FriendlyName;
        }

        var outcome = await new RepoOperations(new GitFacade())
            .SwitchToMainAsync(new RepoInfo("repo", repoPath), CancellationToken.None);

        Assert.Equal(RepoOutcomeKind.Processed, outcome.Kind);
        var step = Assert.Single(outcome.Branches);
        Assert.Equal(defaultBranch, step.Branch);
        Assert.Equal(BranchOutcomeKind.AlreadyUpToDate, step.Kind);
    }

    [Fact]
    public async Task Dirty_worktree_is_stashed_and_restored_across_the_switch()
    {
        var repoPath = InitRepo("repo");
        string defaultBranch;
        using (var repo = new Repository(repoPath))
        {
            SetIdentity(repo);
            CommitFile(repo, "x.txt", "base\n", "init");
            defaultBranch = repo.Head.FriendlyName;
            var feature = repo.CreateBranch("feature");
            Commands.Checkout(repo, feature);
        }

        // Uncommitted local edit on feature that doesn't conflict with main.
        File.WriteAllText(Path.Combine(repoPath, "scratch.txt"), "wip\n");

        var outcome = await new RepoOperations(new GitFacade())
            .SwitchToMainAsync(new RepoInfo("repo", repoPath), CancellationToken.None);

        Assert.Equal(RepoOutcomeKind.Processed, outcome.Kind);
        Assert.DoesNotContain(outcome.Branches, b => b.Kind == BranchOutcomeKind.Failed);

        using var check = new Repository(repoPath);
        Assert.Equal(defaultBranch, check.Head.FriendlyName);
        Assert.True(File.Exists(Path.Combine(repoPath, "scratch.txt")),
            "stashed changes should have been popped back onto main");
    }

    [Fact]
    public async Task Missing_default_branch_reports_failed()
    {
        // Fresh repo with no commits and no branches — GetDefaultBranchName returns null.
        var repoPath = InitRepo("repo");

        var outcome = await new RepoOperations(new GitFacade())
            .SwitchToMainAsync(new RepoInfo("repo", repoPath), CancellationToken.None);

        Assert.Equal(RepoOutcomeKind.Failed, outcome.Kind);
        Assert.Contains("main/master", outcome.Detail ?? string.Empty);
    }
}
