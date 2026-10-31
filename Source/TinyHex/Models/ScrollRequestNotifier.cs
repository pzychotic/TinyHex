namespace TinyHex.Models;

/// <summary>
/// Notifier that view-layer behaviors subscribe to in order to scroll a
/// list to a given item index, even if that index is already selected.
/// </summary>
public sealed class ScrollRequestNotifier
{
    public event Action<int>? ScrollRequested;

    public void Request(int index) => ScrollRequested?.Invoke(index);
}
