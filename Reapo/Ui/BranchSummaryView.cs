using Reapo.Discovery;
using Reapo.Git;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Reapo.Ui;

public enum BranchSummaryOutcome
{
    Continue,
    Back,
}

public sealed record BranchSummaryResult(BranchSummaryOutcome Outcome, IRenderable? Panel);

public sealed class BranchSummaryView
{
    private readonly GitFacade _git;
    private readonly RepoStatusCache _cache;

    public BranchSummaryView(GitFacade git, RepoStatusCache cache)
    {
        _git = git;
        _cache = cache;
    }

    public async Task<BranchSummaryResult> BuildAsync(RepoInfo repo, bool fetch, CancellationToken ct)
    {
        if (fetch)
        {
            var fetchOutcome = await TryFetchAsync(repo, ct);
            if (fetchOutcome == BranchSummaryOutcome.Back) return new BranchSummaryResult(BranchSummaryOutcome.Back, null);
        }

        IReadOnlyList<BranchInfo> branches;
        try
        {
            branches = _git.GetBranches(repo.FullPath);
        }
        catch (Exception ex)
        {
            return new BranchSummaryResult(RenderFatalReadFailure(ex), null);
        }

        _cache.RefreshOne(repo, _git);
        return new BranchSummaryResult(BranchSummaryOutcome.Continue, BuildHeaderAndGrid(repo, branches));
    }

    private async Task<BranchSummaryOutcome> TryFetchAsync(RepoInfo repo, CancellationToken ct)
    {
        while (true)
        {
            try
            {
                await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .StartAsync(
                        $"Fetching from origin for [bold deepskyblue1]{Markup.Escape(repo.Name)}[/]...",
                        async _ => await _git.FetchAsync(repo.FullPath, ct));
                return BranchSummaryOutcome.Continue;
            }
            catch (OperationCanceledException)
            {
                AnsiConsole.MarkupLine("[yellow]Fetch cancelled.[/]");
            }
            catch (GitProcessException ex)
            {
                RenderFetchError(ex);
            }

            switch (PromptOnFetchFailure())
            {
                case FetchFailureChoice.Retry:          continue;
                case FetchFailureChoice.ContinueCached: return BranchSummaryOutcome.Continue;
                default:                                return BranchSummaryOutcome.Back;
            }
        }
    }

    private enum FetchFailureChoice { Retry, ContinueCached, Back }

    private static FetchFailureChoice PromptOnFetchFailure()
    {
        const string retry = "Retry fetch";
        const string cont  = "Continue with cached info";
        const string back  = "Back to repo list";

        var picked = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .AddChoices(retry, cont, back));

        return picked switch
        {
            retry => FetchFailureChoice.Retry,
            cont  => FetchFailureChoice.ContinueCached,
            _     => FetchFailureChoice.Back,
        };
    }

    private static void RenderFetchError(GitProcessException ex)
    {
        AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
        if (!string.IsNullOrEmpty(ex.Stderr))
        {
            var snippet = ex.Stderr.Length > 200 ? ex.Stderr[..200] + "..." : ex.Stderr;
            AnsiConsole.MarkupLine($"[grey]{Markup.Escape(snippet)}[/]");
        }
    }

    private static BranchSummaryOutcome RenderFatalReadFailure(Exception ex)
    {
        AnsiConsole.MarkupLine($"[red]Failed to read branches: {Markup.Escape(ex.Message)}[/]");
        AnsiConsole.MarkupLine("[grey](press any key to return to the repo list)[/]");
        Console.ReadKey(intercept: true);
        return BranchSummaryOutcome.Back;
    }

    private static IRenderable BuildHeaderAndGrid(
        RepoInfo repo,
        IReadOnlyList<BranchInfo> branches)
    {
        var untracked = branches.Count(b => !b.HasRemote);
        var trackingSummary = untracked == 0 ? "all tracked" : $"{untracked} untracked";
        var rightSide = $"({branches.Count} {(branches.Count == 1 ? "branch" : "branches")}, {trackingSummary})";

        var rows = branches.Select(BuildRow).ToList();
        var nameBudget = ComputeNameBudget(rows);

        var grid = new Grid()
            .AddColumn().AddColumn().AddColumn().AddColumn().AddColumn();

        foreach (var row in rows)
        {
            var displayName = TruncateName(row.Branch.Name, nameBudget);
            var glyph = row.Branch.IsCurrent ? "[bold deepskyblue1]*[/]" : " ";
            var nameMarkup = $"{glyph} [skyblue1]{Markup.Escape(displayName)}[/]";
            grid.AddRow(nameMarkup, row.Tracked, row.Incoming, row.Outgoing, row.Tree);
        }

        var panel = new Panel(grid)
        {
            Border = BoxBorder.Rounded,
            Expand = true,
            Header = new PanelHeader(
                $" [bold deepskyblue1]{Markup.Escape(repo.Name)}[/] [grey]{rightSide}[/] ",
                Justify.Left),
            Padding = new Padding(2, 0, 2, 0),
        };

        return panel;
    }

    private sealed record RowData(BranchInfo Branch, string Tracked, string Incoming, string Outgoing, string Tree);

    private static RowData BuildRow(BranchInfo b)
    {
        var chips = BranchChips.ForState(b);
        return new RowData(b, chips.Tracked, chips.Incoming, chips.Outgoing, chips.Tree);
    }

    private static int ComputeNameBudget(IReadOnlyList<RowData> rows)
    {
        // Chip column widths: max visible cells per column across all rows.
        var trackedW  = rows.Max(r => Theme.VisibleLength(r.Tracked));
        var incomingW = rows.Max(r => Theme.VisibleLength(r.Incoming));
        var outgoingW = rows.Max(r => Theme.VisibleLength(r.Outgoing));
        var treeW     = rows.Max(r => Theme.VisibleLength(r.Tree));

        const int glyphAndSpace = 2;       // "* " or "  "
        const int columnGaps    = 8;       // 4 inter-column gaps × 2 cells (Grid pads 1 right + 1 left)
        const int panelChrome   = 2 + 4;   // 1+1 border + 2+2 horizontal padding
        const int safety        = 2;       // small margin for markup-length quirks

        var consumed = panelChrome + glyphAndSpace + columnGaps + safety + trackedW + incomingW + outgoingW + treeW;
        var available = AnsiConsole.Profile.Width - consumed;
        return Math.Max(8, available);
    }

    private static string TruncateName(string name, int budget) =>
        name.Length > budget ? name[..(budget - 3)] + "..." : name;
}
