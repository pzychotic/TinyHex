using TinyHex.Services;

namespace TinyHex.Tests.TestSupport;

public sealed class FakeDialogService : IDialogService
{
    public string? OpenFileResult { get; set; }

    public List<string> Errors { get; } = [];

    public string? ShowOpenFileDialog(string title, string filter) => OpenFileResult;

    public void ShowError(string title, string message) => Errors.Add(message);
}
