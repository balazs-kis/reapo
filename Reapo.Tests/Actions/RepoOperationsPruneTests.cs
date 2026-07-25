using LibGit2Sharp;
using Reapo.Actions;
using Reapo.Discovery;
using Reapo.Git;
using Reapo.Tests.Support;

namespace Reapo.Tests.Actions;

public sealed class RepoOperationsPruneTests : GitRepoTestBase
{
    private RepoInfo InitRepoWithCommit(out string defaultBranch)
    {
        var repoPath = InitRepo("repo");
        using var repo = new Repository(repoPath);
        CommitFile(repo, "a.txt", "hi", "init");
        defaultBranch = repo.Head.FriendlyName;
        return new RepoInfo("repo", repoPath);
    }

    private static IReadOnlyList<string> RemainingBranchNames(string repoPath) =>
        new GitFacade().GetBranches(repoPath).Select(b => b.Name).ToList();

    [Fact]
    public async Task Untracked_variant_deletes_local_only_branches_and_keeps_default()
    {
        var repo = InitRepoWithCommit(out var defaultBranch);
        using (var r = new Repository(repo.FullPath))
        {
            r.Branches.Add("feature-a", r.Head.Tip);
            r.Branches.Add("feature-b", r.Head.Tip);
        }

        var outcome = await new RepoOperations(new GitFacade())
            .PruneAsync(repo, includeHealthyTracked: false, CancellationToken.None);

        Assert.Equal(RepoOutcomeKind.Processed, outcome.Kind);
        var remaining = RemainingBranchNames(repo.FullPath);
        Assert.Contains(defaultBranch, remaining);
        Assert.DoesNotContain("feature-a", remaining);
        Assert.DoesNotContain("feature-b", remaining);
    }

    [Fact]
    public async Task Untracked_variant_keeps_healthy_tracked_branch()
    {
        var bare = InitBareRepo("remote.git");
        var repo = InitRepoWithCommit(out var defaultBranch);

        using (var r = new Repository(repo.FullPath))
        {
            r.Network.Remotes.Add("origin", bare);
            var feature = r.Branches.Add("feature", r.Head.Tip);
            PushAndTrack(r, feature);
        }

        var outcome = await new RepoOperations(new GitFacade())
            .PruneAsync(repo, includeHealthyTracked: false, CancellationToken.None);

        Assert.Equal(RepoOutcomeKind.Processed, outcome.Kind);
        var remaining = RemainingBranchNames(repo.FullPath);
        Assert.Contains("feature", remaining);
        Assert.Contains(defaultBranch, remaining);
    }

    [Fact]
    public async Task All_variant_deletes_healthy_tracked_branch_but_keeps_default()
    {
        var bare = InitBareRepo("remote.git");
        var repo = InitRepoWithCommit(out var defaultBranch);

        using (var r = new Repository(repo.FullPath))
        {
            r.Network.Remotes.Add("origin", bare);
            var feature = r.Branches.Add("feature", r.Head.Tip);
            PushAndTrack(r, feature);
        }

        var outcome = await new RepoOperations(new GitFacade())
            .PruneAsync(repo, includeHealthyTracked: true, CancellationToken.None);

        Assert.Equal(RepoOutcomeKind.Processed, outcome.Kind);
        var remaining = RemainingBranchNames(repo.FullPath);
        Assert.DoesNotContain("feature", remaining);
        Assert.Contains(defaultBranch, remaining);
    }

    [Fact]
    public async Task Untracked_variant_deletes_branch_whose_upstream_is_gone()
    {
        var bare = InitBareRepo("remote.git");
        var repo = InitRepoWithCommit(out _);

        using (var r = new Repository(repo.FullPath))
        {
            r.Network.Remotes.Add("origin", bare);
            var feature = r.Branches.Add("feature", r.Head.Tip);
            PushAndTrack(r, feature);
        }

        // Remove the branch on the remote, then prune the local remote-tracking ref.
        using (var bareRepo = new Repository(bare))
        {
            bareRepo.Refs.Remove("refs/heads/feature");
        }
        await new GitFacade().FetchAsync(repo.FullPath, CancellationToken.None);

        var outcome = await new RepoOperations(new GitFacade())
            .PruneAsync(repo, includeHealthyTracked: false, CancellationToken.None);

        Assert.Equal(RepoOutcomeKind.Processed, outcome.Kind);
        Assert.DoesNotContain("feature", RemainingBranchNames(repo.FullPath));
    }

    [Fact]
    public async Task All_variant_always_protects_main_and_master_even_when_neither_is_current_or_default()
    {
        var repo = InitRepoWithCommit(out _);
        using (var r = new Repository(repo.FullPath))
        {
            var tip = r.Head.Tip;
            foreach (var name in new[] { "main", "master", "feature", "junk" })
            {
                if (r.Branches[name] is null) r.Branches.Add(name, tip);
            }
            // Current branch is "feature" — so neither main nor master is current, and with no
            // origin/HEAD the resolved default falls back to "main" (leaving "master" unprotected
            // before the fix).
            Commands.Checkout(r, r.Branches["feature"]);
        }

        var outcome = await new RepoOperations(new GitFacade())
            .PruneAsync(repo, includeHealthyTracked: true, CancellationToken.None);

        Assert.Equal(RepoOutcomeKind.Processed, outcome.Kind);
        var remaining = RemainingBranchNames(repo.FullPath);
        Assert.Contains("main", remaining);
        Assert.Contains("master", remaining);   // the discriminating assertion for the fix
        Assert.Contains("feature", remaining);  // current is always kept
        Assert.DoesNotContain("junk", remaining);
    }
}
