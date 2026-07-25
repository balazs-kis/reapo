namespace Reapo.Git;

public sealed class GitProcessException : Exception
{
    public string Stderr { get; }

    public GitProcessException(string message, string stderr, Exception? innerException = null)
        : base(message, innerException)
    {
        Stderr = stderr;
    }
}
