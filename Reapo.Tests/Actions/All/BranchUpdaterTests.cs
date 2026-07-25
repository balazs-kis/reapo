using LibGit2Sharp;
using Reapo.Actions;
using Reapo.Git;
using Reapo.Tests.Support;
using BranchUpdater = Reapo.Actions.BranchUpdater;

namespace Reapo.Tests.Actions.All;

public sealed class BranchUpdaterTests : GitRepoTestBase
{

    [Fact]
    public async Task Skips_local_only_branch()
    {
        var repoPath = Path.Combine(Root, "repo");
        Repository.Init(repoPath);
        using (var repo = new Repository(repoPath))
        {
            CommitFile(repo, "a.txt", "hi", "init");
        }

        var updater = new BranchUpdater(new GitFacade());
        var outcomes = await updater.UpdateAsync(repoPath, CancellationToken.None);

        // Local-only branch is skipped silently — no outcome row.
        Assert.Empty(outcomes);
    }

    [Fact]
    public async Task Tracked_diverged_non_current_branch_is_failed_with_diverged_detail()
    {
        var bare    = Path.Combine(Root, "remote.git");
        var repoPath = Path.Combine(Root, "repo");
        Repository.Init(bare, isBare: true);
        Repository.Init(repoPath);

        using (var repo = new Repository(repoPath))
        {
            // Initial commit on the default branch (refs/heads/master or refs/heads/main).
            var initial = CommitFile(repo, "a.txt", "hi", "init");
            var defaultBranch = repo.Head.FriendlyName;

            // Create a feature branch with a unique commit, push it, then add a different commit locally and remotely.
            var feature = repo.Branches.Add("feature", initial);
            Commands.Checkout(repo, feature);
            CommitFile(repo, "b.txt", "feat-1", "feat-1-local");

            var remote = repo.Network.Remotes.Add("origin", bare);
            repo.Network.Push(remote, feature.CanonicalName);
            repo.Branches.Update(feature, b => b.TrackedBranch = "refs/remotes/origin/feature");

            // Diverge: add another local commit, and add a separate one to the bare remote via a working clone.
            CommitFile(repo, "c.txt", "feat-2", "feat-2-local-only");

            // Switch back to default branch so feature is non-current.
            Commands.Checkout(repo, repo.Branches[defaultBranch]);

            // Add a separate commit to the remote feature branch so FF would be non-FF.
            var sideClonePath = Path.Combine(Root, "side-clone");
            Repository.Clone(bare, sideClonePath);
            using (var side = new Repository(sideClonePath))
            {
                Commands.Checkout(side, side.Branches["origin/feature"].FriendlyName);
                // Create a local branch tracking origin/feature.
                var localFeature = side.Branches.Add("feature", side.Head.Tip);
                Commands.Checkout(side, localFeature);
                CommitFile(side, "d.txt", "remote-feat", "from-side");
                var sideRemote = side.Network.Remotes["origin"];
                side.Network.Push(sideRemote, "refs/heads/feature");
            }

            // Refresh local refs from the bare remote.
            Commands.Fetch(repo, "origin", [], null, null);
        }

        var updater = new BranchUpdater(new GitFacade());
        var outcomes = await updater.UpdateAsync(repoPath, CancellationToken.None);

        var feat = outcomes.SingleOrDefault(o => o.Branch == "feature");
        Assert.NotNull(feat);
        Assert.Equal(BranchOutcomeKind.Failed, feat!.Kind);
        Assert.Equal("diverged", feat.Detail);
    }

    [Fact]
    public async Task Tracked_non_current_branch_behind_upstream_is_fast_forwarded()
    {
        var bare     = Path.Combine(Root, "remote.git");
        var repoPath = Path.Combine(Root, "repo");
        Repository.Init(bare, isBare: true);
        Repository.Init(repoPath);

        using (var repo = new Repository(repoPath))
        {
            var initial = CommitFile(repo, "a.txt", "hi", "init");
            var defaultBranch = repo.Head.FriendlyName;

            var feature = repo.Branches.Add("feature", initial);
            Commands.Checkout(repo, feature);

            var remote = repo.Network.Remotes.Add("origin", bare);
            repo.Network.Push(remote, feature.CanonicalName);
            repo.Branches.Update(feature, b => b.TrackedBranch = "refs/remotes/origin/feature");

            // Switch to default branch (so 'feature' is non-current).
            Commands.Checkout(repo, repo.Branches[defaultBranch]);

            // Push a new commit to origin/feature from a side clone so local 'feature' is behind.
            var sideClonePath = Path.Combine(Root, "side-clone");
            Repository.Clone(bare, sideClonePath);
            using (var side = new Repository(sideClonePath))
            {
                Commands.Checkout(side, side.Branches["origin/feature"].FriendlyName);
                var localFeature = side.Branches.Add("feature", side.Head.Tip);
                Commands.Checkout(side, localFeature);
                CommitFile(side, "f.txt", "remote-only", "from-side");
                var sideRemote = side.Network.Remotes["origin"];
                side.Network.Push(sideRemote, "refs/heads/feature");
            }

            Commands.Fetch(repo, "origin", [], null, null);
        }

        var updater = new BranchUpdater(new GitFacade());
        var outcomes = await updater.UpdateAsync(repoPath, CancellationToken.None);

        var feat = outcomes.SingleOrDefault(o => o.Branch == "feature");
        Assert.NotNull(feat);
        Assert.Equal(BranchOutcomeKind.Updated, feat!.Kind);
    }
}
