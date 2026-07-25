using LibGit2Sharp;

namespace Reapo.Tests.Support;

/// <summary>
/// Shared scaffolding for tests that build real git repositories in a throwaway temp directory.
/// Provides the temp-root lifecycle (with retry-delete for OneDrive/Windows file locks) plus the
/// common repo-construction helpers.
/// </summary>
public abstract class GitRepoTestBase : IDisposable
{
    protected string Root { get; }

    protected GitRepoTestBase()
    {
        Root = Path.Combine(Path.GetTempPath(), "ReapoTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    public void Dispose()
    {
        if (Directory.Exists(Root)) DeleteWithRetries(Root);
        GC.SuppressFinalize(this);
    }

    private static void DeleteWithRetries(string path)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try { Directory.Delete(path, recursive: true); return; }
            catch (IOException) { Thread.Sleep(50); }
            catch (UnauthorizedAccessException) { Thread.Sleep(50); }
        }
    }

    protected static readonly Identity Author = new("test", "test@example.com");

    protected string InitRepo(string name)
    {
        var path = Path.Combine(Root, name);
        Repository.Init(path);
        return path;
    }

    protected string InitBareRepo(string name)
    {
        var path = Path.Combine(Root, name);
        Repository.Init(path, isBare: true);
        return path;
    }

    protected static void SetIdentity(Repository repo)
    {
        repo.Config.Set("user.name", "test", ConfigurationLevel.Local);
        repo.Config.Set("user.email", "test@example.com", ConfigurationLevel.Local);
    }

    protected static Commit CommitFile(Repository repo, string fileName, string contents, string message)
    {
        var fullPath = Path.Combine(repo.Info.WorkingDirectory, fileName);
        File.WriteAllText(fullPath, contents);
        Commands.Stage(repo, fileName);
        var sig = new Signature(Author, DateTimeOffset.UtcNow);
        return repo.Commit(message, sig, sig);
    }

    protected static void PushAndTrack(Repository repo, Branch branch)
    {
        var remote = repo.Network.Remotes["origin"];
        repo.Network.Push(remote, branch.CanonicalName);
        repo.Branches.Update(branch, b => b.TrackedBranch = $"refs/remotes/origin/{branch.FriendlyName}");
    }
}
