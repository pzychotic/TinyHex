using System.Text;
using TinyHex.Models;
using TinyHex.Services;

namespace TinyHex.Tests.Services;

[TestFixture]
public class SearchPatternTests
{
    [TestCase("DEADBEEF", new byte[] { 0xDE, 0xAD, 0xBE, 0xEF })]
    [TestCase("de ad be ef", new byte[] { 0xDE, 0xAD, 0xBE, 0xEF })]
    [TestCase("0xDE 0xad", new byte[] { 0xDE, 0xAD })]
    [TestCase("  0A\t0b  ", new byte[] { 0x0A, 0x0B })]
    [TestCase("0x0102", new byte[] { 0x01, 0x02 })]
    public void TryParseHex_Valid(string text, byte[] expected)
    {
        Assert.That(SearchPattern.TryParseHex(text, out var bytes), Is.True);
        Assert.That(bytes, Is.EqualTo(expected));
    }

    [TestCase("ABC")]
    [TestCase("A B")]
    [TestCase("GG")]
    [TestCase("0x")]
    [TestCase("12-34")]
    public void TryParseHex_Invalid(string text)
    {
        Assert.That(SearchPattern.TryParseHex(text, out _), Is.False);
    }

    [TestCase(SearchMode.Text)]
    [TestCase(SearchMode.Hex)]
    public void TryCreate_EmptyText_FailsWithoutError(SearchMode mode)
    {
        Assert.That(SearchPattern.TryCreate("", mode, false, false, out var pattern, out var error), Is.False);
        Assert.That(pattern, Is.Null);
        Assert.That(error, Is.Null);
    }

    [Test]
    public void TryCreate_InvalidHex_FailsWithError()
    {
        Assert.That(SearchPattern.TryCreate("ABC", SearchMode.Hex, false, false, out _, out var error), Is.False);
        Assert.That(error, Is.EqualTo("Invalid hex"));
    }

    [Test]
    public void TryCreate_Hex_MatchesExactBytes()
    {
        Assert.That(SearchPattern.TryCreate("41 61", SearchMode.Hex, false, false, out var pattern, out _), Is.True);
        Assert.That(pattern!.Bytes, Is.EqualTo(new byte[] { 0x41, 0x61 }));
        Assert.That(pattern.IgnoreCase, Is.False);
    }

    [Test]
    public void TryCreate_TextIgnoringCase_FoldsAsciiLetters()
    {
        Assert.That(SearchPattern.TryCreate("AbC-ä", SearchMode.Text, false, false, out var pattern, out _), Is.True);
        Assert.That(pattern!.IgnoreCase, Is.True);
        Assert.That(pattern.Bytes, Is.EqualTo(Encoding.UTF8.GetBytes("abc-ä")));
    }

    [Test]
    public void TryCreate_TextMatchingCase_KeepsBytes()
    {
        Assert.That(SearchPattern.TryCreate("AbC", SearchMode.Text, true, false, out var pattern, out _), Is.True);
        Assert.That(pattern!.IgnoreCase, Is.False);
        Assert.That(pattern.Bytes, Is.EqualTo("AbC"u8.ToArray()));
    }

    [Test]
    public void TryCreate_Utf16_EncodesLittleEndian()
    {
        Assert.That(SearchPattern.TryCreate("AB", SearchMode.Text, true, true, out var pattern, out _), Is.True);
        Assert.That(pattern!.Bytes, Is.EqualTo(new byte[] { 0x41, 0x00, 0x42, 0x00 }));
    }
}
