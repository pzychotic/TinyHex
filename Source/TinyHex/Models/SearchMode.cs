namespace TinyHex.Models;

/// <summary>How the find bar reads its query.</summary>
public enum SearchMode
{
    /// <summary>The query is text, encoded as UTF-8 (or UTF-16LE).</summary>
    Text,

    /// <summary>The query is hex digits, e.g. <c>DE AD BE EF</c>.</summary>
    Hex,
}
