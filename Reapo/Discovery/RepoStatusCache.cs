using Reapo.Git;

namespace Reapo.Discovery;

public sealed class RepoStatusCache
{
    private readonly object _lock = new();
    private Dictionary<string, QuickStatus?> _snapshot = new();

    public IReadOnlyDictionary<string, QuickStatus?> Snapshot
    {
        get { lock (_lock) return new Dictionary<string, QuickStatus?>(_snapshot); }
    }

    public void Refresh(IReadOnlyList<RepoInfo> repos, GitFacade git)
    {
        var fresh = new Dictionary<string, QuickStatus?>();
        Parallel.ForEach(repos, repo =>
        {
            QuickStatus? status = null;
            try { status = git.GetQuickStatus(repo.FullPath); }
            catch { /* tolerated; rendered as ? */ }
            lock (fresh) { fresh[repo.FullPath] = status; }
        });

        lock (_lock) { _snapshot = fresh; }
    }

    public void RefreshOne(RepoInfo repo, GitFacade git)
    {
        QuickStatus? status = null;
        try { status = git.GetQuickStatus(repo.FullPath); }
        catch { /* tolerated; rendered as ? */ }

        // Copy-on-write, matching Refresh: the referenced dictionary is never mutated in place, so
        // any snapshot handed out stays valid and reads need only guard the reference swap.
        lock (_lock)
        {
            _snapshot = new Dictionary<string, QuickStatus?>(_snapshot) { [repo.FullPath] = status };
        }
    }

    public bool IsEmpty
    {
        get { lock (_lock) return _snapshot.Count == 0; }
    }
}
