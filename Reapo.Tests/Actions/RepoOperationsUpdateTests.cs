using LibGit2Sharp;
using Reapo.Actions;
using Reapo.Discovery;
using Reapo.Git;
using Reapo.Tests.Support;

namespace Reapo.Tests.Actions;

public sealed class RepoOperationsUpdateTests : GitRepoTestBase
{
    [Fact]
    public async Task Stash_pop_conflict_reports_processed_with_a_failed_stash_step_not_a_total_failure()
    {
        InitBareRepo("remote.git");
        var bare = Path.Combine(Root, "remote.git");

        var repoPath = InitRepo("repo");
        string defaultBranch;
        using (var repo = new Repository(repoPath))
        {
            SetIdentity(repo);
            CommitFile(repo, "x.txt", "base\n", "init");
            defaultBranch = repo.Head.FriendlyName;
            var remote = repo.Network.Remotes.Add("origin", bare);
            repo.Network.Push(remote, repo.Head.CanonicalName);
            repo.Branches.Update(repo.Head, b => b.TrackedBranch = $"refs/remotes/origin/{repo.Head.FriendlyName}");
        }

        // A side clone pushes a change to the SAME line, so local will be behind by one commit.
        var sideClonePath = Path.Combine(Root, "side");
        Repository.Clone(bare, sideClonePath);
        using (var side = new Repository(sideClonePath))
        {
            SetIdentity(side);
            CommitFile(side, "x.txt", "base\nremote-change\n", "remote edit");
            side.Network.Push(side.Network.Remotes["origin"], side.Head.CanonicalName);
        }

        // Local uncommitted edit to the same line: survives the stash, conflicts when popped
        // back onto the freshly-pulled remote content.
        File.WriteAllText(Path.Combine(repoPath, "x.txt"), "base\nlocal-change\n");

        var outcome = await new RepoOperations(new GitFacade())
            .UpdateAsync(new RepoInfo("repo", repoPath), useStashMechanic: true, CancellationToken.None);

        // The pull succeeded; only restoring the stash failed — so the repo is Processed (partial),
        // not Failed, and the stuck stash is surfaced as a failed step.
        Assert.Equal(RepoOutcomeKind.Processed, outcome.Kind);
        Assert.Contains(outcome.Branches, b => b.Kind == BranchOutcomeKind.Updated && b.Branch == defaultBranch);

        var stashStep = Assert.Single(outcome.Branches, b => b.Branch == "stash pop");
        Assert.Equal(BranchOutcomeKind.Failed, stashStep.Kind);
    }

    [Fact]
    public async Task Clean_repo_up_to_date_reports_processed_with_no_failed_steps()
    {
        InitBareRepo("remote.git");
        var bare = Path.Combine(Root, "remote.git");

        var repoPath = InitRepo("repo");
        using (var repo = new Repository(repoPath))
        {
            SetIdentity(repo);
            CommitFile(repo, "x.txt", "base\n", "init");
            var remote = repo.Network.Remotes.Add("origin", bare);
            repo.Network.Push(remote, repo.Head.CanonicalName);
            repo.Branches.Update(repo.Head, b => b.TrackedBranch = $"refs/remotes/origin/{repo.Head.FriendlyName}");
        }

        var outcome = await new RepoOperations(new GitFacade())
            .UpdateAsync(new RepoInfo("repo", repoPath), useStashMechanic: true, CancellationToken.None);

        Assert.Equal(RepoOutcomeKind.Processed, outcome.Kind);
        Assert.DoesNotContain(outcome.Branches, b => b.Kind == BranchOutcomeKind.Failed);
    }
}
