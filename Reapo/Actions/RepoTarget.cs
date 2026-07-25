using Reapo.Discovery;

namespace Reapo.Actions;

public abstract record RepoTarget
{
    private RepoTarget() { }

    public sealed record Single(RepoInfo Repo) : RepoTarget;

    public sealed record All(IReadOnlyList<RepoInfo> Repos) : RepoTarget;
}
