using LibGit2Sharp;

namespace Reapo.Git;

public sealed record QuickStatus(string Branch, int DirtyCount, bool HasRemote, int? AheadBy, int? BehindBy)
{
    /// <summary>Tracking is configured but the upstream ref no longer resolves (deleted, or never fetched).</summary>
    public bool IsUpstreamGone => HasRemote && BehindBy is null && AheadBy is null;
}

public sealed class GitFacade
{
    private readonly GitProcessRunner _processRunner;

    public GitFacade() : this(new GitProcessRunner()) { }

    public GitFacade(GitProcessRunner processRunner)
    {
        _processRunner = processRunner;
    }

    public QuickStatus GetQuickStatus(string repoPath)
    {
        using var repo = new Repository(repoPath);
        var head = repo.Head;
        var branch = head?.FriendlyName ?? "(detached)";
        var dirtyCount = CountDirtyEntries(repo);

        var hasRemote = head?.TrackedBranch != null;
        var tracking = hasRemote ? head!.TrackingDetails : null;
        return new QuickStatus(branch, dirtyCount, hasRemote, tracking?.AheadBy, tracking?.BehindBy);
    }

    private static int CountDirtyEntries(Repository repo)
    {
        var status = repo.RetrieveStatus(new StatusOptions { IncludeUntracked = true });
        return status.Modified.Count()
             + status.Added.Count()
             + status.Removed.Count()
             + status.Staged.Count()
             + status.Untracked.Count()
             + status.Missing.Count()
             + status.RenamedInIndex.Count()
             + status.RenamedInWorkDir.Count();
    }

    public Task FetchAsync(string repoPath, CancellationToken ct) =>
        _processRunner.RunAsync(repoPath, ["fetch", "--quiet", "--prune"], ct);

    public Task PullRebaseAsync(string repoPath, CancellationToken ct) =>
        _processRunner.RunAsync(repoPath, ["pull", "--rebase"], ct);

    public Task FastForwardAsync(string repoPath, string branchRef, string upstreamRef, CancellationToken ct) =>
        _processRunner.RunAsync(repoPath, ["fetch", ".", $"{upstreamRef}:{branchRef}"], ct);

    public async Task<bool> StashPushAsync(string repoPath, CancellationToken ct)
    {
        var (stdout, stderr) = await _processRunner.RunCaptureAsync(
            repoPath, ["stash", "push", "-u"], ct);
        var combined = stdout + stderr;
        return !combined.Contains("No local changes to save", StringComparison.Ordinal);
    }

    public Task StashPopAsync(string repoPath, CancellationToken ct) =>
        _processRunner.RunAsync(repoPath, ["stash", "pop"], ct);

    public Task RawAsync(string repoPath, IReadOnlyList<string> args, CancellationToken ct) =>
        _processRunner.RunAsync(repoPath, args, ct);

    public Task DeleteBranchAsync(string repoPath, string branchName, CancellationToken ct) =>
        _processRunner.RunAsync(repoPath, ["branch", "-D", branchName], ct);

    public Task SwitchAsync(string repoPath, string branchName, CancellationToken ct) =>
        _processRunner.RunAsync(repoPath, ["switch", branchName], ct);

    public async Task DiscardLocalChangesAsync(string repoPath, CancellationToken ct)
    {
        await _processRunner.RunAsync(repoPath, ["reset", "--hard", "HEAD"], ct);
        await _processRunner.RunAsync(repoPath, ["clean", "-fd"], ct);
    }

    public string? GetDefaultBranchName(string repoPath)
    {
        using var repo = new Repository(repoPath);

        // Primary: origin/HEAD symbolic ref.
        var originHead = repo.Refs["refs/remotes/origin/HEAD"] as SymbolicReference;
        if (originHead?.Target is DirectReference target)
        {
            const string prefix = "refs/remotes/origin/";
            if (target.CanonicalName.StartsWith(prefix, StringComparison.Ordinal))
            {
                return target.CanonicalName[prefix.Length..];
            }
        }

        // Fallback: branch named main or master, if it exists locally.
        if (repo.Branches["main"] != null) return "main";
        if (repo.Branches["master"] != null) return "master";

        return null;
    }

    public IReadOnlyList<BranchInfo> GetBranches(string repoPath)
    {
        using var repo = new Repository(repoPath);

        var headCanonical = repo.Head?.CanonicalName;
        var dirtyCount = CountDirtyEntries(repo);

        var rows = new List<BranchInfo>();
        foreach (var branch in repo.Branches.Where(b => !b.IsRemote))
        {
            var isCurrent = branch.CanonicalName == headCanonical;
            var hasRemote = branch.TrackedBranch != null;
            var tracking = hasRemote ? branch.TrackingDetails : null;
            int? ahead = tracking?.AheadBy;
            int? behind = tracking?.BehindBy;

            rows.Add(new BranchInfo(
                Name:        branch.FriendlyName,
                IsCurrent:   isCurrent,
                HasRemote:   hasRemote,
                AheadBy:     ahead,
                BehindBy:    behind,
                DirtyCount:  isCurrent ? dirtyCount : 0));
        }

        rows.Sort((a, b) =>
        {
            if (a.IsCurrent && !b.IsCurrent) return -1;
            if (!a.IsCurrent && b.IsCurrent) return 1;
            return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        });

        return rows;
    }
}
