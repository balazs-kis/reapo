using System.Diagnostics.CodeAnalysis;

namespace Reapo;

[ExcludeFromCodeCoverage]
public sealed class CancellationManager : IDisposable
{
    private static readonly TimeSpan DoublePressWindow = TimeSpan.FromSeconds(2);

    private CancellationTokenSource _cts = new();
    private bool _cancelledThisCycle;
    private DateTime _lastCancelAt = DateTime.MinValue;
    private readonly object _lock = new();

    public CancellationManager()
    {
        Console.CancelKeyPress += OnCancelKeyPress;
    }

    public CancellationToken Token
    {
        get { lock (_lock) return _cts.Token; }
    }

    public void ResetForNextAction()
    {
        lock (_lock)
        {
            _cts.Dispose();
            _cts = new CancellationTokenSource();
            _cancelledThisCycle = false;
        }
    }

    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        e.Cancel = true;

        bool exitNow;
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            var sinceLast = now - _lastCancelAt;
            _lastCancelAt = now;

            // Exit only on a genuine second press within the window during the same action cycle —
            // not on a lone press that happens to land while a prior cancellation is still in effect.
            exitNow = _cancelledThisCycle && sinceLast < DoublePressWindow;

            if (!exitNow)
            {
                _cancelledThisCycle = true;
                _cts.Cancel();
            }
        }

        if (exitNow) Environment.Exit(130);
    }

    public void Dispose()
    {
        Console.CancelKeyPress -= OnCancelKeyPress;
        _cts.Dispose();
    }
}
