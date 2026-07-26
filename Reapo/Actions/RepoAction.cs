using System.Diagnostics.CodeAnalysis;
using Reapo.Actions.All;
using Reapo.Discovery;
using Reapo.Git;
using Reapo.Ui;
using Spectre.Console;

namespace Reapo.Actions;

/// <summary>
/// Single executor for all repo actions. Behavior is supplied by a <see cref="RepoActionSpec"/>;
/// the target type (<see cref="RepoTarget.All"/> vs <see cref="RepoTarget.Single"/>) selects the flow.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class RepoAction : IRepoAction
{
    private readonly RepoActionSpec _spec;
    private readonly GitFacade _git;
    private readonly RepoStatusCache _cache;
    private readonly RepoOperations _ops;

    public RepoAction(RepoActionSpec spec, GitFacade git, RepoStatusCache cache)
    {
        _spec = spec;
        _git = git;
        _cache = cache;
        _ops = new RepoOperations(git);
    }

    public string Name => _spec.Name;
    public string Description => _spec.Description;
    public AppliesTo AppliesTo => _spec.AppliesTo;
    public ActionSeverity Severity => _spec.Severity;

    public Task ExecuteAsync(RepoTarget target, IActionUi ui, CancellationToken ct) => target switch
    {
        RepoTarget.All all       => ExecuteAllAsync(all, ct),
        RepoTarget.Single single => ExecuteSingleAsync(single, ui, ct),
        _                        => throw new InvalidOperationException($"Unknown target: {target}"),
    };

    // ---- All-repos flow -----------------------------------------------------

    private async Task ExecuteAllAsync(RepoTarget.All all, CancellationToken ct)
    {
        var (qualifying, prefiltered) = PartitionRepos(all.Repos);

        var processed = new List<RepoOutcome>(qualifying.Count);

        if (qualifying.Count > 0)
        {
            var view = new AllReposProgressView(qualifying.Count);

            await AnsiConsole.Live(view.Render())
                .AutoClear(true)
                .StartAsync(async ctx =>
                {
                    using var spinnerCts = new CancellationTokenSource();
                    var spinnerTask = Task.Run(async () =>
                    {
                        try
                        {
                            while (!spinnerCts.IsCancellationRequested)
                            {
                                DrainInput();
                                view.TickSpinner();
                                ctx.UpdateTarget(view.Render());
                                await Task.Delay(100, spinnerCts.Token);
                            }
                        }
                        catch (OperationCanceledException) { }
                    });

                    foreach (var repo in qualifying)
                    {
                        if (ct.IsCancellationRequested)
                        {
                            processed.Add(new RepoOutcome(repo, RepoOutcomeKind.Skipped, "cancelled", []));
                            continue;
                        }

                        view.SetCurrentRepo(repo.Name);
                        ctx.UpdateTarget(view.Render());

                        // The current repo runs to completion (CancellationToken.None) so it is never
                        // left mid-operation; cancellation is honored only between repos (above).
                        var outcome = await SafeProcessAsync(repo, CancellationToken.None);
                        processed.Add(outcome);

                        view.Increment();
                        ctx.UpdateTarget(view.Render());
                    }

                    spinnerCts.Cancel();
                    try { await spinnerTask; } catch { }
                });
        }

        var allOutcomes = new List<RepoOutcome>();
        allOutcomes.AddRange(prefiltered);
        allOutcomes.AddRange(processed);

        // Refresh the status cache so the repo list shows fresh hints on return.
        _cache.Refresh(all.Repos, _git);

        if (_spec.ShowSummary)
        {
            AnsiConsole.Clear();
            RenderSummary(allOutcomes);
            AnsiConsole.MarkupLine("[grey](press any key to return to the repo list)[/]");
            Console.ReadKey(intercept: true);
        }
    }

    private (IReadOnlyList<RepoInfo> Qualifying, IReadOnlyList<RepoOutcome> PreFiltered) PartitionRepos(
        IReadOnlyList<RepoInfo> repos)
    {
        if (_spec.SkipReason is null) return (repos, []);

        var qualify = new List<RepoInfo>();
        var skipped = new List<RepoOutcome>();
        foreach (var r in repos)
        {
            QuickStatus s;
            try { s = _git.GetQuickStatus(r.FullPath); }
            catch (Exception ex)
            {
                skipped.Add(new RepoOutcome(r, RepoOutcomeKind.Failed, ex.Message, []));
                continue;
            }

            var reason = _spec.SkipReason(s);
            if (reason is null) qualify.Add(r);
            else                skipped.Add(new RepoOutcome(r, RepoOutcomeKind.Skipped, reason, []));
        }
        return (qualify, skipped);
    }

    private async Task<RepoOutcome> SafeProcessAsync(RepoInfo repo, CancellationToken ct)
    {
        try
        {
            return await _spec.Run(repo, _ops, ct);
        }
        catch (Exception ex)
        {
            return new RepoOutcome(repo, RepoOutcomeKind.Failed, ex.Message, []);
        }
    }

    private static void DrainInput()
    {
        try { while (Console.KeyAvailable) Console.ReadKey(intercept: true); }
        catch (InvalidOperationException) { /* input redirected; nothing to drain */ }
    }

    private static string SummarizeBranches(IReadOnlyList<BranchOutcome> branches)
    {
        if (branches.Count == 0) return string.Empty;
        var updated = branches.Count(b => b.Kind == BranchOutcomeKind.Updated);
        var current = branches.Count(b => b.Kind == BranchOutcomeKind.AlreadyUpToDate);
        var failed  = branches.Count(b => b.Kind == BranchOutcomeKind.Failed);
        var parts = new List<string>();
        if (updated > 0) parts.Add($"{updated} updated");
        if (current > 0) parts.Add($"{current} up-to-date");
        if (failed  > 0) parts.Add($"{failed} failed");
        return string.Join(", ", parts);
    }

    private static void RenderSummary(IReadOnlyList<RepoOutcome> outcomes)
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Repo");
        table.AddColumn("Outcome");
        table.AddColumn("Detail");

        foreach (var o in outcomes)
        {
            var (badge, detail) = RenderRow(o);
            table.AddRow(Markup.Escape(o.Repo.Name), badge, detail);
        }

        AnsiConsole.Write(table);
    }

    private static (string Badge, string Detail) RenderRow(RepoOutcome o)
    {
        switch (o.Kind)
        {
            case RepoOutcomeKind.Processed:
                var failed = o.Branches.Any(b => b.Kind == BranchOutcomeKind.Failed);
                var updated = o.Branches.Any(b => b.Kind == BranchOutcomeKind.Updated);
                if (failed && updated) return ("[yellow]▲ partial[/]", Markup.Escape(SummarizeBranches(o.Branches)));
                if (failed)            return ("[red]✗ failed[/]",   Markup.Escape(SummarizeBranches(o.Branches)));
                if (updated)           return ("[green3]✓ updated[/]", Markup.Escape(SummarizeBranches(o.Branches)));
                return ("[grey]· no changes[/]", Markup.Escape(SummarizeBranches(o.Branches)));
            case RepoOutcomeKind.Skipped:
                return ("[grey]→ skipped[/]", Markup.Escape(o.Detail ?? string.Empty));
            case RepoOutcomeKind.Failed:
                return ("[red]✗ failed[/]",   Markup.Escape(o.Detail ?? string.Empty));
            default:
                return ("?", string.Empty);
        }
    }

    // ---- Single-repo flow ---------------------------------------------------

    private async Task ExecuteSingleAsync(RepoTarget.Single single, IActionUi ui, CancellationToken ct)
    {
        var repo = single.Repo;
        var label = _spec.RunningLabel?.Invoke(repo) ?? $"{_spec.Name} {repo.Name}...";

        RepoOutcome outcome = new(repo, RepoOutcomeKind.Failed, "not run", []);
        await ui.ShowStatusAsync(label, async c => outcome = await _spec.Run(repo, _ops, c), ct);

        // On success the inner shell loop re-renders the fresh branch list as feedback,
        // so we only stop to surface failures the user needs to read before the screen clears.
        if (!HasFailure(outcome)) return;

        RenderFailures(outcome, ui);
        ui.WaitForKey("(press any key to continue)");
    }

    private static bool HasFailure(RepoOutcome outcome) =>
        outcome.Kind == RepoOutcomeKind.Failed
        || outcome.Branches.Any(b => b.Kind == BranchOutcomeKind.Failed);

    private static void RenderFailures(RepoOutcome outcome, IActionUi ui)
    {
        if (outcome.Kind == RepoOutcomeKind.Failed)
        {
            // Detail may embed raw git stderr, which can contain '[' that Spectre would parse as markup.
            ui.Error(Markup.Escape(outcome.Detail ?? "failed"));
            return;
        }

        foreach (var b in outcome.Branches.Where(b => b.Kind == BranchOutcomeKind.Failed))
        {
            ui.Error(Markup.Escape($"✗ {b.Branch} {b.Detail}"));
        }
    }
}
