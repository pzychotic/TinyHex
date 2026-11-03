namespace TinyHex.Models;

/// <summary>A range of a row's bytes to highlight: <paramref name="Start"/> is the index of the first byte in the row.</summary>
public readonly record struct ByteHighlight(int Start, int Length, bool IsCurrent);

/// <summary>
/// The search hits to highlight: the first <paramref name="count"/> offsets of <paramref name="hits"/> (ascending,
/// not overlapping), each <paramref name="patternLength"/> bytes long, with hit <paramref name="current"/> as the
/// current one. Immutable as far as its readers see, as long as <paramref name="hits"/> only grows past
/// <paramref name="count"/> (as a <see cref="HitList"/> does); a new instance is published whenever hits are added or
/// the current hit changes, which redraws the rows.
/// </summary>
public sealed class SearchHighlights(IReadOnlyList<long> hits, int count, int patternLength, int? current)
{
    public int Count => count;

    public int PatternLength => patternLength;

    public int? Current => current;

    /// <summary>
    /// The parts of hits that fall into the row of <paramref name="rowLength"/> bytes at <paramref name="rowOffset"/>,
    /// in order. A hit that spans rows gives a range in each of them.
    /// </summary>
    public IReadOnlyList<ByteHighlight> For(long rowOffset, int rowLength)
    {
        long rowEnd = rowOffset + rowLength;
        List<ByteHighlight>? result = null;
        for (int i = FirstEndingAfter(rowOffset); i < count && hits[i] < rowEnd; i++)
        {
            long start = Math.Max(hits[i], rowOffset);
            long end = Math.Min(hits[i] + patternLength, rowEnd);
            (result ??= []).Add(new ByteHighlight((int)(start - rowOffset), (int)(end - start), i == current));
        }
        return (IReadOnlyList<ByteHighlight>?)result ?? [];
    }

    /// <summary>The index of the first hit that ends after <paramref name="offset"/>, or <see cref="Count"/>.</summary>
    private int FirstEndingAfter(long offset)
    {
        int low = 0, high = count;
        while (low < high)
        {
            int mid = low + (high - low) / 2;
            if (hits[mid] + patternLength > offset)
                high = mid;
            else
                low = mid + 1;
        }
        return low;
    }
}
