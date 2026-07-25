using Spectre.Console;

namespace Reapo.Tests.Ui;

/// <summary>
/// Guards the C1 fix: git stderr can contain '[' (e.g. "[new branch]"), which Spectre parses as a
/// markup tag and throws on. Failure detail must be escaped before being handed to markup rendering.
/// </summary>
public sealed class MarkupEscapingTests
{
    [Theory]
    [InlineData("fatal: couldn't find remote ref [new branch]")]
    [InlineData("error: cannot lock ref 'refs/heads/x' [rejected]")]
    [InlineData("hint: [detached HEAD] state")]
    public void Raw_git_stderr_with_brackets_throws_when_unescaped_but_survives_escaping(string stderr)
    {
        // The Markup constructor parses eagerly. Reproduce the bug: interpolating raw stderr into
        // markup (as ui.Error did before the fix) throws on '['.
        Assert.ThrowsAny<Exception>(() => new Markup($"[red]{stderr}[/]"));

        // The fix: escaping first constructs cleanly, and the bracket is preserved as literal text.
        var markup = Markup.Escape(stderr);
        _ = new Markup($"[red]{markup}[/]"); // must not throw
        Assert.Contains("[", markup);
    }
}
