using System.Windows;

namespace TinyHex.Services;

public sealed class WpfDialogService(Window owner) : IDialogService
{
    public string? ShowOpenFileDialog(string title, string filter)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = title, Filter = filter };
        return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
    }
}
