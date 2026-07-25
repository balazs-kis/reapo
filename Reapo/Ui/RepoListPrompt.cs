using Reapo.Discovery;
using Reapo.Git;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Reapo.Ui;

public sealed class RepoListPrompt
{
    private readonly GitFacade _git;
    private readonly RepoStatusCache _cache;

    public RepoListPrompt(GitFacade git, RepoStatusCache cache)
    {
        _git = git;
        _cache = cache;
    }

    public RepoListSelection Show(string scannedPath, IReadOnlyList<RepoInfo> repos)
    {
        EnsureCacheWarm(repos);
        var statuses = _cache.Snapshot;

        // Row 0 = <ALL REPOS>, rows 1..N = repos, last row = <QUIT>.
        var count = repos.Count + 2;
        var quitIndex = repos.Count + 1;
        var index = 0;
        var done = false;
        RepoListSelection result = RepoListSelection.Quit;

        AnsiConsole.Live(Render(repos, statuses, index, scannedPath))
            .Start(ctx =>
            {
                ctx.Refresh();
                while (!done)
                {
                    var key = Console.ReadKey(intercept: true);
                    switch (key.Key)
                    {
                        case ConsoleKey.UpArrow:
                            index = (index - 1 + count) % count;
                            break;
                        case ConsoleKey.DownArrow:
                            index = (index + 1) % count;
                            break;
                        case ConsoleKey.Escape:
                            result = RepoListSelection.Quit;
                            done = true;
                            break;
                        case ConsoleKey.Enter:
                            result = index switch
                            {
                                0          => RepoListSelection.All,
                                _ when index == quitIndex => RepoListSelection.Quit,
                                _          => RepoListSelection.SingleByPath(repos[index - 1].FullPath),
                            };
                            done = true;
                            break;
                    }

                    ctx.UpdateTarget(Render(repos, statuses, index, scannedPath));
                    ctx.Refresh();
                }
            });

        return result;
    }

    private void EnsureCacheWarm(IReadOnlyList<RepoInfo> repos)
    {
        if (!_cache.IsEmpty) return;

        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .Start("Reading repos...", _ => _cache.Refresh(repos, _git));
    }

    private static IRenderable Render(
        IReadOnlyList<RepoInfo> repos,
        IReadOnlyDictionary<string, QuickStatus?> statuses,
        int index,
        string scannedPath)
    {
        var width = Theme.ListInnerWidth;
        var lines = new List<string>(repos.Count + 2);

        lines.Add(RowOrHighlight($"{Theme.Cursor(index == 0)}[bold deepskyblue1]<ALL REPOS>[/]", index == 0, width));

        for (var i = 0; i < repos.Count; i++)
        {
            var selected = index == i + 1;
            lines.Add(RowOrHighlight(RenderRepo(repos[i], statuses, Theme.Cursor(selected), width), selected, width));
        }

        var quitSelected = index == repos.Count + 1;
        lines.Add(RowOrHighlight($"{Theme.Cursor(quitSelected)}[bold orangered1]<QUIT>[/]", quitSelected, width));

        var list = new Markup(string.Join("\n", lines));
        return Theme.ListPanel(list, $"[bold deepskyblue1]REAP-O[/] [grey]{Markup.Escape(scannedPath)}[/]");
    }

    private static string RowOrHighlight(string content, bool selected, int width) =>
        selected ? Theme.HighlightRow(content, width) : content;

    private const string Separator = " [grey]•[/] ";

    private static string RenderRepo(RepoInfo repo, IReadOnlyDictionary<string, QuickStatus?> statuses, string cursor, int width)
    {
        if (!statuses.TryGetValue(repo.FullPath, out var status) || status is null)
        {
            return $"{cursor}{Markup.Escape(repo.Name)} [grey][[?]][/]";
        }

        var chipSet = BranchChips.ForState(status);
        var chips = $"{chipSet.Tracked} {chipSet.Incoming} {chipSet.Outgoing} {chipSet.Tree}";

        const int separatorVisible = 3; // " • " per separator
        const int cursorVisible = 2;    // "→ " or "  "
        var nameVisible = repo.Name.Length;
        var chipsVisible = Theme.VisibleLength(chips);
        var consumed = cursorVisible + nameVisible + separatorVisible + separatorVisible + chipsVisible;

        var available = width - consumed;
        var branchBudget = Math.Min(50, Math.Max(8, available));
        var branchName = status.Branch.Length > branchBudget
            ? status.Branch[..(branchBudget - 3)] + "..."
            : status.Branch;
        var branch = $"[skyblue1]{Markup.Escape(branchName)}[/]";

        return $"{cursor}{Markup.Escape(repo.Name)}{Separator}{branch}{Separator}{chips}";
    }
}

public abstract record RepoListSelection
{
    public static readonly RepoListSelection All = new AllChoice();
    public static readonly RepoListSelection Quit = new QuitChoice();
    public static RepoListSelection SingleByPath(string fullPath) => new SingleChoice(fullPath);

    public sealed record AllChoice    : RepoListSelection;
    public sealed record QuitChoice   : RepoListSelection;
    public sealed record SingleChoice(string FullPath) : RepoListSelection;
}
