using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace TinyHex.Services;

/// <summary>
/// Hits found since the previous batch, as ascending file offsets, and how far the search got.
/// </summary>
/// <param name="Hits">File offsets of the new hits.</param>
/// <param name="Position">File offset up to which the search is done.</param>
/// <param name="IsComplete">True for the last batch: the search reached the end or <paramref name="CapReached"/>.</param>
/// <param name="CapReached">The search stopped after the maximum number of hits.</param>
public readonly record struct SearchBatch(long[] Hits, long Position, bool IsComplete, bool CapReached);

/// <summary>
/// Finds a byte pattern in a stream of any size, reading it in large buffers on a background thread.
/// </summary>
public static class ByteSearch
{
    public const int DefaultBufferSize = 4 * 1024 * 1024;

    /// <summary>How often a batch of hits is passed on while searching.</summary>
    private static readonly TimeSpan BatchInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Searches the first <paramref name="length"/> bytes of <paramref name="stream"/> for <paramref name="pattern"/>.
    /// Hits do not overlap: after a hit the search goes on behind it. Batches come at most every 100 ms, and the last
    /// one has <see cref="SearchBatch.IsComplete"/> set. The stream must not be used elsewhere until the enumeration
    /// has ended.
    /// </summary>
    public static async IAsyncEnumerable<SearchBatch> FindAllAsync(
        Stream stream, SearchPattern pattern, long length, int maxHits, int bufferSize = DefaultBufferSize,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var channel = Channel.CreateUnbounded<SearchBatch>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
        var producer = Task.Run(() =>
        {
            try
            {
                Scan(stream, pattern, length, maxHits, bufferSize, channel.Writer, cts.Token);
                channel.Writer.Complete();
            }
            catch (Exception ex)
            {
                channel.Writer.Complete(ex);
            }
        }, CancellationToken.None);

        try
        {
            await foreach (var batch in channel.Reader.ReadAllAsync(cancellationToken))
                yield return batch;
        }
        finally
        {
            // stop the scan and wait for it, so the caller may dispose the stream once the enumeration has ended
            cts.Cancel();
            await producer;
        }
    }

    private static void Scan(Stream stream, SearchPattern pattern, long length, int maxHits, int bufferSize,
        ChannelWriter<SearchBatch> writer, CancellationToken ct)
    {
        var needle = pattern.Bytes;
        int keep = needle.Length - 1;   // bytes carried over to the next buffer, for hits across buffers
        var buffer = new byte[bufferSize + keep];
        var folded = pattern.IgnoreCase ? new byte[buffer.Length] : null;
        var pending = new List<long>();
        var sinceBatch = Stopwatch.StartNew();
        long bufferOffset = 0;          // file offset of buffer[0]
        long nextAllowed = 0;           // hits do not overlap: the next one starts here at the earliest
        long remaining = length;
        int carried = 0;
        int hitCount = 0;

        stream.Position = 0;
        while (remaining > 0)
        {
            ct.ThrowIfCancellationRequested();
            int toRead = (int)Math.Min(bufferSize, remaining);
            int read = stream.ReadAtLeast(buffer.AsSpan(carried, toRead), toRead, throwOnEndOfStream: false);
            if (read == 0)
                break;  // the file got shorter
            remaining -= read;
            int valid = carried + read;

            ReadOnlySpan<byte> haystack = buffer.AsSpan(0, valid);
            if (folded != null)
            {
                SearchPattern.Fold(haystack, folded);
                haystack = folded.AsSpan(0, valid);
            }

            int position = (int)Math.Max(0, nextAllowed - bufferOffset);
            while (position <= valid - needle.Length)
            {
                int found = haystack[position..].IndexOf(needle);
                if (found < 0)
                    break;

                long hit = bufferOffset + position + found;
                pending.Add(hit);
                nextAllowed = hit + needle.Length;
                position += found + needle.Length;
                if (++hitCount >= maxHits)
                {
                    writer.TryWrite(new SearchBatch([.. pending], hit + needle.Length, IsComplete: true, CapReached: true));
                    return;
                }
            }

            carried = Math.Min(keep, valid);
            Buffer.BlockCopy(buffer, valid - carried, buffer, 0, carried);
            bufferOffset += valid - carried;

            if (sinceBatch.Elapsed >= BatchInterval && remaining > 0)
            {
                writer.TryWrite(new SearchBatch([.. pending], bufferOffset + carried, IsComplete: false, CapReached: false));
                pending.Clear();
                sinceBatch.Restart();
            }
        }
        writer.TryWrite(new SearchBatch([.. pending], length, IsComplete: true, CapReached: false));
    }
}
