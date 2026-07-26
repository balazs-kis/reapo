using System.Diagnostics.CodeAnalysis;
using Reapo.Git;

namespace Reapo.Ui;

public sealed record BranchChipSet(string Tracked, string Incoming, string Outgoing, string Tree);

[ExcludeFromCodeCoverage]
public static class BranchChips
{
    private const string Link        = "◉";
    private const string Unlink      = "○";
    private const string ArrowDown   = "↓";
    private const string ArrowUp     = "↑";
    private const string Pencil      = "~";
    private const string Ghost       = "◌";
    private const string MiddleDot   = "·";

    /// <summary>Repo-list row: shows the current branch's status; working-tree edits always shown.</summary>
    public static BranchChipSet ForState(QuickStatus status) =>
        Build(status.HasRemote, status.IsUpstreamGone, status.AheadBy, status.BehindBy, status.DirtyCount, showTree: true);

    /// <summary>Branch-summary row: edits are shown only for the current branch.</summary>
    public static BranchChipSet ForState(BranchInfo branch) =>
        Build(branch.HasRemote, branch.IsUpstreamGone, branch.AheadBy, branch.BehindBy, branch.DirtyCount, showTree: branch.IsCurrent);

    private static BranchChipSet Build(bool hasRemote, bool upstreamGone, int? aheadBy, int? behindBy, int dirtyCount, bool showTree)
    {
        var tracked = (hasRemote, upstreamGone) switch
        {
            (true, true)  => Gone(),
            (true, false) => Tracked(),
            _             => LocalOnly(),
        };
        var incoming = hasRemote && !upstreamGone ? Behind(behindBy!.Value) : Dash();
        var outgoing = hasRemote && !upstreamGone ? Ahead(aheadBy!.Value)   : Dash();
        var tree     = showTree ? Edits(dirtyCount) : Dash();
        return new BranchChipSet(tracked, incoming, outgoing, tree);
    }

    private static string Tracked()      => $"[green3][[{Link}]][/]";
    private static string LocalOnly()    => $"[grey][[{Unlink}]][/]";
    private static string Gone()         => $"[red][[{Ghost}]][/]";
    private static string Dash()         => $"[grey][[{MiddleDot}]][/]";

    private static string Behind(int n)  => $"[{(n == 0 ? "green3" : "yellow")}][[{ArrowDown} {n}]][/]";
    private static string Ahead(int n)   => $"[{(n == 0 ? "green3" : "yellow")}][[{ArrowUp} {n}]][/]";
    private static string Edits(int n)   => $"[{(n == 0 ? "green3" : "red")}][[{Pencil} {n}]][/]";
}
