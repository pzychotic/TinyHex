using System.Collections;
using System.IO.MemoryMappedFiles;
using TinyHex.Models;
using TinyHex.Tests.TestSupport;

namespace TinyHex.Tests.Models;

[TestFixture]
public class MemoryMappedDataCollectionTests
{
    private static MemoryMappedFile Map(TempFile file) =>
        MemoryMappedFile.CreateFromFile(file.Path, FileMode.Open);

    [Test]
    public void Count_IsFileSizeDividedByRecordSize_DroppingPartialRecord()
    {
        using var file = TempFile.WithSequentialBytes(100);
        using var mmf = Map(file);
        using var records = new MemoryMappedDataCollection(mmf, 100, 16);

        Assert.That(records.Count, Is.EqualTo(6));
    }

    [Test]
    public void Indexer_FormatsOffsetHexAndAscii()
    {
        using var file = new TempFile([0x41, 0x42, 0x00, 0x7F, 0x20, 0x7E, 0x1F, 0xFF]);
        using var mmf = Map(file);
        using var records = new MemoryMappedDataCollection(mmf, 8, 4);

        Assert.That(records[0], Is.EqualTo("00000000: 41 42 00 7F | AB.."));
        Assert.That(records[1], Is.EqualTo("00000004: 20 7E 1F FF |  ~.."));
    }

    [Test]
    public void Indexer_AcrossChunkBoundaries_MatchesFileContent()
    {
        const int size = 1024;
        const int recordSize = 8;
        using var file = TempFile.WithSequentialBytes(size);
        using var mmf = Map(file);
        // tiny chunks force many chunk loads and evictions
        using var records = new MemoryMappedDataCollection(mmf, size, recordSize, chunkSize: 64, overlap: 8);

        // read backwards and forwards so that eviction of every slot is exercised
        var indices = Enumerable.Range(0, records.Count).Reverse().Concat(Enumerable.Range(0, records.Count));
        foreach (int i in indices)
        {
            long offset = (long)i * recordSize;
            var expectedHex = string.Join(" ", Enumerable.Range(0, recordSize).Select(b => ((byte)(offset + b)).ToString("X2")));
            Assert.That(records[i], Does.StartWith($"{offset:X8}: {expectedHex} | "));
        }
    }

    [TestCase(-1)]
    [TestCase(4)]
    public void Indexer_OutOfRange_Throws(int index)
    {
        using var file = TempFile.WithSequentialBytes(16);
        using var mmf = Map(file);
        using var records = new MemoryMappedDataCollection(mmf, 16, 4);

        Assert.Throws<ArgumentOutOfRangeException>(() => _ = records[index]);
    }

    [Test]
    public void Collection_IsReadOnly()
    {
        using var file = TempFile.WithSequentialBytes(16);
        using var mmf = Map(file);
        using var records = new MemoryMappedDataCollection(mmf, 16, 4);
        IList list = records;

        Assert.That(records.IsReadOnly, Is.True);
        Assert.That(list.IsFixedSize, Is.True);
        Assert.Throws<NotSupportedException>(() => records.Add("x"));
        Assert.Throws<NotSupportedException>(() => records.RemoveAt(0));
        Assert.Throws<NotSupportedException>(() => records[0] = "x");
        Assert.Throws<NotSupportedException>(() => list.Clear());
    }
}
