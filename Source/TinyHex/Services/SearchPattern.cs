using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using TinyHex.Models;

namespace TinyHex.Services;

/// <summary>
/// The bytes to search for. With <see cref="IgnoreCase"/>, ASCII letters match in either case; the bytes are then
/// stored folded to lower case (see <see cref="Fold"/>).
/// </summary>
public sealed class SearchPattern
{
    private static readonly byte[] FoldTable = BuildFoldTable();

    private SearchPattern(byte[] bytes, bool ignoreCase)
    {
        IgnoreCase = ignoreCase;
        Bytes = ignoreCase ? Fold(bytes) : bytes;
    }

    public byte[] Bytes { get; }

    public bool IgnoreCase { get; }

    public int Length => Bytes.Length;

    /// <summary>
    /// Builds the pattern for a find bar query. Fails without an error for an empty query, and with an error for
    /// invalid hex.
    /// </summary>
    /// <param name="matchCase">Text only: match ASCII letters in their exact case.</param>
    /// <param name="utf16">Text only: encode the text as UTF-16LE instead of UTF-8.</param>
    public static bool TryCreate(string text, SearchMode mode, bool matchCase, bool utf16,
        [NotNullWhen(true)] out SearchPattern? pattern, out string? error)
    {
        pattern = null;
        error = null;
        if (text.Length == 0)
            return false;

        if (mode == SearchMode.Hex)
        {
            if (!TryParseHex(text, out var bytes))
            {
                error = "Invalid hex";
                return false;
            }
            if (bytes.Length == 0)
                return false;
            pattern = new SearchPattern(bytes, ignoreCase: false);
            return true;
        }

        var encoded = (utf16 ? Encoding.Unicode : Encoding.UTF8).GetBytes(text);
        pattern = new SearchPattern(encoded, ignoreCase: !matchCase);
        return true;
    }

    /// <summary>
    /// Parses hex digits like <c>DEADBEEF</c>, <c>DE AD BE EF</c> or <c>0xDE 0xAD</c>: tokens are separated by white
    /// space, may start with <c>0x</c>, and each must hold whole bytes.
    /// </summary>
    public static bool TryParseHex(string text, out byte[] bytes)
    {
        bytes = [];
        var digits = new StringBuilder(text.Length);
        foreach (var token in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            var tokenDigits = token.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? token[2..] : token;
            if (tokenDigits.Length == 0 || tokenDigits.Length % 2 != 0)
                return false;
            digits.Append(tokenDigits);
        }

        var result = new byte[digits.Length / 2];
        if (Convert.FromHexString(digits.ToString(), result, out _, out _) != OperationStatus.Done)
            return false;
        bytes = result;
        return true;
    }

    /// <summary>Returns a copy of <paramref name="bytes"/> with ASCII upper-case letters made lower case.</summary>
    public static byte[] Fold(ReadOnlySpan<byte> bytes)
    {
        var folded = new byte[bytes.Length];
        Fold(bytes, folded);
        return folded;
    }

    /// <summary>Copies <paramref name="source"/> to <paramref name="destination"/>, ASCII letters made lower case.</summary>
    public static void Fold(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        for (int i = 0; i < source.Length; i++)
            destination[i] = FoldTable[source[i]];
    }

    private static byte[] BuildFoldTable()
    {
        var table = new byte[256];
        for (int i = 0; i < 256; i++)
            table[i] = (byte)(i is >= 'A' and <= 'Z' ? i + ('a' - 'A') : i);
        return table;
    }
}
