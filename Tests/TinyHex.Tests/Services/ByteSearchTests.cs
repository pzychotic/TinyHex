using System.Text;
using TinyHex.Models;
using TinyHex.Services;

namespace TinyHex.Tests.Services;

[TestFixture]
public class ByteSearchTests
{
    private static SearchPattern Text(string text, bool matchCase = true)
    {
        SearchPattern.TryCreate(text, SearchMode.Text, matchCase, false, out var pattern, out _);
        return pattern!;
    }

    private static SearchPattern Hex(string hex)
    {
        SearchPattern.TryCreate(hex, SearchMode.Hex, false, false, out var pattern, out _);
        return pattern!;
    }

    private static async Task<(List<long> Hits, SearchBatch Last)> FindAll(byte[] data, SearchPattern pattern,
        long? length = null, int maxHits = 1000, int bufferSize = ByteSearch.DefaultBufferSize)
    {
        using var stream = new MemoryStream(data);
        var hits = new List<long>();
        SearchBatch last = default;
        await foreach (var batch in ByteSearch.FindAllAsync(stream, pattern, length ?? data.Length, maxHits, bufferSize))
        {
            hits.AddRange(batch.Hits);
            last = batch;
        }
        return (hits, last);
    }

    [Test]
    public async Task FindAll_FindsEveryHitInOrder()
    {
        var (hits, last) = await FindAll(Encoding.ASCII.GetBytes("xxABxxxABxAB"), Text("AB"));

        Assert.That(hits, Is.EqualTo(new long[] { 2, 7, 10 }));
        Assert.That(last.IsComplete, Is.True);
        Assert.That(last.CapReached, Is.False);
        Assert.That(last.Position, Is.EqualTo(12));
    }

    [TestCase(15)]  // spans the first two buffers
    [TestCase(14)]  // ends at the first buffer's last byte
    [TestCase(16)]  // starts at the second buffer
    [TestCase(61)]  // ends at the end of the data
    public async Task FindAll_HitAcrossBufferBoundaries_IsFound(int offset)
    {
        var data = new byte[64];
        data[offset] = 1; data[offset + 1] = 2; data[offset + 2] = 3;

        var (hits, _) = await FindAll(data, Hex("01 02 03"), bufferSize: 16);

        Assert.That(hits, Is.EqualTo(new long[] { offset }));
    }

    [Test]
    public async Task FindAll_TinyBuffers_MatchesNaiveSearch()
    {
        var random = new Random(42);
        var data = new byte[5000];
        random.NextBytes(data);
        for (int i = 0; i < data.Length; i++)
            data[i] = (byte)(data[i] % 3);   // few distinct values: many hits, many near misses
        var pattern = Hex("01 02 01");

        var (hits, _) = await FindAll(data, pattern, bufferSize: 7);

        var expected = new List<long>();
        for (int i = 0; i + 3 <= data.Length; i++)
        {
            if (data[i] == 1 && data[i + 1] == 2 && data[i + 2] == 1)
            {
                expected.Add(i);
                i += 2;   // no overlapping hits
            }
        }
        Assert.That(hits, Is.EqualTo(expected));
    }

    [Test]
    public async Task FindAll_HitsDoNotOverlap()
    {
        var (hits, _) = await FindAll("aaaaa"u8.ToArray(), Text("aa"));

        Assert.That(hits, Is.EqualTo(new long[] { 0, 2 }));
    }

    [Test]
    public async Task FindAll_IgnoringCase_MatchesEitherCase()
    {
        var data = Encoding.ASCII.GetBytes("abc ABC aBc xbc");

        var (ignoring, _) = await FindAll(data, Text("Abc", matchCase: false), bufferSize: 4);
        var (matching, _) = await FindAll(data, Text("aBc"), bufferSize: 4);

        Assert.That(ignoring, Is.EqualTo(new long[] { 0, 4, 8 }));
        Assert.That(matching, Is.EqualTo(new long[] { 8 }));
    }

    [Test]
    public async Task FindAll_CapReached_StopsAndSaysSo()
    {
        var (hits, last) = await FindAll("aaaaaa"u8.ToArray(), Text("a"), maxHits: 3);

        Assert.That(hits, Is.EqualTo(new long[] { 0, 1, 2 }));
        Assert.That(last.IsComplete, Is.True);
        Assert.That(last.CapReached, Is.True);
    }

    [Test]
    public async Task FindAll_SearchesOnlyTheGivenLength()
    {
        var (hits, _) = await FindAll("ab ab ab"u8.ToArray(), Text("ab"), length: 4);

        Assert.That(hits, Is.EqualTo(new long[] { 0 }));
    }

    [Test]
    public async Task FindAll_NoHit_GivesOneCompleteBatch()
    {
        var (hits, last) = await FindAll(new byte[100], Hex("FF"));

        Assert.That(hits, Is.Empty);
        Assert.That(last.IsComplete, Is.True);
    }

    [Test]
    public void FindAll_Cancelled_Throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.CatchAsync<OperationCanceledException>(async () =>
        {
            using var stream = new MemoryStream(new byte[100]);
            await foreach (var _ in ByteSearch.FindAllAsync(stream, Hex("00"), 100, 1000, cancellationToken: cts.Token))
            {
            }
        });
    }
}
