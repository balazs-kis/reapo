using Reapo;
using Reapo.Actions;
using Reapo.Cli;
using Reapo.Discovery;
using Reapo.Git;
using Reapo.Ui;
using Spectre.Console;

try
{
    var rootCommand = RootCommandFactory.Create(RunAsync);
    return await rootCommand.Parse(args).InvokeAsync();
}
catch (Exception ex)
{
    AnsiConsole.WriteException(ex);
    return 1;
}

static async Task<int> RunAsync(string path)
{
    using var cancellation = new CancellationManager();

    var scanner            = new RepoScanner();
    var processRunner      = new GitProcessRunner();
    var git                = new GitFacade(processRunner);
    var statusCache        = new RepoStatusCache();
    var actions            = RepoActionCatalog.Create(git, statusCache);
    var registry           = new ActionRegistry(actions);
    var actionUi           = new SpectreActionUi();
    var branchSummaryView  = new BranchSummaryView(git, statusCache);
    var repoListPrompt     = new RepoListPrompt(git, statusCache);
    var actionMenuPrompt   = new ActionMenuPrompt();
    var shell              = new AppShell(repoListPrompt, actionMenuPrompt, registry, actionUi, cancellation, branchSummaryView);

    var repos = scanner.Scan(path);
    await shell.RunAsync(path, repos);
    return 0;
}
