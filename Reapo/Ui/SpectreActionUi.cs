using Spectre.Console;

namespace Reapo.Ui;

public sealed class SpectreActionUi : IActionUi
{
    public void Error(string markup) => AnsiConsole.MarkupLine($"[red]{markup}[/]");

    public async Task ShowStatusAsync(string description, Func<CancellationToken, Task> work, CancellationToken ct)
    {
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync(description, async _ => await work(ct));
    }

    public void WaitForKey(string markup)
    {
        AnsiConsole.MarkupLine($"[grey]{markup}[/]");
        Console.ReadKey(intercept: true);
    }
}
