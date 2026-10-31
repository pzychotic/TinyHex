using System.Globalization;

namespace TinyHex.Models;

/// <summary>
/// Parses a user-entered file offset, either decimal or hexadecimal with a <c>0x</c> prefix.
/// </summary>
public static class FileOffsetParser
{
    public static bool TryParse(string input, out long offset)
    {
        if (input.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return long.TryParse(input.AsSpan(2), NumberStyles.HexNumber, null, out offset);

        return long.TryParse(input, out offset);
    }
}
