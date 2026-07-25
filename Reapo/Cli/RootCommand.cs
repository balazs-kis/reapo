using System.CommandLine;

namespace Reapo.Cli;

public static class RootCommandFactory
{
    public static RootCommand Create(Func<string, Task<int>> handler)
    {
        var pathArg = new Argument<string>("path")
        {
            Description = "Directory containing your git repositories.",
        };

        pathArg.Validators.Add(result =>
        {
            var value = result.GetValueOrDefault<string>();
            if (string.IsNullOrWhiteSpace(value))
            {
                result.AddError("A path is required.");
                return;
            }
            if (!Directory.Exists(value))
            {
                result.AddError($"Directory not found: {value}");
            }
        });

        var root = new RootCommand("Reapo — manage a folder full of git repositories.")
        {
            pathArg,
        };

        root.SetAction(async parseResult =>
        {
            var path = parseResult.GetValue(pathArg)!;
            return await handler(path);
        });

        return root;
    }
}
