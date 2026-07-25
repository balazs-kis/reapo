using Spectre.Console;
using Spectre.Console.Rendering;

namespace Reapo.Actions.All;

internal sealed class AllReposProgressView
{
    private static readonly string[] SpinnerFrames =
        ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"];

    private readonly int _total;
    private int _completed;
    private string _currentRepoName = string.Empty;
    private int _spinnerFrame;

    public AllReposProgressView(int total)
    {
        _total = total;
    }

    public void SetCurrentRepo(string name) => _currentRepoName = name;

    public void Increment() => _completed++;

    public void TickSpinner() => _spinnerFrame = (_spinnerFrame + 1) % SpinnerFrames.Length;

    public IRenderable Render()
    {
        var ratio = _total == 0 ? 1.0 : Math.Clamp((double)_completed / _total, 0.0, 1.0);
        var percent = (int)Math.Round(ratio * 100);

        var grid = new Grid().AddColumn();

        var barWidth = Math.Max(10, (AnsiConsole.Profile.Width - 8 /* panel chrome */) - 6 /* "100%  " */);
        var filled = (int)Math.Round(ratio * barWidth);
        var empty = barWidth - filled;
        var bar = $"[green3]{new string('█', filled)}[/][grey]{new string('░', empty)}[/]  [bold]{percent,3}%[/]";
        grid.AddRow(new Markup(bar));

        var spinner = SpinnerFrames[_spinnerFrame];
        var counterWidth = _total.ToString().Length * 2 + 3;
        var counter = $"{_completed} / {_total}".PadRight(counterWidth);
        var label = string.IsNullOrEmpty(_currentRepoName)
            ? $"[skyblue1]{spinner}[/]   [grey]{counter}[/]   [grey]waiting...[/]"
            : $"[skyblue1]{spinner}[/]   [grey]{counter}[/]   [bold]{Markup.Escape(_currentRepoName)}[/]";
        grid.AddRow(new Markup(label));

        return new Panel(grid)
        {
            Border = BoxBorder.Rounded,
            Expand = true,
            Padding = new Padding(1, 0, 1, 0),
        };
    }
}
