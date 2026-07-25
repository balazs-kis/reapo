using LibGit2Sharp;
using Reapo.Git;

namespace Reapo.Actions;

public sealed class BranchUpdater
{
    private readonly GitFacade _git;

    public BranchUpdater(GitFacade git)
    {
        _git = git;
    }

    public async Task<IReadOnlyList<BranchOutcome>> UpdateAsync(string repoPath, CancellationToken ct)
    {
        var snapshot = SnapshotBranches(repoPath);
        var results = new List<BranchOutcome>();

        foreach (var b in snapshot)
        {
            if (!b.HasRemote) continue; // local-only: skipped silently

            if (b.IsCurrent)
            {
                results.Add(await UpdateCurrentAsync(repoPath, b, ct));
            }
            else
            {
                results.Add(await FastForwardOtherAsync(repoPath, b, ct));
            }
        }

        return results;
    }

    private async Task<BranchOutcome> UpdateCurrentAsync(string repoPath, BranchSnapshot b, CancellationToken ct)
    {
        if (b.BehindBy == 0)
        {
            return new BranchOutcome(b.Name, BranchOutcomeKind.AlreadyUpToDate, null);
        }

        try
        {
            await _git.PullRebaseAsync(repoPath, ct);
            return new BranchOutcome(b.Name, BranchOutcomeKind.Updated, null);
        }
        catch (GitProcessException ex)
        {
            // If a rebase is in progress, abort it so subsequent branches can run.
            try { await _git.RawAsync(repoPath, ["rebase", "--abort"], CancellationToken.None); } catch { }
            var isConflict = ex.Stderr.Contains("conflict", StringComparison.OrdinalIgnoreCase)
                          || ex.Stderr.Contains("CONFLICT",  StringComparison.Ordinal);
            var detail = isConflict ? "rebase conflict; see git status" : ex.Stderr;
            return new BranchOutcome(b.Name, BranchOutcomeKind.Failed, detail);
        }
    }

    private async Task<BranchOutcome> FastForwardOtherAsync(string repoPath, BranchSnapshot b, CancellationToken ct)
    {
        if (b.BehindBy == 0)
        {
            return new BranchOutcome(b.Name, BranchOutcomeKind.AlreadyUpToDate, null);
        }

        try
        {
            await _git.FastForwardAsync(repoPath, b.CanonicalName, b.UpstreamCanonicalName!, ct);
            return new BranchOutcome(b.Name, BranchOutcomeKind.Updated, null);
        }
        catch (GitProcessException ex)
        {
            // FF rejection produces "non-fast-forward" / "rejected" in stderr.
            var stderr = ex.Stderr;
            var isDiverged = stderr.Contains("non-fast-forward", StringComparison.Ordinal)
                          || stderr.Contains("rejected",          StringComparison.Ordinal);
            var detail = isDiverged ? "diverged" : stderr;
            return new BranchOutcome(b.Name, BranchOutcomeKind.Failed, detail);
        }
    }

    private static IReadOnlyList<BranchSnapshot> SnapshotBranches(string repoPath)
    {
        using var repo = new Repository(repoPath);
        var headCanonical = repo.Head?.CanonicalName;
        var rows = new List<BranchSnapshot>();
        foreach (var b in repo.Branches.Where(x => !x.IsRemote))
        {
            var hasRemote = b.TrackedBranch != null;
            var tracking  = hasRemote ? b.TrackingDetails : null;
            rows.Add(new BranchSnapshot(
                Name: b.FriendlyName,
                CanonicalName: b.CanonicalName,
                UpstreamCanonicalName: b.TrackedBranch?.CanonicalName,
                IsCurrent: b.CanonicalName == headCanonical,
                HasRemote: hasRemote,
                BehindBy: tracking?.BehindBy ?? 0));
        }
        return rows;
    }

    private sealed record BranchSnapshot(
        string  Name,
        string  CanonicalName,
        string? UpstreamCanonicalName,
        bool    IsCurrent,
        bool    HasRemote,
        int     BehindBy);
}
