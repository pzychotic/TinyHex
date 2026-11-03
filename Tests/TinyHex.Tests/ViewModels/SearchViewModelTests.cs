using TinyHex.Models;
using TinyHex.Tests.TestSupport;
using TinyHex.ViewModels;

namespace TinyHex.Tests.ViewModels;

/// <summary>Navigation while a search is still running, with batches sent by the test.</summary>
[TestFixture]
public class SearchViewModelTests
{
    private const long FileSize = 1000;

    private FakeFileSearchService _service = null!;
    private SearchViewModel _search = null!;
    private long _selectedOffset;
    private readonly List<long> _wentTo = [];

    [SetUp]
    public void SetUp()
    {
        _service = new FakeFileSearchService();
        _selectedOffset = 0;
        _wentTo.Clear();
        _search = new SearchViewModel(_service, () => _selectedOffset, _wentTo.Add) { Delay = TimeSpan.Zero };
        _search.SetFile("test.bin", FileSize);
        _search.IsOpen = true;
    }

    /// <summary>
    /// Runs a test and then stops its search, still within the test: the search resumes on the test's
    /// synchronization context, which NUnit shuts down when the test method returns.
    /// </summary>
    private async Task Run(Func<Task> test)
    {
        try
        {
            await test();
        }
        finally
        {
            _search.Dispose();
            await _search.SearchTask;
        }
    }

    [Test]
    public Task FindNext_BeforeAnyHitBehindTheStart_WaitsForTheScanInsteadOfWrapping() => Run(async () =>
    {
        _selectedOffset = 500;
        _search.Text = "AB";
        await _service.Send(position: 300, isComplete: false, 10);

        _search.FindNextCommand.Execute(null);
        Assert.That(_wentTo, Is.Empty);

        await _service.Send(position: 600, isComplete: false, 550);
        Assert.That(_wentTo, Is.EqualTo(new long[] { 550 }));
        Assert.That(_search.Status, Is.EqualTo("2 of 2, searching… 60%"));

        // the scan's own jump has happened; a later hit does not move the selection again
        await _service.Send(position: FileSize, isComplete: true, 700);
        Assert.That(_wentTo, Is.EqualTo(new long[] { 550 }));
        Assert.That(_search.Status, Is.EqualTo("2 of 3"));
    });

    [Test]
    public Task FindPrevious_OnTheFirstHitWhileSearching_WaitsForTheLastHit() => Run(async () =>
    {
        _search.Text = "AB";
        await _service.Send(position: 300, isComplete: false, 10, 20);
        Assert.That(_wentTo, Is.EqualTo(new long[] { 10 }));

        _search.FindPreviousCommand.Execute(null);
        Assert.That(_wentTo, Is.EqualTo(new long[] { 10 }));

        await _service.Send(position: 600, isComplete: false, 400);
        Assert.That(_wentTo, Is.EqualTo(new long[] { 10 }));

        await _service.Send(position: FileSize, isComplete: true, 900);
        Assert.That(_wentTo, Is.EqualTo(new long[] { 10, 900 }));
        Assert.That(_search.Status, Is.EqualTo("4 of 4"));
    });

    [Test]
    public Task FindNext_OnTheLastHitFoundSoFar_TakesTheNextOneFound() => Run(async () =>
    {
        _search.Text = "AB";
        await _service.Send(position: 300, isComplete: false, 10);

        _search.FindNextCommand.Execute(null);
        await _service.Send(position: 600, isComplete: false, 400);

        Assert.That(_wentTo, Is.EqualTo(new long[] { 10, 400 }));
    });

    [Test]
    public Task Highlights_AreOnlyRepublishedWhenTheHitsChange() => Run(async () =>
    {
        _search.Text = "AB";
        await _service.Send(position: 300, isComplete: false, 10);
        var highlights = _search.Highlights;

        await _service.Send(position: 600, isComplete: false);

        Assert.That(_search.Highlights, Is.SameAs(highlights));
        Assert.That(_search.Status, Is.EqualTo("1 of 1, searching… 60%"));
    });

    [Test]
    public Task Typing_KeepsTheOldResultsAndChecksTheQueryOnlyOnceItHasSettled() => Run(async () =>
    {
        _search.Text = "AB";
        await _service.Send(position: FileSize, isComplete: true, 10);
        var highlights = _search.Highlights;

        _search.Delay = TimeSpan.FromHours(1);
        _search.Mode = SearchMode.Hex;
        _search.Text = "ABC";

        Assert.That(_search.HasNoMatches, Is.False);
        Assert.That(_search.Status, Is.EqualTo("1 of 1"));
        Assert.That(_search.Highlights, Is.SameAs(highlights));

        // the old hits no longer belong to the query
        _search.FindNextCommand.Execute(null);
        Assert.That(_wentTo, Is.EqualTo(new long[] { 10 }));
    });
}
