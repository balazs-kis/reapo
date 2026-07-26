using Reapo.Discovery;
using Reapo.Git;

namespace Reapo.Actions;

/// <summary>
/// The full set of repo actions, expressed as data. Each spec becomes one <see cref="RepoAction"/>.
/// Actions identical across the single- and all-repos menus (the prune actions) are declared once
/// with <see cref="AppliesTo.Both"/>.
/// </summary>
public static class RepoActionCatalog
{
    private static readonly HashSet<string> MainBranchNames =
        new(StringComparer.OrdinalIgnoreCase) { "main", "master" };

    public static IReadOnlyList<IRepoAction> Create(GitFacade git, RepoStatusCache cache) =>
        Specs.Select(spec => (IRepoAction)new RepoAction(spec, git, cache)).ToList();

    internal static readonly IReadOnlyList<RepoActionSpec> Specs =
    [
        // ---- All repos ------------------------------------------------------
        new RepoActionSpec(
            Name: "Fetch status",
            Description: "Fetch from origin in every repo, then refresh statuses.",
            Severity: ActionSeverity.Safe,
            AppliesTo: AppliesTo.All,
            Run: (repo, ops, ct) => ops.FetchAsync(repo, ct),
            ShowSummary: false),

        new RepoActionSpec(
            Name: "Update clean repos",
            Description: "Pull every tracked branch, only if the worktree is clean.",
            Severity: ActionSeverity.Safe,
            AppliesTo: AppliesTo.All,
            Run: (repo, ops, ct) => ops.UpdateAsync(repo, useStashMechanic: false, ct),
            SkipReason: s => s.DirtyCount > 0 ? "dirty (action requires clean)" : null),

        new RepoActionSpec(
            Name: "Update all repos",
            Description: "Stash if dirty, pull every tracked branch, then unstash.",
            Severity: ActionSeverity.Risky,
            AppliesTo: AppliesTo.All,
            Run: (repo, ops, ct) => ops.UpdateAsync(repo, useStashMechanic: true, ct)),

        new RepoActionSpec(
            Name: "Update clean repos on main",
            Description: "Pull every tracked branch, only if clean and currently on main/master.",
            Severity: ActionSeverity.Safe,
            AppliesTo: AppliesTo.All,
            Run: (repo, ops, ct) => ops.UpdateAsync(repo, useStashMechanic: false, ct),
            SkipReason: s => !MainBranchNames.Contains(s.Branch)
                ? "not on main/master"
                : s.DirtyCount > 0 ? "dirty (action requires clean)" : null),

        new RepoActionSpec(
            Name: "Update all repos on main",
            Description: "Stash if dirty, pull every tracked branch, then unstash. Only repos on main/master.",
            Severity: ActionSeverity.Risky,
            AppliesTo: AppliesTo.All,
            Run: (repo, ops, ct) => ops.UpdateAsync(repo, useStashMechanic: true, ct),
            SkipReason: s => MainBranchNames.Contains(s.Branch) ? null : "not on main/master"),

        // ---- Single repo (dangerous) ---------------------------------------
        new RepoActionSpec(
            Name: "Discard local changes",
            Description: "Reset tracked files to HEAD and delete untracked files. Git-ignored files are left untouched.",
            Severity: ActionSeverity.Dangerous,
            AppliesTo: AppliesTo.Single,
            Run: (repo, ops, ct) => ops.DiscardAsync(repo, ct),
            RunningLabel: repo => $"Discarding local changes in {repo.Name}..."),

        // ---- Both (single + all) -------------------------------------------
        new RepoActionSpec(
            Name: "Prune untracked branches",
            Description: "Delete local branches whose upstream is gone or that never had one. Keeps current and main/master.",
            Severity: ActionSeverity.Dangerous,
            AppliesTo: AppliesTo.Both,
            Run: (repo, ops, ct) => ops.PruneAsync(repo, includeHealthyTracked: false, ct),
            RunningLabel: repo => $"Pruning {repo.Name}..."),

        new RepoActionSpec(
            Name: "Prune all but current and main",
            Description: "Delete every local branch except current and main/master, regardless of tracking state.",
            Severity: ActionSeverity.Dangerous,
            AppliesTo: AppliesTo.Both,
            Run: (repo, ops, ct) => ops.PruneAsync(repo, includeHealthyTracked: true, ct),
            RunningLabel: repo => $"Pruning {repo.Name}..."),

        // ---- Single repo ----------------------------------------------------
        new RepoActionSpec(
            Name: "Fetch",
            Description: "Fetch from origin and refresh the branch view.",
            Severity: ActionSeverity.Safe,
            AppliesTo: AppliesTo.Single,
            Run: (repo, ops, ct) => ops.FetchAsync(repo, ct),
            RunningLabel: repo => $"Fetching {repo.Name}..."),

        new RepoActionSpec(
            Name: "Update",
            Description: "Fetch, then pull every tracked branch. Stashes and restores uncommitted changes if needed.",
            Severity: ActionSeverity.Risky,
            AppliesTo: AppliesTo.Single,
            Run: (repo, ops, ct) => ops.UpdateAsync(repo, useStashMechanic: true, ct),
            RunningLabel: repo => $"Updating {repo.Name}..."),

        new RepoActionSpec(
            Name: "Switch to main",
            Description: "Switch to main/master. Stashes and restores uncommitted changes if needed.",
            Severity: ActionSeverity.Risky,
            AppliesTo: AppliesTo.Single,
            Run: (repo, ops, ct) => ops.SwitchToMainAsync(repo, ct),
            RunningLabel: repo => $"Switching {repo.Name} to main..."),
    ];
}
