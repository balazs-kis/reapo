using LibGit2Sharp;
using Reapo.Git;
using Reapo.Tests.Support;

namespace Reapo.Tests.Git;

public sealed class GitFacadeDirtyCountTests : GitRepoTestBase
{
    [Fact]
    public void Dirty_count_reflects_untracked_and_modified_files()
    {
        var repoPath = InitRepo("repo");
        using (var repo = new Repository(repoPath))
        {
            SetIdentity(repo);
            CommitFile(repo, "tracked.txt", "hi", "init");
        }

        File.WriteAllText(Path.Combine(repoPath, "tracked.txt"), "dirty\n");
        File.WriteAllText(Path.Combine(repoPath, "new.txt"), "wip\n");

        var status = new GitFacade().GetQuickStatus(repoPath);
        Assert.Equal(2, status.DirtyCount);
    }

    [Fact]
    public void Clean_repo_has_zero_dirty_count()
    {
        var repoPath = InitRepo("repo");
        using (var repo = new Repository(repoPath))
        {
            SetIdentity(repo);
            CommitFile(repo, "tracked.txt", "hi", "init");
        }

        var status = new GitFacade().GetQuickStatus(repoPath);
        Assert.Equal(0, status.DirtyCount);
    }

    [Fact]
    public void Files_ignored_via_repo_gitignore_do_not_count()
    {
        var repoPath = InitRepo("repo");
        using (var repo = new Repository(repoPath))
        {
            SetIdentity(repo);
            File.WriteAllText(Path.Combine(repoPath, ".gitignore"), ".DS_Store\n*.log\n");
            CommitFile(repo, ".gitignore", ".DS_Store\n*.log\n", "init ignore");
        }

        File.WriteAllText(Path.Combine(repoPath, ".DS_Store"), "junk");
        File.WriteAllText(Path.Combine(repoPath, "debug.log"), "noise");

        var status = new GitFacade().GetQuickStatus(repoPath);
        Assert.Equal(0, status.DirtyCount);
    }

    [Fact]
    public void Files_ignored_via_global_excludesfile_do_not_count()
    {
        // This is the DS_Store bug: LibGit2Sharp historically counted files that git itself would
        // treat as ignored via core.excludesfile. Point a repo-local core.excludesfile at a file
        // ignoring .DS_Store and verify a stray .DS_Store isn't counted.
        var repoPath = InitRepo("repo");
        var excludesFile = Path.Combine(Root, "global-ignore");
        File.WriteAllText(excludesFile, ".DS_Store\n");

        using (var repo = new Repository(repoPath))
        {
            SetIdentity(repo);
            CommitFile(repo, "tracked.txt", "hi", "init");
            repo.Config.Set("core.excludesfile", excludesFile, ConfigurationLevel.Local);
        }

        File.WriteAllText(Path.Combine(repoPath, ".DS_Store"), "macos junk");

        var status = new GitFacade().GetQuickStatus(repoPath);
        Assert.Equal(0, status.DirtyCount);
    }
}
