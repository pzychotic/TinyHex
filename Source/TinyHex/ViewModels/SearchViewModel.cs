using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.IO;
using TinyHex.Models;
using TinyHex.Services;

namespace TinyHex.ViewModels;

/// <summary>
/// The find bar: a live search of the whole file. Once the query has not changed for <see cref="Delay"/>, a background
/// scan collects the hit offsets, jumps to the first hit at or after the selected row as soon as it is found, and
/// highlights the hits.
/// </summary>
/// <remarks>
/// Threading: use this view model from one thread only, the UI thread. The scan itself runs on a background thread
/// (see <see cref="ByteSearch"/>), but its batches are applied after awaits that resume on the caller's
/// <see cref="SynchronizationContext"/> (the WPF dispatcher), so all state here is only ever touched on that thread.
/// Each search waits for the one before it to have stopped, so two searches never apply batches at the same time.
/// </remarks>
public sealed partial class SearchViewModel : ObservableObject, IDisposable
{
    /// <summary>At most this many hits are kept; the status then shows the count with a "+".</summary>
    internal const int MaxHits = 1_000_000;

    /// <summary>Where Find next/previous goes once the running scan has got that far.</summary>
    private enum PendingJump { None, Next, Last }

    private readonly IFileSearchService _searchService;
    private readonly Func<long> _selectedOffset;
    private readonly Action<long> _goToOffset;

    private string? _filePath;
    private long _fileSize;
    private HitList _hits = new();
    private int _patternLength;
    private int? _currentHit;
    private long _searchPosition;
    private bool _searchComplete;
    private bool _capReached;
    private bool _queryPending;
    private PendingJump _pendingJump;
    private CancellationTokenSource? _searchCts;

    /// <param name="searchService">Scans the file.</param>
    /// <param name="selectedOffset">The file offset of the selected row, where a search looks for its first hit.</param>
    /// <param name="goToOffset">Selects the row with the given file offset and scrolls to it.</param>
    public SearchViewModel(IFileSearchService searchService, Func<long> selectedOffset, Action<long> goToOffset)
    {
        _searchService = searchService;
        _selectedOffset = selectedOffset;
        _goToOffset = goToOffset;
    }

    /// <summary>How long the query has to stay the same before a search starts.</summary>
    internal TimeSpan Delay { get; set; } = TimeSpan.FromMilliseconds(250);

    /// <summary>The running (or last) search; lets tests wait for it.</summary>
    internal Task SearchTask { get; private set; } = Task.CompletedTask;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CloseCommand))]
    private bool _isOpen;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FindNextCommand), nameof(FindPreviousCommand))]
    private string _text = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTextMode))]
    private SearchMode _mode = SearchMode.Text;

    [ObservableProperty]
    private bool _matchCase;

    [ObservableProperty]
    private bool _utf16;

    /// <summary>"3 of 17", "Searching… 37%", "No matches", "Invalid hex", or empty.</summary>
    [ObservableProperty]
    private string _status = string.Empty;

    /// <summary>The query finds nothing (or is invalid); the find box shows an error border.</summary>
    [ObservableProperty]
    private bool _hasNoMatches;

    /// <summary>The hits for the rows to highlight; null while nothing is to be highlighted.</summary>
    [ObservableProperty]
    private SearchHighlights? _highlights;

    /// <summary>Match case and UTF-16 only apply to a text query.</summary>
    public bool IsTextMode => Mode == SearchMode.Text;

    /// <summary>The file offset of the current hit, if any.</summary>
    public long? CurrentHitOffset => _currentHit is int current ? _hits[current] : null;

    private bool HasText => Text.Length > 0;

    /// <summary>Sets the file to search (null for none), and searches it with the open find bar's query.</summary>
    public void SetFile(string? filePath, long fileSize)
    {
        _filePath = filePath;
        _fileSize = fileSize;
        Restart();
    }

    partial void OnIsOpenChanged(bool value) => Restart();
    partial void OnTextChanged(string value) => Restart();
    partial void OnModeChanged(SearchMode value) => Restart();
    partial void OnMatchCaseChanged(bool value) => Restart();
    partial void OnUtf16Changed(bool value) => Restart();

    [RelayCommand(CanExecute = nameof(IsOpen))]
    private void Close() => IsOpen = false;

    [RelayCommand(CanExecute = nameof(HasText))]
    private void FindNext() => Find(+1);

    [RelayCommand(CanExecute = nameof(HasText))]
    private void FindPrevious() => Find(-1);

    private void Find(int step)
    {
        // F3 on a closed find bar just reopens it, which searches again
        if (!IsOpen)
        {
            IsOpen = true;
            return;
        }
        // the hits are still those of an older query; the new search jumps to its first hit anyway
        if (_queryPending || _hits.Count == 0)
            return;

        if (_currentHit is not int current)
        {
            // the first hit at or after the selected row; wrap around only once the scan is done, else the scan
            // jumps to the first hit behind its start as soon as it finds it
            if (_hits.FirstAtOrAfter(_selectedOffset()) is int first)
                GoToHit(first);
            else if (_searchComplete)
                GoToHit(0);
            return;
        }

        int next = current + step;
        if (next < 0 || next >= _hits.Count)
        {
            // more hits may still come: wait for them instead of wrapping around too early
            if (!_searchComplete)
            {
                _pendingJump = step > 0 ? PendingJump.Next : PendingJump.Last;
                return;
            }
            next = next < 0 ? _hits.Count - 1 : 0;
        }
        _pendingJump = PendingJump.None;
        GoToHit(next);
    }

    /// <summary>
    /// Cancels the running search. Clears the results right away if there is nothing to search; else starts a new
    /// search that keeps the old results on show until the query has settled.
    /// </summary>
    private void Restart()
    {
        CancelSearch();
        if (!IsOpen || _filePath == null || !HasText)
        {
            _queryPending = false;
            Reset();
            return;
        }

        _queryPending = true;
        var cts = new CancellationTokenSource();
        _searchCts = cts;
        SearchTask = RunSearchAsync(SearchTask, _filePath, _fileSize, cts);
    }

    private async Task RunSearchAsync(Task previous, string filePath, long length, CancellationTokenSource cts)
    {
        var ct = cts.Token;
        try
        {
            await previous;
            await Task.Delay(Delay, ct);

            // the query has settled: only now drop the old results and check it, so typing does not flash errors
            Reset();
            _queryPending = false;
            if (!SearchPattern.TryCreate(Text, Mode, MatchCase, Utf16, out var pattern, out var error))
            {
                HasNoMatches = error != null;
                Status = error ?? string.Empty;
                return;
            }

            _patternLength = pattern.Length;
            long startOffset = _selectedOffset();
            Status = "Searching…";
            // no ConfigureAwait(false): each batch has to be applied on the UI thread, see the remarks
            await foreach (var batch in _searchService.SearchAsync(filePath, length, pattern, MaxHits, ct))
            {
                if (ct.IsCancellationRequested)
                    return;
                ApplyBatch(batch, startOffset);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (!ct.IsCancellationRequested)
            {
                HasNoMatches = true;
                Status = $"Search failed: {ex.Message}";
            }
        }
        finally
        {
            if (_searchCts == cts)
                _searchCts = null;
            cts.Dispose();
        }
    }

    private void ApplyBatch(SearchBatch batch, long startOffset)
    {
        int firstNew = _hits.Count;
        _hits.AddRange(batch.Hits);
        _searchPosition = batch.Position;
        _searchComplete = batch.IsComplete;
        _capReached = batch.CapReached;
        bool hasNew = _hits.Count > firstNew;

        int? jumpTo = null;
        if (_currentHit is null)
        {
            // the first hit at or after the start; once the search is done, wrap around to the first one
            jumpTo = _hits.FirstAtOrAfter(startOffset, firstNew) ?? (_searchComplete && _hits.Count > 0 ? 0 : null);
        }
        else if (_pendingJump == PendingJump.Next && (hasNew || _searchComplete))
        {
            jumpTo = hasNew ? firstNew : 0;
        }
        else if (_pendingJump == PendingJump.Last && _searchComplete)
        {
            jumpTo = _hits.Count - 1;
        }

        if (jumpTo is int index)
        {
            _pendingJump = PendingJump.None;
            GoToHit(index);
        }
        else
        {
            UpdateView();
        }
    }

    private void GoToHit(int index)
    {
        _currentHit = index;
        _goToOffset(_hits[index]);
        UpdateView();
    }

    private void UpdateView()
    {
        // a new instance redraws every visible row, so only publish one when the hits or the current hit changed
        if (_hits.Count == 0)
            Highlights = null;
        else if (Highlights is not { } shown || shown.Count != _hits.Count || shown.Current != _currentHit)
            Highlights = new SearchHighlights(_hits, _hits.Count, _patternLength, _currentHit);
        HasNoMatches = _searchComplete && _hits.Count == 0;

        string total = _capReached ? $"{_hits.Count}+" : _hits.Count.ToString();
        if (_searchComplete)
        {
            Status = _currentHit is int current ? $"{current + 1} of {total}"
                : _hits.Count == 0 ? "No matches"
                : $"{total} matches";
        }
        else
        {
            int percent = _fileSize > 0 ? (int)(100 * _searchPosition / _fileSize) : 0;
            Status = _currentHit is int current
                ? $"{current + 1} of {total}, searching… {percent}%"
                : $"Searching… {percent}%";
        }
    }

    private void Reset()
    {
        _hits = new HitList();
        _patternLength = 0;
        _currentHit = null;
        _searchPosition = 0;
        _searchComplete = false;
        _capReached = false;
        _pendingJump = PendingJump.None;
        Highlights = null;
        HasNoMatches = false;
        Status = string.Empty;
    }

    private void CancelSearch()
    {
        // the search disposes its token source once it has stopped
        _searchCts?.Cancel();
        _searchCts = null;
    }

    public void Dispose() => CancelSearch();
}
