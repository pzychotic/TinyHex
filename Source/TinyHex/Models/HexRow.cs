namespace TinyHex.Models;

/// <summary>
/// One line of the hex view: <paramref name="Bytes"/> read at file offset <paramref name="Offset"/>, shown in a layout of
/// <paramref name="BytesPerLine"/> bytes. The last line of a file may hold fewer bytes than that.
/// </summary>
public sealed record HexRow(long Offset, byte[] Bytes, int BytesPerLine)
{
    /// <summary>The formatted line, see <see cref="HexRowFormatter"/>.</summary>
    public override string ToString() => HexRowFormatter.Format(this);
}
