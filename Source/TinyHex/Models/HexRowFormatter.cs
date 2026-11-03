using System.Text;

namespace TinyHex.Models;

/// <summary>
/// Formats a <see cref="HexRow"/> as <c>offset: hex dump | ascii</c>, e.g. <c>00000000: 41 42 00 7F | AB..</c>, and
/// tells where each byte appears in that text. A short last line is padded, so its <c>|</c> lines up with the others.
/// </summary>
public static class HexRowFormatter
{
    public static string Format(HexRow row)
    {
        var offset = OffsetText(row.Offset);
        var line = new StringBuilder(offset.Length + 4 * row.BytesPerLine + 4);
        line.Append(offset).Append(':');
        for (int i = 0; i < row.BytesPerLine; i++)
        {
            if (i < row.Bytes.Length)
                line.Append(' ').Append(row.Bytes[i].ToString("X2"));
            else
                line.Append("   ");
        }
        line.Append(" | ");
        foreach (byte b in row.Bytes)
            line.Append(b >= 32 && b <= 126 ? (char)b : '.');
        return line.ToString();
    }

    /// <summary>The position of the two hex digits of byte <paramref name="index"/> in the formatted line.</summary>
    public static int HexColumn(HexRow row, int index) => OffsetText(row.Offset).Length + 2 + 3 * index;

    /// <summary>The position of the character of byte <paramref name="index"/> in the formatted line.</summary>
    public static int AsciiColumn(HexRow row, int index) =>
        OffsetText(row.Offset).Length + 3 * row.BytesPerLine + 4 + index;

    // at least 8 digits; offsets from 4 GB on take more
    private static string OffsetText(long offset) => offset.ToString("X8");
}
