namespace TinyHex.Services;

/// <summary>Searches a file for a byte pattern in the background.</summary>
public interface IFileSearchService
{
    /// <summary>
    /// Searches the first <paramref name="length"/> bytes of the file at <paramref name="filePath"/>, see
    /// <see cref="ByteSearch.FindAllAsync"/>. Fails with an <see cref="System.IO.IOException"/> or
    /// <see cref="UnauthorizedAccessException"/> when the file cannot be read.
    /// </summary>
    IAsyncEnumerable<SearchBatch> SearchAsync(string filePath, long length, SearchPattern pattern, int maxHits,
        CancellationToken cancellationToken = default);
}
