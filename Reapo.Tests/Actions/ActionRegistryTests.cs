using Reapo.Actions;
using Reapo.Discovery;
using Reapo.Ui;

namespace Reapo.Tests.Actions;

public sealed class ActionRegistryTests
{
    private static readonly RepoInfo SampleRepo = new("alpha", "C:/x/alpha");
    private static readonly RepoTarget SingleTarget = new RepoTarget.Single(SampleRepo);
    private static readonly RepoTarget AllTarget   = new RepoTarget.All([SampleRepo]);

    private sealed class StubAction : IRepoAction
    {
        public StubAction(string name, AppliesTo appliesTo)
        {
            Name = name;
            AppliesTo = appliesTo;
        }

        public string Name { get; }
        public string Description => Name + " desc";
        public AppliesTo AppliesTo { get; }
        public Task ExecuteAsync(RepoTarget target, IActionUi ui, CancellationToken ct) => Task.CompletedTask;
    }

    [Fact]
    public void Empty_registry_returns_no_actions()
    {
        var reg = new ActionRegistry([]);
        Assert.Empty(reg.GetActionsFor(SingleTarget));
        Assert.Empty(reg.GetActionsFor(AllTarget));
    }

    [Fact]
    public void Single_only_action_appears_for_single_target_only()
    {
        var action = new StubAction("only-single", AppliesTo.Single);
        var reg = new ActionRegistry([action]);
        Assert.Single(reg.GetActionsFor(SingleTarget));
        Assert.Empty(reg.GetActionsFor(AllTarget));
    }

    [Fact]
    public void All_only_action_appears_for_all_target_only()
    {
        var action = new StubAction("only-all", AppliesTo.All);
        var reg = new ActionRegistry([action]);
        Assert.Empty(reg.GetActionsFor(SingleTarget));
        Assert.Single(reg.GetActionsFor(AllTarget));
    }

    [Fact]
    public void Both_action_appears_for_both_targets()
    {
        var action = new StubAction("both", AppliesTo.Both);
        var reg = new ActionRegistry([action]);
        Assert.Single(reg.GetActionsFor(SingleTarget));
        Assert.Single(reg.GetActionsFor(AllTarget));
    }

    [Fact]
    public void Preserves_registration_order()
    {
        var a = new StubAction("a", AppliesTo.Both);
        var b = new StubAction("b", AppliesTo.Both);
        var c = new StubAction("c", AppliesTo.Both);
        var reg = new ActionRegistry([c, a, b]);
        Assert.Equal(new[] { "c", "a", "b" }, reg.GetActionsFor(SingleTarget).Select(x => x.Name).ToArray());
    }
}
