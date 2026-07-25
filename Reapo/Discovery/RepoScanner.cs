namespace Reapo.Discovery;

public sealed class RepoScanner
{
    private static readonly HashSet<string> NoiseFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules", "bin", "obj", "dist", "build", "target",
    };

    public IReadOnlyList<RepoInfo> Scan(string rootPath)
    {
        if (!Directory.Exists(rootPath)) return [];

        var results = new List<RepoInfo>();

        foreach (var dir in EnumerateChildrenSafely(rootPath))
        {
            var name = Path.GetFileName(dir);
            if (string.IsNullOrEmpty(name)) continue;
            if (name.StartsWith('.')) continue;
            if (NoiseFolders.Contains(name)) continue;
            if (!HasGitMarker(dir)) continue;

            results.Add(new RepoInfo(name, dir));
        }

        results.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return results;
    }

    private static IEnumerable<string> EnumerateChildrenSafely(string root)
    {
        try
        {
            return Directory.EnumerateDirectories(root);
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
        catch (IOException)
        {
            return [];
        }
    }

    private static bool HasGitMarker(string dir)
    {
        var marker = Path.Combine(dir, ".git");
        return Directory.Exists(marker) || File.Exists(marker);
    }
}
