using System.IO;
using System.Runtime.CompilerServices;

namespace TinyHex.Services;

/// <summary>Searches a file with <see cref="ByteSearch"/>, through a stream of its own.</summary>
public sealed class FileSearchService : IFileSearchService
{
    public async IAsyncEnumerable<SearchBatch> SearchAsync(string filePath, long length, SearchPattern pattern,
        int maxHits, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // share for writing too, like the memory mapped view, so files other processes write to can be searched
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite,
            bufferSize: 0, FileOptions.SequentialScan);

        await foreach (var batch in ByteSearch.FindAllAsync(stream, pattern, length, maxHits,
                           cancellationToken: cancellationToken))
            yield return batch;
    }
}
