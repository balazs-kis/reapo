using Reapo.Discovery;

namespace Reapo.Actions;

public enum RepoOutcomeKind
{
    Processed,
    Skipped,
    Failed,
}

public sealed record RepoOutcome(
    RepoInfo                       Repo,
    RepoOutcomeKind                Kind,
    string?                        Detail,
    IReadOnlyList<BranchOutcome>   Branches);
