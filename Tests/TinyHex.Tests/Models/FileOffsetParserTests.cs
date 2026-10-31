using TinyHex.Models;

namespace TinyHex.Tests.Models;

[TestFixture]
public class FileOffsetParserTests
{
    [TestCase("0", 0L)]
    [TestCase("1234", 1234L)]
    [TestCase("0x1F", 0x1FL)]
    [TestCase("0X1f", 0x1FL)]
    [TestCase("0xFFFFFFFFFF", 0xFFFFFFFFFFL)]
    public void TryParse_ValidInput_ReturnsOffset(string input, long expected)
    {
        Assert.That(FileOffsetParser.TryParse(input, out long offset), Is.True);
        Assert.That(offset, Is.EqualTo(expected));
    }

    [TestCase("")]
    [TestCase("abc")]
    [TestCase("1F")]
    [TestCase("0x")]
    [TestCase("0xZZ")]
    public void TryParse_InvalidInput_ReturnsFalse(string input)
    {
        Assert.That(FileOffsetParser.TryParse(input, out _), Is.False);
    }
}
