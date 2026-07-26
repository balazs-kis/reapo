using System.Diagnostics.CodeAnalysis;
using Reapo.Actions;
using Reapo.Discovery;
using Spectre.Console;

namespace Reapo.Ui;

[ExcludeFromCodeCoverage]
public sealed class AppShell
{
    private readonly RepoListPrompt _repoListPrompt;
    private readonly ActionMenuPrompt _actionMenuPrompt;
    private readonly ActionRegistry _registry;
    private readonly IActionUi _actionUi;
    private readonly CancellationManager _cancellation;
    private readonly BranchSummaryView _branchSummaryView;
    private readonly HashSet<string> _fetchedThisSession = new(StringComparer.Ordinal);

    public AppShell(
        RepoListPrompt repoListPrompt,
        ActionMenuPrompt actionMenuPrompt,
        ActionRegistry registry,
        IActionUi actionUi,
        CancellationManager cancellation,
        BranchSummaryView branchSummaryView)
    {
        _repoListPrompt = repoListPrompt;
        _actionMenuPrompt = actionMenuPrompt;
        _registry = registry;
        _actionUi = actionUi;
        _cancellation = cancellation;
        _branchSummaryView = branchSummaryView;
    }

    public async Task RunAsync(string scannedPath, IReadOnlyList<RepoInfo> repos)
    {
        if (repos.Count == 0)
        {
            AnsiConsole.MarkupLine($"[yellow]No git repositories found in[/] [grey]{Markup.Escape(scannedPath)}[/]");
            return;
        }

        while (true)
        {
            AnsiConsole.Clear();

            var selection = _repoListPrompt.Show(scannedPath, repos);
            AnsiConsole.Clear();
            if (selection is RepoListSelection.QuitChoice) return;

            RepoTarget target;
            switch (selection)
            {
                case RepoListSelection.AllChoice:
                    target = new RepoTarget.All(repos);
                    break;
                case RepoListSelection.SingleChoice s:
                    var repo = repos.FirstOrDefault(r => r.FullPath == s.FullPath);
                    if (repo is null) continue; // selection no longer present; re-show the list
                    target = new RepoTarget.Single(repo);
                    break;
                default:
                    throw new InvalidOperationException();
            }

            if (target is RepoTarget.Single single)
            {
                await RunSingleRepoLoopAsync(single, target);
                continue;
            }

            var actions = _registry.GetActionsFor(target);
            var picked = _actionMenuPrompt.Show(actions);
            if (picked is null) continue;

            await RunActionAsync(picked, target);
        }
    }

    private async Task RunSingleRepoLoopAsync(RepoTarget.Single single, RepoTarget target)
    {
        var fetch = _fetchedThisSession.Add(single.Repo.FullPath);
        while (true)
        {
            AnsiConsole.Clear();
            _cancellation.ResetForNextAction();
            var summary = await _branchSummaryView.BuildAsync(single.Repo, fetch, _cancellation.Token);
            if (summary.Outcome == BranchSummaryOutcome.Back) return;

            var actions = _registry.GetActionsFor(target);
            var picked = _actionMenuPrompt.Show(actions, summary.Panel);
            if (picked is null) return;

            await RunActionAsync(picked, target);
            fetch = false;
        }
    }

    private async Task RunActionAsync(IRepoAction action, RepoTarget target)
    {
        _cancellation.ResetForNextAction();
        try
        {
            await action.ExecuteAsync(target, _actionUi, _cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.MarkupLine("[yellow]Action cancelled.[/]");
            AnsiConsole.MarkupLine("[grey](press any key to return to the repo list)[/]");
            Console.ReadKey(intercept: true);
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            AnsiConsole.MarkupLine("[grey](press any key to return to the repo list)[/]");
            Console.ReadKey(intercept: true);
        }
    }
}
