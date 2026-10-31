using TinyHex.Services;

namespace TinyHex.Tests.TestSupport;

public sealed class FakeDialogService : IDialogService
{
    public string? OpenFileResult { get; set; }

    public string? ShowOpenFileDialog(string title, string filter) => OpenFileResult;
}
