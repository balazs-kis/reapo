using Reapo.Actions;
using Reapo.Git;

namespace Reapo.Tests.Actions;

public sealed class RepoActionCatalogSkipReasonTests
{
    private static RepoActionSpec SpecNamed(string name) =>
        RepoActionCatalog.Specs.First(s => s.Name == name);

    private static QuickStatus Status(string branch = "main", int dirty = 0) =>
        new(branch, dirty, HasRemote: false, AheadBy: null, BehindBy: null);

    [Fact]
    public void Update_clean_repos_skips_dirty()
    {
        var spec = SpecNamed("Update clean repos");
        Assert.NotNull(spec.SkipReason);
        Assert.Equal("dirty (action requires clean)", spec.SkipReason!(Status(dirty: 3)));
    }

    [Fact]
    public void Update_clean_repos_processes_when_clean()
    {
        var spec = SpecNamed("Update clean repos");
        Assert.Null(spec.SkipReason!(Status(dirty: 0)));
    }

    [Fact]
    public void Update_all_repos_has_no_skip_predicate()
    {
        Assert.Null(SpecNamed("Update all repos").SkipReason);
    }

    [Theory]
    [InlineData("main",   0, null)]
    [InlineData("master", 0, null)]
    [InlineData("feat",   0, "not on main/master")]
    [InlineData("main",   2, "dirty (action requires clean)")]
    [InlineData("MAIN",   0, null)]                  // case-insensitive
    public void Update_clean_repos_on_main(string branch, int dirty, string? expected)
    {
        var spec = SpecNamed("Update clean repos on main");
        Assert.Equal(expected, spec.SkipReason!(Status(branch, dirty)));
    }

    [Theory]
    [InlineData("main",   0, null)]
    [InlineData("master", 5, null)]                  // dirty is fine for "all repos on main"
    [InlineData("feat",   0, "not on main/master")]
    public void Update_all_repos_on_main(string branch, int dirty, string? expected)
    {
        var spec = SpecNamed("Update all repos on main");
        Assert.Equal(expected, spec.SkipReason!(Status(branch, dirty)));
    }

    [Fact]
    public void Catalog_contains_all_expected_action_names()
    {
        var names = RepoActionCatalog.Specs.Select(s => s.Name).ToList();
        var expected = new[]
        {
            "Fetch status",
            "Update clean repos",
            "Update all repos",
            "Update clean repos on main",
            "Update all repos on main",
            "Discard local changes",
            "Prune untracked branches",
            "Prune all but current and main",
            "Fetch",
            "Update",
            "Switch to main",
        };
        Assert.Equal(expected, names);
    }

    [Fact]
    public void Dangerous_actions_come_after_risky_which_come_after_safe_in_severity()
    {
        // Safe < Risky < Dangerous in the enum ordering the menu relies on.
        Assert.True((int)ActionSeverity.Safe < (int)ActionSeverity.Risky);
        Assert.True((int)ActionSeverity.Risky < (int)ActionSeverity.Dangerous);
    }
}
