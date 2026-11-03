using TinyHex.Models;

namespace TinyHex.Tests.Models;

[TestFixture]
public class HexRowFormatterTests
{
    [Test]
    public void Columns_PointAtEachByte()
    {
        var row = new HexRow(0x20, [0x41, 0x42, 0x00, 0x7F], 4);
        var text = row.ToString();

        Assert.That(text, Is.EqualTo("00000020: 41 42 00 7F | AB.."));
        for (int i = 0; i < 4; i++)
        {
            Assert.That(text.Substring(HexRowFormatter.HexColumn(row, i), 2), Is.EqualTo(row.Bytes[i].ToString("X2")));
            char expected = row.Bytes[i] is >= 32 and <= 126 ? (char)row.Bytes[i] : '.';
            Assert.That(text[HexRowFormatter.AsciiColumn(row, i)], Is.EqualTo(expected));
        }
    }

    [Test]
    public void Columns_ShortLastRow_StayAligned()
    {
        var row = new HexRow(4, [0x45, 0x46], 4);
        var text = row.ToString();

        Assert.That(text, Is.EqualTo("00000004: 45 46       | EF"));
        Assert.That(text[HexRowFormatter.AsciiColumn(row, 1)], Is.EqualTo('F'));
    }

    [Test]
    public void Columns_OffsetBeyond4GB_ShiftWithLongerOffset()
    {
        var row = new HexRow(0x1_0000_0000, [0xAB], 1);
        var text = row.ToString();

        Assert.That(text, Is.EqualTo("100000000: AB | ."));
        Assert.That(text.Substring(HexRowFormatter.HexColumn(row, 0), 2), Is.EqualTo("AB"));
        Assert.That(text[HexRowFormatter.AsciiColumn(row, 0)], Is.EqualTo('.'));
    }
}
