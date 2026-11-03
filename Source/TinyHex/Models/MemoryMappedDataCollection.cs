using System.Collections;
using System.IO.MemoryMappedFiles;

namespace TinyHex.Models;

public class MemoryMappedDataCollection : IList<HexRow>, IList, IDisposable
{
    private readonly MemoryMappedFile _mmf;
    private readonly long _fileSize;
    private readonly int _recordSize;
    private readonly long _chunkSize;
    private readonly long _overlap;
    private readonly long _count;
    private readonly MappedChunk?[] _chunks = new MappedChunk?[3];

    public MemoryMappedDataCollection(
        MemoryMappedFile mmf,
        long fileSize,
        int recordSize,
        long chunkSize = 64L * 1024 * 1024,
        long overlap   = 4L  * 1024 * 1024)
    {
        _mmf        = mmf;
        _fileSize   = fileSize;
        _recordSize = recordSize;
        _chunkSize  = chunkSize;
        _overlap    = overlap;
        // the last line may be shorter than a record
        _count      = (fileSize + recordSize - 1) / recordSize;
    }

    public HexRow this[int index]
    {
        get
        {
            if (index < 0 || index >= _count)
                throw new ArgumentOutOfRangeException();

            long fileOffset = (long)index * _recordSize;
            byte[] buffer = new byte[Math.Min(_recordSize, _fileSize - fileOffset)];
            var chunk = GetOrLoadChunk(fileOffset);
            chunk.Accessor.ReadArray(fileOffset - chunk.StartOffset, buffer, 0, buffer.Length);
            return new HexRow(fileOffset, buffer, _recordSize);
        }
        set => throw new NotSupportedException();
    }

    public int Count => (int)Math.Min(_count, int.MaxValue);
    public bool IsReadOnly => true;

    private MappedChunk GetOrLoadChunk(long byteOffset)
    {
        foreach (var c in _chunks)
            if (c != null && c.Contains(byteOffset)) return c;

        long ci    = byteOffset / _chunkSize;
        long start = Math.Max(0, ci * _chunkSize - _overlap);
        long end   = Math.Min(_fileSize, (ci + 1) * _chunkSize + _overlap);

        int slot = FindEvictSlot(ci);
        _chunks[slot]?.Dispose();

        var chunk = new MappedChunk
        {
            ChunkIndex  = ci,
            StartOffset = start,
            EndOffset   = end,
            Accessor    = _mmf.CreateViewAccessor(start, end - start, MemoryMappedFileAccess.Read)
        };
        _chunks[slot] = chunk;
        return chunk;
    }

    private int FindEvictSlot(long targetChunkIndex)
    {
        for (int i = 0; i < _chunks.Length; i++)
            if (_chunks[i] == null) return i;

        int worst = 0;
        long maxDist = 0;
        for (int i = 0; i < _chunks.Length; i++)
        {
            long dist = Math.Abs(_chunks[i]!.ChunkIndex - targetChunkIndex);
            if (dist > maxDist) { maxDist = dist; worst = i; }
        }
        return worst;
    }

    public void Dispose()
    {
        foreach (var c in _chunks) c?.Dispose();
    }

    private sealed class MappedChunk : IDisposable
    {
        public long ChunkIndex;
        public long StartOffset;
        public long EndOffset;
        public MemoryMappedViewAccessor Accessor = null!;
        public bool Contains(long offset) => offset >= StartOffset && offset < EndOffset;
        public void Dispose() => Accessor?.Dispose();
    }

    public void Add(HexRow item) => throw new NotSupportedException();
    public void Clear() => throw new NotSupportedException();
    public bool Contains(HexRow item) => false;
    public void CopyTo(HexRow[] array, int arrayIndex) => throw new NotSupportedException();
    public int IndexOf(HexRow item) => throw new NotSupportedException();
    public void Insert(int index, HexRow item) => throw new NotSupportedException();
    public bool Remove(HexRow item) => throw new NotSupportedException();
    public void RemoveAt(int index) => throw new NotSupportedException();

    public IEnumerator<HexRow> GetEnumerator() => Enumerable.Empty<HexRow>().GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    object? IList.this[int index]
    {
        get => this[index];
        set => throw new NotSupportedException();
    }
    bool IList.IsReadOnly => true;
    bool IList.IsFixedSize => true;
    int ICollection.Count => Count;
    bool ICollection.IsSynchronized => false;
    object ICollection.SyncRoot => this;

    int IList.Add(object? value) => throw new NotSupportedException();
    void IList.Clear() => throw new NotSupportedException();
    bool IList.Contains(object? value) => false;
    int IList.IndexOf(object? value) => -1;
    void IList.Insert(int index, object? value) => throw new NotSupportedException();
    void IList.Remove(object? value) => throw new NotSupportedException();
    void IList.RemoveAt(int index) => throw new NotSupportedException();
    void ICollection.CopyTo(Array array, int index) => throw new NotSupportedException();
}
