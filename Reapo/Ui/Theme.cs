using Reapo.Actions;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Reapo.Ui;

public static class Theme
{
    public const string SelectedBackground = "grey19";

    // Border (1+1) + default Panel padding (1+1) consumed around list rows.
    public const int PanelChrome = 4;

    public static int ListInnerWidth => Math.Max(8, AnsiConsole.Profile.Width - PanelChrome);

    public static string Cursor(bool selected) => selected ? "[deepskyblue1]→[/] " : "  ";

    /// <summary>Length of the rendered text with Spectre markup stripped.</summary>
    public static int VisibleLength(string markup) => Markup.Remove(markup).Length;

    public static string HighlightRow(string content, int width)
    {
        var pad = Math.Max(0, width - VisibleLength(content));
        return $"[on {SelectedBackground}]{content}{new string(' ', pad)}[/]";
    }

    public static Panel ListPanel(IRenderable content, string headerMarkup) => new(content)
    {
        Border = BoxBorder.Rounded,
        Expand = true,
        Header = new PanelHeader($" {headerMarkup} ", Justify.Left),
        Padding = new Padding(1, 0, 1, 0),
    };

    public static string ForSeverity(ActionSeverity severity, string text) => severity switch
    {
        ActionSeverity.Dangerous => $"🚨 [red]{Markup.Escape(text)}[/]",
        ActionSeverity.Risky     => $"⚠️ [yellow]{Markup.Escape(text)}[/]",
        _                        => $"🟢 [lightgreen]{Markup.Escape(text)}[/]",
    };
}
