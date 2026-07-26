using LibGit2Sharp;
using Reapo.Discovery;
using Reapo.Git;
using Reapo.Tests.Support;

namespace Reapo.Tests.Discovery;

public sealed class RepoStatusCacheTests : GitRepoTestBase
{
    [Fact]
    public void New_cache_is_empty_and_snapshot_returns_empty_dictionary()
    {
        var cache = new RepoStatusCache();
        Assert.True(cache.IsEmpty);
        Assert.Empty(cache.Snapshot);
    }

    [Fact]
    public void Refresh_populates_cache_with_status_for_each_repo()
    {
        var pathA = InitRepo("a");
        var pathB = InitRepo("b");
        foreach (var p in new[] { pathA, pathB })
        {
            using var r = new Repository(p);
            SetIdentity(r);
            CommitFile(r, "x.txt", "hi", "init");
        }

        var cache = new RepoStatusCache();
        cache.Refresh(
            new[] { new RepoInfo("a", pathA), new RepoInfo("b", pathB) },
            new GitFacade());

        Assert.False(cache.IsEmpty);
        var snap = cache.Snapshot;
        Assert.Equal(2, snap.Count);
        Assert.NotNull(snap[pathA]);
        Assert.NotNull(snap[pathB]);
    }

    [Fact]
    public void Refresh_tolerates_broken_repo_paths_by_storing_null()
    {
        var goodPath = InitRepo("good");
        using (var r = new Repository(goodPath))
        {
            SetIdentity(r);
            CommitFile(r, "x.txt", "hi", "init");
        }
        var bogusPath = Path.Combine(Root, "not-a-repo");

        var cache = new RepoStatusCache();
        cache.Refresh(
            new[] { new RepoInfo("good", goodPath), new RepoInfo("bogus", bogusPath) },
            new GitFacade());

        var snap = cache.Snapshot;
        Assert.NotNull(snap[goodPath]);
        Assert.Null(snap[bogusPath]);
    }

    [Fact]
    public void Snapshot_is_defensive_copy_untouched_by_further_refreshes()
    {
        var pathA = InitRepo("a");
        using (var r = new Repository(pathA))
        {
            SetIdentity(r);
            CommitFile(r, "x.txt", "hi", "init");
        }

        var cache = new RepoStatusCache();
        cache.Refresh(new[] { new RepoInfo("a", pathA) }, new GitFacade());

        var before = cache.Snapshot;
        Assert.Single(before);

        var pathB = InitRepo("b");
        using (var r = new Repository(pathB))
        {
            SetIdentity(r);
            CommitFile(r, "x.txt", "hi", "init");
        }
        cache.RefreshOne(new RepoInfo("b", pathB), new GitFacade());

        Assert.Single(before);                    // caller's copy untouched
        Assert.Equal(2, cache.Snapshot.Count);    // new snapshot reflects the change
    }

    [Fact]
    public void RefreshOne_updates_single_entry_without_disturbing_others()
    {
        var pathA = InitRepo("a");
        var pathB = InitRepo("b");
        foreach (var p in new[] { pathA, pathB })
        {
            using var r = new Repository(p);
            SetIdentity(r);
            CommitFile(r, "x.txt", "hi", "init");
        }

        var cache = new RepoStatusCache();
        cache.Refresh(
            new[] { new RepoInfo("a", pathA), new RepoInfo("b", pathB) },
            new GitFacade());

        // Make a dirty change in repo B so its status has DirtyCount > 0 after refresh.
        File.WriteAllText(Path.Combine(pathB, "scratch.txt"), "dirty");
        cache.RefreshOne(new RepoInfo("b", pathB), new GitFacade());

        var snap = cache.Snapshot;
        Assert.Equal(0, snap[pathA]!.DirtyCount);
        Assert.True(snap[pathB]!.DirtyCount > 0);
    }
}
