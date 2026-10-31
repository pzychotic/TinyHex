namespace TinyHex.Services;

public interface IDialogService
{
    string? ShowOpenFileDialog(string title, string filter);
}
