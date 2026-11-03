using TinyHex.Models;

namespace TinyHex.Tests.Models;

[TestFixture]
public class SearchHighlightsTests
{
    [Test]
    public void For_RowWithHits_GivesTheirRanges()
    {
        var highlights = new SearchHighlights([2, 10, 13], 3, patternLength: 2, current: 1);

        Assert.That(highlights.For(0, 8), Is.EqualTo(new[] { new ByteHighlight(2, 2, false) }));
        Assert.That(highlights.For(8, 8), Is.EqualTo(new[]
        {
            new ByteHighlight(2, 2, true),
            new ByteHighlight(5, 2, false),
        }));
        Assert.That(highlights.For(16, 8), Is.Empty);
    }

    [Test]
    public void For_HitSpanningRows_IsSplitBetweenThem()
    {
        var highlights = new SearchHighlights([6], 1, patternLength: 4, current: 0);

        Assert.That(highlights.For(0, 8), Is.EqualTo(new[] { new ByteHighlight(6, 2, true) }));
        Assert.That(highlights.For(8, 8), Is.EqualTo(new[] { new ByteHighlight(0, 2, true) }));
    }

    [Test]
    public void For_HitLongerThanRow_CoversTheWholeRow()
    {
        var highlights = new SearchHighlights([2], 1, patternLength: 20, current: null);

        Assert.That(highlights.For(8, 8), Is.EqualTo(new[] { new ByteHighlight(0, 8, false) }));
    }

    [Test]
    public void For_IgnoresHitsBeyondCount()
    {
        var highlights = new SearchHighlights([2, 4], 1, patternLength: 1, current: null);

        Assert.That(highlights.For(0, 8), Is.EqualTo(new[] { new ByteHighlight(2, 1, false) }));
    }
}
