using Reapo.Discovery;
using Reapo.Git;

namespace Reapo.Actions;

public sealed class RepoOperations
{
    private readonly GitFacade _git;

    public RepoOperations(GitFacade git) => _git = git;

    public async Task<RepoOutcome> FetchAsync(RepoInfo repo, CancellationToken ct)
    {
        try
        {
            await _git.FetchAsync(repo.FullPath, ct);
            return new RepoOutcome(repo, RepoOutcomeKind.Processed, null, []);
        }
        catch (GitProcessException ex)
        {
            return new RepoOutcome(repo, RepoOutcomeKind.Failed, $"fetch failed: {ex.Stderr}", []);
        }
    }

    public async Task<RepoOutcome> DiscardAsync(RepoInfo repo, CancellationToken ct)
    {
        try
        {
            await _git.DiscardLocalChangesAsync(repo.FullPath, ct);
            return new RepoOutcome(repo, RepoOutcomeKind.Processed, null, []);
        }
        catch (GitProcessException ex)
        {
            return new RepoOutcome(repo, RepoOutcomeKind.Failed, $"discard failed: {ex.Stderr}", []);
        }
    }

    public async Task<RepoOutcome> UpdateAsync(RepoInfo repo, bool useStashMechanic, CancellationToken ct)
    {
        QuickStatus status;
        try
        {
            status = _git.GetQuickStatus(repo.FullPath);
        }
        catch (Exception ex)
        {
            return new RepoOutcome(repo, RepoOutcomeKind.Failed, ex.Message, []);
        }

        try
        {
            await _git.FetchAsync(repo.FullPath, ct);
        }
        catch (GitProcessException ex)
        {
            return new RepoOutcome(repo, RepoOutcomeKind.Failed, $"fetch failed: {ex.Stderr}", []);
        }

        var stashed = false;
        if (useStashMechanic && status.DirtyCount > 0)
        {
            try
            {
                stashed = await _git.StashPushAsync(repo.FullPath, ct);
            }
            catch (GitProcessException ex)
            {
                return new RepoOutcome(repo, RepoOutcomeKind.Failed, $"stash push failed: {ex.Stderr}", []);
            }
        }

        IReadOnlyList<BranchOutcome> branches;
        try
        {
            var updater = new BranchUpdater(_git);
            branches = await updater.UpdateAsync(repo.FullPath, ct);
        }
        catch (Exception ex)
        {
            if (stashed)
            {
                try { await _git.StashPopAsync(repo.FullPath, CancellationToken.None); } catch { }
            }
            return new RepoOutcome(repo, RepoOutcomeKind.Failed, ex.Message, []);
        }

        if (stashed)
        {
            try
            {
                await _git.StashPopAsync(repo.FullPath, CancellationToken.None);
            }
            catch (GitProcessException ex)
            {
                // The branches were updated successfully; only restoring the stash failed. Report the
                // repo as processed (not a total failure) but surface the stuck stash as a failed step
                // so it shows as "partial" in the summary and as an error in the single-repo view.
                var withStashFailure = branches
                    .Append(new BranchOutcome("stash pop", BranchOutcomeKind.Failed,
                        $"conflicted; your changes are stashed — see git stash list: {ex.Stderr}"))
                    .ToList();
                return new RepoOutcome(repo, RepoOutcomeKind.Processed, null, withStashFailure);
            }
        }

        return new RepoOutcome(repo, RepoOutcomeKind.Processed, null, branches);
    }

    public async Task<RepoOutcome> SwitchToMainAsync(RepoInfo repo, CancellationToken ct)
    {
        string? target;
        QuickStatus status;
        try
        {
            target = _git.GetDefaultBranchName(repo.FullPath);
            status = _git.GetQuickStatus(repo.FullPath);
        }
        catch (Exception ex)
        {
            return new RepoOutcome(repo, RepoOutcomeKind.Failed, ex.Message, []);
        }

        if (target is null)
        {
            return new RepoOutcome(repo, RepoOutcomeKind.Failed, "no main/master branch found", []);
        }

        if (string.Equals(status.Branch, target, StringComparison.Ordinal))
        {
            return new RepoOutcome(repo, RepoOutcomeKind.Processed, null,
                [new BranchOutcome(target, BranchOutcomeKind.AlreadyUpToDate, "already on branch")]);
        }

        var stashed = false;
        if (status.DirtyCount > 0)
        {
            try
            {
                stashed = await _git.StashPushAsync(repo.FullPath, ct);
            }
            catch (GitProcessException ex)
            {
                return new RepoOutcome(repo, RepoOutcomeKind.Failed, $"stash push failed: {ex.Stderr}", []);
            }
        }

        try
        {
            await _git.SwitchAsync(repo.FullPath, target, ct);
        }
        catch (GitProcessException ex)
        {
            if (stashed)
            {
                try { await _git.StashPopAsync(repo.FullPath, CancellationToken.None); } catch { }
            }
            return new RepoOutcome(repo, RepoOutcomeKind.Failed, $"switch failed: {ex.Stderr}", []);
        }

        if (stashed)
        {
            try
            {
                await _git.StashPopAsync(repo.FullPath, CancellationToken.None);
            }
            catch (GitProcessException ex)
            {
                return new RepoOutcome(repo, RepoOutcomeKind.Processed, null,
                    [
                        new BranchOutcome(target, BranchOutcomeKind.Updated, "switched"),
                        new BranchOutcome("stash pop", BranchOutcomeKind.Failed,
                            $"conflicted; your changes are stashed — see git stash list: {ex.Stderr}"),
                    ]);
            }
        }

        return new RepoOutcome(repo, RepoOutcomeKind.Processed, null,
            [new BranchOutcome(target, BranchOutcomeKind.Updated, "switched")]);
    }

    public async Task<RepoOutcome> PruneAsync(RepoInfo repo, bool includeHealthyTracked, CancellationToken ct)
    {
        IReadOnlyList<BranchInfo> branches;
        string? defaultBranch;
        try
        {
            branches = _git.GetBranches(repo.FullPath);
            defaultBranch = _git.GetDefaultBranchName(repo.FullPath);
        }
        catch (Exception ex)
        {
            return new RepoOutcome(repo, RepoOutcomeKind.Failed, ex.Message, []);
        }

        // Always protect the current branch, the resolved default, and main/master — even when
        // origin/HEAD resolves elsewhere — so the action never deletes what its name promises to keep.
        var protectedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "main", "master" };
        var current = branches.FirstOrDefault(b => b.IsCurrent);
        if (current != null) protectedNames.Add(current.Name);
        if (defaultBranch != null) protectedNames.Add(defaultBranch);

        var outcomes = new List<BranchOutcome>();
        foreach (var b in branches)
        {
            if (protectedNames.Contains(b.Name)) continue;

            var qualifies = includeHealthyTracked || !b.HasRemote || b.IsUpstreamGone;
            if (!qualifies) continue;

            try
            {
                await _git.DeleteBranchAsync(repo.FullPath, b.Name, ct);
                outcomes.Add(new BranchOutcome(b.Name, BranchOutcomeKind.Updated, "deleted"));
            }
            catch (GitProcessException ex)
            {
                outcomes.Add(new BranchOutcome(b.Name, BranchOutcomeKind.Failed, ex.Stderr));
            }
        }

        return new RepoOutcome(repo, RepoOutcomeKind.Processed, null, outcomes);
    }
}
