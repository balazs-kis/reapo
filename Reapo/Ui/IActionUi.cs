namespace Reapo.Ui;

public interface IActionUi
{
    void Error(string markup);

    Task ShowStatusAsync(string description, Func<CancellationToken, Task> work, CancellationToken ct);

    void WaitForKey(string markup);
}
