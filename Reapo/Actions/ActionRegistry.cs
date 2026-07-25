namespace Reapo.Actions;

public sealed class ActionRegistry
{
    private readonly IReadOnlyList<IRepoAction> _actions;

    public ActionRegistry(IEnumerable<IRepoAction> actions)
    {
        _actions = actions.ToList();
    }

    public IReadOnlyList<IRepoAction> GetActionsFor(RepoTarget target)
    {
        var required = target is RepoTarget.Single ? AppliesTo.Single : AppliesTo.All;
        return _actions.Where(a => (a.AppliesTo & required) != 0).ToList();
    }
}
