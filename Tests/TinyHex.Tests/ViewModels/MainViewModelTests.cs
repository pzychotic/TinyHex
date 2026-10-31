using TinyHex.Tests.TestSupport;
using TinyHex.ViewModels;

namespace TinyHex.Tests.ViewModels;

[TestFixture]
public class MainViewModelTests
{
    private TempFile _file = null!;
    private FakeDialogService _dialogService = null!;
    private MainViewModel _vm = null!;

    [SetUp]
    public void SetUp()
    {
        _file = TempFile.WithSequentialBytes(256);
        _dialogService = new FakeDialogService();
        _vm = new MainViewModel(_dialogService);
    }

    [TearDown]
    public void TearDown()
    {
        _vm.Dispose();
        _file.Dispose();
    }

    private void OpenTestFile()
    {
        _dialogService.OpenFileResult = _file.Path;
        _vm.OpenFileCommand.Execute(null);
    }

    [Test]
    public void Defaults()
    {
        Assert.That(_vm.BytesPerLineOptions, Is.EqualTo(new[] { 8, 16, 32, 64 }));
        Assert.That(_vm.BytesPerLine, Is.EqualTo(32));
        Assert.That(_vm.Records, Is.Null);
        Assert.That(_vm.SelectedIndex, Is.EqualTo(-1));
        Assert.That(_vm.Filename, Is.Empty);
        Assert.That(_vm.WindowTitle, Is.EqualTo("TinyHex"));
    }

    [Test]
    public void OpenFile_Cancelled_LeavesRecordsEmpty()
    {
        _dialogService.OpenFileResult = null;
        _vm.OpenFileCommand.Execute(null);

        Assert.That(_vm.Records, Is.Null);
        Assert.That(_vm.WindowTitle, Is.EqualTo("TinyHex"));
    }

    [Test]
    public void OpenFile_ShowsFilenameInWindowTitle()
    {
        var changed = new List<string?>();
        _vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        OpenTestFile();

        Assert.That(_vm.Filename, Is.EqualTo(Path.GetFileName(_file.Path)));
        Assert.That(_vm.WindowTitle, Is.EqualTo($"TinyHex - {Path.GetFileName(_file.Path)}"));
        Assert.That(changed, Does.Contain(nameof(MainViewModel.WindowTitle)));
    }

    [Test]
    public void OpenFile_LoadsRecords()
    {
        OpenTestFile();

        Assert.That(_vm.Records, Is.Not.Null);
        Assert.That(_vm.Records!.Count, Is.EqualTo(256 / 32));
        Assert.That(_vm.Records[1], Does.StartWith("00000020: 20 21"));
    }

    [Test]
    public void Drop_LoadsFirstFile()
    {
        _vm.DropCommand.Execute(new[] { _file.Path, "ignored.bin" });

        Assert.That(_vm.Records, Is.Not.Null);
        Assert.That(_vm.Records!.Count, Is.EqualTo(256 / 32));
    }

    [Test]
    public void Drop_NoFiles_LeavesRecordsEmpty()
    {
        _vm.DropCommand.Execute(Array.Empty<string>());
        _vm.DropCommand.Execute(null);

        Assert.That(_vm.Records, Is.Null);
    }

    [Test]
    public void ChangingBytesPerLine_ReloadsRecords()
    {
        OpenTestFile();
        var before = _vm.Records;

        _vm.BytesPerLine = 8;

        Assert.That(_vm.Records, Is.Not.SameAs(before));
        Assert.That(_vm.Records!.Count, Is.EqualTo(256 / 8));
    }

    [Test]
    public void ChangingBytesPerLine_BeforeOpen_IsUsedWhenOpening()
    {
        _vm.BytesPerLine = 16;
        OpenTestFile();

        Assert.That(_vm.Records!.Count, Is.EqualTo(256 / 16));
    }

    [TestCase("64", 2)]
    [TestCase("0x40", 2)]
    [TestCase("255", 7)]
    [TestCase("0", 0)]
    public void GoTo_ValidOffset_SelectsAndScrollsToRecord(string input, int expectedIndex)
    {
        OpenTestFile();
        int? scrolledTo = null;
        _vm.ScrollRequest.ScrollRequested += index => scrolledTo = index;

        _vm.GoToText = input;
        _vm.GoToCommand.Execute(null);

        Assert.That(_vm.SelectedIndex, Is.EqualTo(expectedIndex));
        Assert.That(scrolledTo, Is.EqualTo(expectedIndex));
    }

    [Test]
    public void GoTo_SameOffsetTwice_RequestsScrollBothTimes()
    {
        OpenTestFile();
        int requests = 0;
        _vm.ScrollRequest.ScrollRequested += _ => requests++;

        _vm.GoToText = "100";
        _vm.GoToCommand.Execute(null);
        _vm.GoToCommand.Execute(null);

        Assert.That(requests, Is.EqualTo(2));
    }

    [TestCase("256")]
    [TestCase("-1")]
    [TestCase("abc")]
    [TestCase("")]
    public void GoTo_InvalidOrOutOfRange_DoesNothing(string input)
    {
        OpenTestFile();
        bool scrolled = false;
        _vm.ScrollRequest.ScrollRequested += _ => scrolled = true;

        _vm.GoToText = input;
        _vm.GoToCommand.Execute(null);

        Assert.That(_vm.SelectedIndex, Is.EqualTo(-1));
        Assert.That(scrolled, Is.False);
    }

    [Test]
    public void GoTo_WithoutFile_DoesNothing()
    {
        bool scrolled = false;
        _vm.ScrollRequest.ScrollRequested += _ => scrolled = true;

        _vm.GoToText = "0";
        _vm.GoToCommand.Execute(null);

        Assert.That(_vm.SelectedIndex, Is.EqualTo(-1));
        Assert.That(scrolled, Is.False);
    }
}
