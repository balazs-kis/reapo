using LibGit2Sharp;
using Reapo.Git;
using Reapo.Tests.Support;

namespace Reapo.Tests.Git;

public sealed class GitFacadeBranchTests : GitRepoTestBase
{

    [Fact]
    public void Local_only_branch_has_no_remote_and_null_counts()
    {
        var repoPath = InitRepo("repo");
        using (var repo = new Repository(repoPath))
        {
            CommitFile(repo, "a.txt", "hello", "init");
        }

        var branches = new GitFacade().GetBranches(repoPath);

        var only = Assert.Single(branches);
        Assert.False(only.HasRemote);
        Assert.Null(only.AheadBy);
        Assert.Null(only.BehindBy);
        Assert.True(only.IsCurrent);
    }

    [Fact]
    public void Tracked_branch_with_no_divergence_reports_zero_counts()
    {
        var bare = InitBareRepo("remote.git");
        var repoPath = InitRepo("repo");

        using (var repo = new Repository(repoPath))
        {
            CommitFile(repo, "a.txt", "hello", "init");
            var remote = repo.Network.Remotes.Add("origin", bare);
            repo.Network.Push(remote, repo.Head.CanonicalName);
            repo.Branches.Update(repo.Head, b => b.TrackedBranch = $"refs/remotes/origin/{repo.Head.FriendlyName}");
        }

        var branches = new GitFacade().GetBranches(repoPath);

        var current = Assert.Single(branches);
        Assert.True(current.HasRemote);
        Assert.Equal(0, current.AheadBy);
        Assert.Equal(0, current.BehindBy);
    }

    [Fact]
    public void Tracked_branch_with_local_extra_commit_is_ahead()
    {
        var bare = InitBareRepo("remote.git");
        var repoPath = InitRepo("repo");

        using (var repo = new Repository(repoPath))
        {
            CommitFile(repo, "a.txt", "hello", "init");
            var remote = repo.Network.Remotes.Add("origin", bare);
            repo.Network.Push(remote, repo.Head.CanonicalName);
            repo.Branches.Update(repo.Head, b => b.TrackedBranch = $"refs/remotes/origin/{repo.Head.FriendlyName}");

            CommitFile(repo, "b.txt", "world", "second");
        }

        var branches = new GitFacade().GetBranches(repoPath);

        var current = Assert.Single(branches);
        Assert.True(current.HasRemote);
        Assert.Equal(1, current.AheadBy);
        Assert.Equal(0, current.BehindBy);
    }

    [Fact]
    public void Multiple_branches_are_sorted_with_current_first()
    {
        var repoPath = InitRepo("repo");
        using (var repo = new Repository(repoPath))
        {
            var first = CommitFile(repo, "a.txt", "hello", "init");
            repo.Branches.Add("zeta", first);
            repo.Branches.Add("alpha", first);
        }

        var branches = new GitFacade().GetBranches(repoPath);

        Assert.Equal(3, branches.Count);
        Assert.True(branches[0].IsCurrent);
        Assert.Equal(new[] { "alpha", "zeta" }, branches.Skip(1).Select(b => b.Name).ToArray());
    }

    [Fact]
    public void Current_branch_dirty_flag_reflects_working_tree()
    {
        var repoPath = InitRepo("repo");
        using (var repo = new Repository(repoPath))
        {
            CommitFile(repo, "a.txt", "hello", "init");
        }

        File.WriteAllText(Path.Combine(repoPath, "a.txt"), "hello world");

        var branches = new GitFacade().GetBranches(repoPath);

        var current = Assert.Single(branches);
        Assert.Equal(1, current.DirtyCount);
    }
}
