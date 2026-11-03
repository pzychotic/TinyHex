using System.Windows.Controls;
using System.Windows.Documents;
using TinyHex.Behaviors;
using TinyHex.Models;

namespace TinyHex.Tests.Behaviors;

[TestFixture]
public class SearchHighlightTests
{
    private static string[] RunTexts(TextBlock textBlock) =>
        textBlock.Inlines.OfType<Run>().Select(r => r.Text).ToArray();

    [Test]
    public void Row_WithoutHighlights_ShowsTheLine()
    {
        var textBlock = new TextBlock();

        SearchHighlight.SetRow(textBlock, new HexRow(0, "ABCD"u8.ToArray(), 4));

        Assert.That(textBlock.Text, Is.EqualTo("00000000: 41 42 43 44 | ABCD"));
    }

    [Test]
    public void Row_WithHit_HighlightsHexAndAsciiColumns()
    {
        var textBlock = new TextBlock();
        SearchHighlight.SetRow(textBlock, new HexRow(0, "ABCD"u8.ToArray(), 4));

        SearchHighlight.SetHighlights(textBlock, new SearchHighlights([1], 1, patternLength: 2, current: 0));

        Assert.That(RunTexts(textBlock), Is.EqualTo(new[] { "00000000: 41 ", "42 43", " 44 | A", "BC", "D" }));
    }

    [Test]
    public void Highlights_Cleared_ShowsThePlainLineAgain()
    {
        var textBlock = new TextBlock();
        SearchHighlight.SetRow(textBlock, new HexRow(0, "ABCD"u8.ToArray(), 4));
        SearchHighlight.SetHighlights(textBlock, new SearchHighlights([0], 1, patternLength: 4, current: null));

        SearchHighlight.SetHighlights(textBlock, null);

        Assert.That(RunTexts(textBlock), Is.EqualTo(new[] { "00000000: 41 42 43 44 | ABCD" }));
    }
}
