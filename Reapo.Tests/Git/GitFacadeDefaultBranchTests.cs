using LibGit2Sharp;
using Reapo.Git;
using Reapo.Tests.Support;

namespace Reapo.Tests.Git;

public sealed class GitFacadeDefaultBranchTests : GitRepoTestBase
{
    [Fact]
    public void Falls_back_to_local_main_when_no_origin_HEAD()
    {
        var repoPath = InitRepo("repo");
        using (var repo = new Repository(repoPath))
        {
            SetIdentity(repo);
            CommitFile(repo, "x.txt", "hi", "init");
            if (repo.Head.FriendlyName != "main")
            {
                var main = repo.CreateBranch("main");
                Commands.Checkout(repo, main);
            }
        }

        Assert.Equal("main", new GitFacade().GetDefaultBranchName(repoPath));
    }

    [Fact]
    public void Falls_back_to_local_master_when_no_main_and_no_origin_HEAD()
    {
        var repoPath = InitRepo("repo");
        using (var repo = new Repository(repoPath))
        {
            SetIdentity(repo);
            CommitFile(repo, "x.txt", "hi", "init");
            if (repo.Head.FriendlyName != "master")
            {
                var master = repo.CreateBranch("master");
                Commands.Checkout(repo, master);
                // Remove whatever the initial HEAD branch was, if it's not master.
                var toRemove = repo.Branches.FirstOrDefault(b => !b.IsRemote && b.FriendlyName != "master");
                if (toRemove != null) repo.Branches.Remove(toRemove);
            }
        }

        Assert.Equal("master", new GitFacade().GetDefaultBranchName(repoPath));
    }

    [Fact]
    public void Returns_null_when_no_default_can_be_resolved()
    {
        var repoPath = InitRepo("repo");
        using (var repo = new Repository(repoPath))
        {
            SetIdentity(repo);
            CommitFile(repo, "x.txt", "hi", "init");
            // Rename current branch to something that isn't main/master, and don't create either.
            var other = repo.CreateBranch("dev");
            Commands.Checkout(repo, other);
            var toRemove = repo.Branches
                .Where(b => !b.IsRemote && b.FriendlyName != "dev")
                .ToList();
            foreach (var b in toRemove) repo.Branches.Remove(b);
        }

        Assert.Null(new GitFacade().GetDefaultBranchName(repoPath));
    }

    [Fact]
    public void Resolves_from_origin_HEAD_when_set()
    {
        var bare = InitBareRepo("remote.git");
        var repoPath = InitRepo("repo");
        string defaultBranch;
        using (var repo = new Repository(repoPath))
        {
            SetIdentity(repo);
            CommitFile(repo, "x.txt", "hi", "init");
            defaultBranch = repo.Head.FriendlyName;
            repo.Network.Remotes.Add("origin", bare);
            repo.Network.Push(repo.Network.Remotes["origin"], repo.Head.CanonicalName);
        }

        // Manually create origin/HEAD symbolic ref pointing at defaultBranch, mirroring what a
        // regular clone would set up.
        using (var repo = new Repository(repoPath))
        {
            repo.Refs.Add("refs/remotes/origin/HEAD", $"refs/remotes/origin/{defaultBranch}");
        }

        Assert.Equal(defaultBranch, new GitFacade().GetDefaultBranchName(repoPath));
    }
}
