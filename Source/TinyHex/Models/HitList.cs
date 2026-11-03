using System.Collections;

namespace TinyHex.Models;

/// <summary>
/// The file offsets of search hits, ascending. It can only grow, so a reader that remembers a count (like
/// <see cref="SearchHighlights"/>) keeps seeing the same hits up to that count while more are added.
/// </summary>
public sealed class HitList : IReadOnlyList<long>
{
    private readonly List<long> _hits = [];

    public int Count => _hits.Count;

    public long this[int index] => _hits[index];

    /// <summary>Appends hits that all lie behind the ones already in the list.</summary>
    public void AddRange(IEnumerable<long> hits) => _hits.AddRange(hits);

    /// <summary>The index of the first hit at or after <paramref name="offset"/>, from hit <paramref name="from"/> on.</summary>
    public int? FirstAtOrAfter(long offset, int from = 0)
    {
        int index = _hits.BinarySearch(from, _hits.Count - from, offset, null);
        if (index < 0)
            index = ~index;
        return index < _hits.Count ? index : null;
    }

    public IEnumerator<long> GetEnumerator() => _hits.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
