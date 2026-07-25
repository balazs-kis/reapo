using Reapo.Discovery;
using Reapo.Git;

namespace Reapo.Actions;

/// <summary>
/// Declarative description of a repo action. One spec drives the shared <see cref="RepoAction"/>
/// executor for either an "all repos" or a "single repo" target (or both).
/// </summary>
/// <param name="Run">The per-repo operation. Runs against one repo; must map failures to a Failed outcome.</param>
/// <param name="SkipReason">
/// All-repos only. Given a repo's quick status, returns a skip reason to pre-filter it, or null to process it.
/// Null means every repo qualifies (no status probe).
/// </param>
/// <param name="ShowSummary">All-repos only. Whether to render the summary table after the run.</param>
/// <param name="RunningLabel">Single-repo only. Status-spinner label; defaults to the action name.</param>
public sealed record RepoActionSpec(
    string                                                          Name,
    string                                                          Description,
    ActionSeverity                                                  Severity,
    AppliesTo                                                       AppliesTo,
    Func<RepoInfo, RepoOperations, CancellationToken, Task<RepoOutcome>> Run,
    Func<QuickStatus, string?>?                                     SkipReason   = null,
    bool                                                            ShowSummary  = true,
    Func<RepoInfo, string>?                                         RunningLabel = null);
