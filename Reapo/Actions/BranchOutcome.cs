namespace Reapo.Actions;

public enum BranchOutcomeKind
{
    Updated,
    AlreadyUpToDate,
    Failed,
}

public sealed record BranchOutcome(string Branch, BranchOutcomeKind Kind, string? Detail);
