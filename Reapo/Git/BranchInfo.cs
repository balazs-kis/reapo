namespace Reapo.Git;

public sealed record BranchInfo(
    string Name,
    bool   IsCurrent,
    bool   HasRemote,
    int?   AheadBy,
    int?   BehindBy,
    int    DirtyCount)
{
    /// <summary>Tracking is configured but the upstream ref no longer resolves (deleted, or never fetched).</summary>
    public bool IsUpstreamGone => HasRemote && BehindBy is null && AheadBy is null;
}
