using Reapo.Actions;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Reapo.Ui;

public sealed class ActionMenuPrompt
{
    public IRepoAction? Show(IReadOnlyList<IRepoAction> actions, IRenderable? header = null)
    {
        if (actions.Count == 0)
        {
            if (header != null) AnsiConsole.Write(header);
            AnsiConsole.MarkupLine("[grey]No actions available yet.[/]");
            AnsiConsole.MarkupLine("[grey](press any key to go back)[/]");
            Console.ReadKey(intercept: true);
            return null;
        }

        var ordered = actions
            .OrderBy(a => (int)a.Severity)
            .ToList();

        var backIndex = ordered.Count;       // trailing "Back" row
        var count     = ordered.Count + 1;
        var index     = 0;
        var armed     = false;

        IRepoAction? result = null;
        var done = false;

        AnsiConsole.Live(Compose(header, Render(ordered, index, armed)))
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
                            armed = false;
                            break;
                        case ConsoleKey.DownArrow:
                            index = (index + 1) % count;
                            armed = false;
                            break;
                        case ConsoleKey.Escape:
                            if (armed) armed = false;
                            else { result = null; done = true; }
                            break;
                        case ConsoleKey.Enter:
                            if (index == backIndex)
                            {
                                result = null;
                                done = true;
                            }
                            else
                            {
                                var action = ordered[index];
                                if (action.Severity == ActionSeverity.Dangerous && !armed)
                                {
                                    armed = true;
                                }
                                else
                                {
                                    result = action;
                                    done = true;
                                }
                            }
                            break;
                    }

                    ctx.UpdateTarget(Compose(header, Render(ordered, index, armed)));
                    ctx.Refresh();
                }
            });

        return result;
    }

    private static IRenderable Compose(IRenderable? header, IRenderable menu)
    {
        if (header is null) return menu;
        var rows = new Rows(header, menu);
        return rows;
    }

    private static IRenderable Render(IReadOnlyList<IRepoAction> ordered, int index, bool armed)
    {
        var width = Theme.ListInnerWidth;
        var lines = new List<string>(ordered.Count + 1);

        for (var i = 0; i < ordered.Count; i++)
        {
            var cursor = Theme.Cursor(i == index);
            string content;
            if (i == index && armed)
            {
                content = $"{cursor}[bold red]Are you sure? (↳/esc)[/]";
            }
            else
            {
                var a = ordered[i];
                content = $"{cursor}{Theme.ForSeverity(a.Severity, a.Name)} [grey]•[/] [grey]{Markup.Escape(a.Description)}[/]";
            }
            lines.Add(i == index ? Theme.HighlightRow(content, width) : content);
        }

        var isBackSelected = index == ordered.Count;
        var back = $"{Theme.Cursor(isBackSelected)}[grey]Back[/]";
        lines.Add(isBackSelected ? Theme.HighlightRow(back, width) : back);

        var list = new Markup(string.Join("\n", lines));
        return Theme.ListPanel(list, "[bold deepskyblue1]Actions[/]");
    }
}
