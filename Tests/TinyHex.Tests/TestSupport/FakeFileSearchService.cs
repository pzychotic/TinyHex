using System.Runtime.CompilerServices;
using TinyHex.Services;

namespace TinyHex.Tests.TestSupport;

/// <summary>
/// A search whose batches the test sends one by one with <see cref="Send"/>, which completes once the consumer has
/// handled the batch (asked for the next one, or ended the enumeration).
/// </summary>
public sealed class FakeFileSearchService : IFileSearchService
{
    private TaskCompletionSource<SearchBatch> _next = NewSource<SearchBatch>();
    private TaskCompletionSource<bool> _consumed = NewSource<bool>();

    public async IAsyncEnumerable<SearchBatch> SearchAsync(string filePath, long length, SearchPattern pattern,
        int maxHits, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var registration = cancellationToken.Register(() => _next.TrySetCanceled());
        try
        {
            while (true)
            {
                var batch = await _next.Task;
                yield return batch;
                if (batch.IsComplete)
                    yield break;
                _consumed.TrySetResult(true);
            }
        }
        finally
        {
            _consumed.TrySetResult(true);
        }
    }

    public Task Send(long position, bool isComplete, params long[] hits)
    {
        var consumed = _consumed = NewSource<bool>();
        var next = _next;
        _next = NewSource<SearchBatch>();
        next.SetResult(new SearchBatch(hits, position, isComplete, CapReached: false));
        return consumed.Task;
    }

    private static TaskCompletionSource<T> NewSource<T>() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
