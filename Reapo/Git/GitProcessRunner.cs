using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Reapo.Git;

public sealed class GitProcessRunner
{
    public async Task RunAsync(string repoPath, IReadOnlyList<string> args, CancellationToken ct)
    {
        await RunCaptureAsync(repoPath, args, ct);
    }

    public async Task<(string Stdout, string Stderr)> RunCaptureAsync(
        string repoPath, IReadOnlyList<string> args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName               = "git",
            WorkingDirectory       = repoPath,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true,
        };
        psi.Environment["GIT_TERMINAL_PROMPT"] = "0";
        psi.Environment["LC_ALL"] = "C"; // stable, English git output (we match on stderr substrings)
        foreach (var a in args) psi.ArgumentList.Add(a);

        Process process;
        try
        {
            process = Process.Start(psi)
                ?? throw new GitProcessException("Failed to start `git`.", string.Empty);
        }
        // Only the failure modes of Process.Start itself — a missing/unstartable `git`. Anything else
        // (e.g. a programming error) propagates instead of being mislabeled as a PATH problem.
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or PlatformNotSupportedException)
        {
            throw new GitProcessException("`git` is not on PATH or could not be started.", string.Empty, ex);
        }

        using (process)
        {
            var stdoutBuilder = new StringBuilder();
            var stderrBuilder = new StringBuilder();
            var stdoutTask = ConsumeAsync(process.StandardOutput, stdoutBuilder);
            var stderrTask = ConsumeAsync(process.StandardError, stderrBuilder);

            try
            {
                await process.WaitForExitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                try { await stderrTask; } catch { }
                try { await stdoutTask; } catch { }
                throw;
            }

            await stderrTask;
            await stdoutTask;

            if (process.ExitCode != 0)
            {
                throw new GitProcessException(
                    $"`git {string.Join(' ', args)}` exited with code {process.ExitCode}.",
                    stderrBuilder.ToString().Trim());
            }

            return (stdoutBuilder.ToString(), stderrBuilder.ToString());
        }
    }

    /// <summary>
    /// Synchronous variant used from non-async paths (status cache refresh, branch enumeration).
    /// Returns (exitCode, stdout, stderr) rather than throwing so callers can decide how to react.
    /// </summary>
    public (int ExitCode, string Stdout, string Stderr) RunCaptureSync(
        string repoPath, IReadOnlyList<string> args)
    {
        var psi = new ProcessStartInfo
        {
            FileName               = "git",
            WorkingDirectory       = repoPath,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true,
        };
        psi.Environment["GIT_TERMINAL_PROMPT"] = "0";
        psi.Environment["LC_ALL"] = "C";
        foreach (var a in args) psi.ArgumentList.Add(a);

        Process process;
        try
        {
            process = Process.Start(psi)
                ?? throw new GitProcessException("Failed to start `git`.", string.Empty);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or PlatformNotSupportedException)
        {
            throw new GitProcessException("`git` is not on PATH or could not be started.", string.Empty, ex);
        }

        using (process)
        {
            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            return (process.ExitCode, stdout, stderr);
        }
    }

    private static async Task ConsumeAsync(StreamReader reader, StringBuilder sink)
    {
        var content = await reader.ReadToEndAsync();
        sink.Append(content);
    }

    private static void TryKill(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        catch { /* process already gone */ }
    }
}
