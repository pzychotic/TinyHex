using System.Text;
using TinyHex.Models;
using TinyHex.Tests.TestSupport;
using TinyHex.ViewModels;

namespace TinyHex.Tests.ViewModels;

[TestFixture]
public class MainViewModelSearchTests
{
    // "AB" at 10, 50, 100 and 200, "ab" at 150; 32 bytes per line
    private static readonly long[] UpperHits = [10, 50, 100, 200];

    private TempFile _file = null!;
    private FakeDialogService _dialogService = null!;
    private MainViewModel _vm = null!;

    [SetUp]
    public void SetUp()
    {
        var data = Enumerable.Repeat((byte)'.', 256).ToArray();
        foreach (var offset in UpperHits)
            "AB"u8.CopyTo(data.AsSpan((int)offset));
        "ab"u8.CopyTo(data.AsSpan(150));
        _file = new TempFile(data);
        _dialogService = new FakeDialogService();
        _vm = new MainViewModel(_dialogService) { SearchDelay = TimeSpan.Zero };
        Open(_file);
        _vm.IsSearchOpen = true;
    }

    [TearDown]
    public async Task TearDown()
    {
        _vm.Dispose();
        await _vm.SearchTask;   // the search has the file open until it has stopped
        _file.Dispose();
        foreach (var file in _otherFiles)
            file.Dispose();
        _otherFiles.Clear();
    }

    private readonly List<TempFile> _otherFiles = [];

    /// <summary>A further test file, deleted after the view model has let go of it.</summary>
    private TempFile OtherFile(byte[] data)
    {
        var file = new TempFile(data);
        _otherFiles.Add(file);
        return file;
    }

    private void Open(TempFile file)
    {
        _dialogService.OpenFileResult = file.Path;
        _vm.OpenFileCommand.Execute(null);
    }

    private async Task Search(string text)
    {
        _vm.SearchText = text;
        await _vm.SearchTask;
    }

    private long CurrentHitOffset() =>
        _vm.SearchHighlights is { Current: int current } h
            ? Enumerable.Range(0, 256).First(o => h.For(o, 1).Any(r => r.IsCurrent))
            : -1;

    [Test]
    public async Task SearchText_HighlightsTheHitsAndGoesToTheFirst()
    {
        int? scrolledTo = null;
        _vm.ScrollRequest.ScrollRequested += index => scrolledTo = index;

        await Search("AB");

        Assert.That(_vm.SearchStatus, Is.EqualTo("1 of 5"));
        Assert.That(_vm.HasNoMatches, Is.False);
        Assert.That(_vm.SearchHighlights!.Count, Is.EqualTo(5));
        Assert.That(_vm.SearchHighlights.Current, Is.EqualTo(0));
        Assert.That(_vm.SelectedIndex, Is.EqualTo(0));
        Assert.That(scrolledTo, Is.EqualTo(0));
    }

    [Test]
    public async Task SearchText_StartsAtTheSelectedRow()
    {
        _vm.SelectedIndex = 3; // offset 96

        await Search("AB");

        Assert.That(_vm.SearchStatus, Is.EqualTo("3 of 5"));
        Assert.That(CurrentHitOffset(), Is.EqualTo(100));
        Assert.That(_vm.SelectedIndex, Is.EqualTo(3));
    }

    [Test]
    public async Task SearchText_AfterTheLastHit_WrapsToTheFirst()
    {
        _vm.SelectedIndex = 7; // offset 224

        await Search("AB");

        Assert.That(_vm.SearchStatus, Is.EqualTo("1 of 5"));
        Assert.That(_vm.SelectedIndex, Is.EqualTo(0));
    }

    [Test]
    public async Task SearchText_NoHit_SaysSo()
    {
        await Search("zz");

        Assert.That(_vm.SearchStatus, Is.EqualTo("No matches"));
        Assert.That(_vm.HasNoMatches, Is.True);
        Assert.That(_vm.SearchHighlights, Is.Null);
    }

    [Test]
    public async Task SearchMatchCase_NarrowsTheHits()
    {
        _vm.SearchMatchCase = true;

        await Search("AB");

        Assert.That(_vm.SearchStatus, Is.EqualTo("1 of 4"));
    }

    [Test]
    public async Task HexSearch_FindsExactBytes()
    {
        _vm.SearchMode = SearchMode.Hex;

        await Search("61 62");

        Assert.That(_vm.SearchStatus, Is.EqualTo("1 of 1"));
        Assert.That(CurrentHitOffset(), Is.EqualTo(150));
        Assert.That(_vm.IsTextSearch, Is.False);
    }

    [Test]
    public async Task HexSearch_InvalidHex_SaysSo()
    {
        _vm.SearchMode = SearchMode.Hex;

        await Search("ABC");

        Assert.That(_vm.SearchStatus, Is.EqualTo("Invalid hex"));
        Assert.That(_vm.HasNoMatches, Is.True);
    }

    [Test]
    public async Task Utf16Search_FindsWideText()
    {
        var data = new byte[64];
        Encoding.Unicode.GetBytes("Hi").CopyTo(data, 40);
        Open(OtherFile(data));
        _vm.SearchUtf16 = true;

        await Search("hi");

        Assert.That(_vm.SearchStatus, Is.EqualTo("1 of 1"));
        Assert.That(CurrentHitOffset(), Is.EqualTo(40));
    }

    [Test]
    public async Task FindNextAndPrevious_WrapAround()
    {
        await Search("AB");

        _vm.FindPreviousCommand.Execute(null);
        Assert.That(_vm.SearchStatus, Is.EqualTo("5 of 5"));
        Assert.That(_vm.SelectedIndex, Is.EqualTo(200 / 32));

        _vm.FindNextCommand.Execute(null);
        Assert.That(_vm.SearchStatus, Is.EqualTo("1 of 5"));

        _vm.FindNextCommand.Execute(null);
        Assert.That(_vm.SearchStatus, Is.EqualTo("2 of 5"));
        Assert.That(CurrentHitOffset(), Is.EqualTo(50));
    }

    [Test]
    public async Task CloseSearch_ClearsTheHitsButKeepsTheText()
    {
        await Search("AB");

        _vm.CloseSearchCommand.Execute(null);

        Assert.That(_vm.IsSearchOpen, Is.False);
        Assert.That(_vm.SearchText, Is.EqualTo("AB"));
        Assert.That(_vm.SearchHighlights, Is.Null);
        Assert.That(_vm.SearchStatus, Is.Empty);
        Assert.That(_vm.CloseSearchCommand.CanExecute(null), Is.False);
    }

    [Test]
    public async Task FindNext_WhileClosed_ReopensTheSearch()
    {
        _vm.IsSearchOpen = false;
        _vm.SearchText = "AB";

        _vm.FindNextCommand.Execute(null);
        await _vm.SearchTask;

        Assert.That(_vm.IsSearchOpen, Is.True);
        Assert.That(_vm.SearchStatus, Is.EqualTo("1 of 5"));
    }

    [Test]
    public void FindNext_WithoutText_IsDisabled()
    {
        Assert.That(_vm.FindNextCommand.CanExecute(null), Is.False);
        Assert.That(_vm.FindPreviousCommand.CanExecute(null), Is.False);

        _vm.SearchText = "x";

        Assert.That(_vm.FindNextCommand.CanExecute(null), Is.True);
    }

    [Test]
    public async Task OpenFile_SearchesTheNewFile()
    {
        await Search("AB");
        var data = new byte[64];
        "AB"u8.CopyTo(data.AsSpan(33));

        Open(OtherFile(data));
        await _vm.SearchTask;

        Assert.That(_vm.SearchStatus, Is.EqualTo("1 of 1"));
        Assert.That(CurrentHitOffset(), Is.EqualTo(33));
    }

    [Test]
    public async Task ChangingBytesPerLine_KeepsTheHits()
    {
        await Search("AB");
        _vm.FindNextCommand.Execute(null); // offset 50
        var highlights = _vm.SearchHighlights;

        _vm.BytesPerLine = 8;

        Assert.That(_vm.SearchHighlights, Is.SameAs(highlights));
        Assert.That(_vm.SelectedIndex, Is.EqualTo(50 / 8));
    }
}
