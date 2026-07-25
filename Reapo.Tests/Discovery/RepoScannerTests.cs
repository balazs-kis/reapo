using Reapo.Discovery;
using Reapo.Tests.Support;

namespace Reapo.Tests.Discovery;

public sealed class RepoScannerTests : GitRepoTestBase
{
    private string MakeRepoDir(string name)
    {
        var path = Path.Combine(Root, name);
        Directory.CreateDirectory(path);
        Directory.CreateDirectory(Path.Combine(path, ".git"));
        return path;
    }

    private string MakeWorktreeDir(string name)
    {
        var path = Path.Combine(Root, name);
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, ".git"), "gitdir: ../some/where");
        return path;
    }

    private string MakePlainDir(string name)
    {
        var path = Path.Combine(Root, name);
        Directory.CreateDirectory(path);
        return path;
    }

    [Fact]
    public void Returns_empty_for_empty_root()
    {
        var scanner = new RepoScanner();
        var result = scanner.Scan(Root);
        Assert.Empty(result);
    }

    [Fact]
    public void Detects_repo_with_git_directory()
    {
        MakeRepoDir("alpha");
        var result = new RepoScanner().Scan(Root);
        Assert.Single(result);
        Assert.Equal("alpha", result[0].Name);
    }

    [Fact]
    public void Detects_repo_with_git_file_worktree()
    {
        MakeWorktreeDir("worktree-repo");
        var result = new RepoScanner().Scan(Root);
        Assert.Single(result);
        Assert.Equal("worktree-repo", result[0].Name);
    }

    [Fact]
    public void Skips_directories_without_git()
    {
        MakePlainDir("not-a-repo");
        MakeRepoDir("real-repo");
        var result = new RepoScanner().Scan(Root);
        Assert.Single(result);
        Assert.Equal("real-repo", result[0].Name);
    }

    [Fact]
    public void Skips_dot_prefixed_directories()
    {
        MakeRepoDir(".hidden");
        MakePlainDir(".idea");
        MakePlainDir(".claude");
        MakeRepoDir("visible");
        var result = new RepoScanner().Scan(Root);
        Assert.Single(result);
        Assert.Equal("visible", result[0].Name);
    }

    [Theory]
    [InlineData("node_modules")]
    [InlineData("bin")]
    [InlineData("obj")]
    [InlineData("dist")]
    [InlineData("build")]
    [InlineData("target")]
    [InlineData("NODE_MODULES")]
    public void Skips_standard_noise_folders(string name)
    {
        MakeRepoDir(name);
        MakeRepoDir("keep-me");
        var result = new RepoScanner().Scan(Root);
        Assert.Single(result);
        Assert.Equal("keep-me", result[0].Name);
    }

    [Fact]
    public void Sorts_results_by_name_case_insensitive()
    {
        MakeRepoDir("Charlie");
        MakeRepoDir("alpha");
        MakeRepoDir("bravo");
        var result = new RepoScanner().Scan(Root);
        Assert.Equal(new[] { "alpha", "bravo", "Charlie" }, result.Select(r => r.Name).ToArray());
    }

    [Fact]
    public void Does_not_recurse_into_subdirectories()
    {
        var outer = MakePlainDir("outer");
        Directory.CreateDirectory(Path.Combine(outer, "inner", ".git"));
        var result = new RepoScanner().Scan(Root);
        Assert.Empty(result);
    }
}
