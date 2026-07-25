using Reapo.Ui;

namespace Reapo.Actions;

[Flags]
public enum AppliesTo
{
    Single  = 1 << 0,
    All     = 1 << 1,
    Both    = Single | All,
}

public enum ActionSeverity
{
    Safe = 0,
    Risky = 1,
    Dangerous = 2,
}

public interface IRepoAction
{
    string Name { get; }

    string Description { get; }

    AppliesTo AppliesTo { get; }

    ActionSeverity Severity => ActionSeverity.Safe;

    Task ExecuteAsync(RepoTarget target, IActionUi ui, CancellationToken ct);
}
