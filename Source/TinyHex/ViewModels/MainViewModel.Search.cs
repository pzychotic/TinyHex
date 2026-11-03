using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.IO;
using TinyHex.Models;
using TinyHex.Services;

namespace TinyHex.ViewModels;

/// <summary>
/// The find bar: a live search of the whole file. Every change of the query restarts a background scan that collects
/// the hit offsets, jumps to the first hit at or after the selected row as soon as it is found, and highlights the hits.
/// </summary>
public partial class MainViewModel
{
    /// <summary>At most this many hits are kept; the status then shows the count with a "+".</summary>
    internal const int MaxSearchHits = 1_000_000;

    /// <summary>How long typing has to pause before a search starts.</summary>
    internal TimeSpan SearchDelay { get; set; } = TimeSpan.FromMilliseconds(250);

    /// <summary>The running (or last) search; lets tests wait for it.</summary>
    internal Task SearchTask { get; private set; } = Task.CompletedTask;

    private string? _filePath;
    private List<long> _hits = [];
    private int _patternLength;
    private int? _currentHit;
    private long _searchPosition;
    private bool _searchComplete;
    private bool _capReached;
    private bool _jumpPending;
    private CancellationTokenSource? _searchCts;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CloseSearchCommand))]
    private bool _isSearchOpen;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FindNextCommand), nameof(FindPreviousCommand))]
    private string _searchText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTextSearch))]
    private SearchMode _searchMode = SearchMode.Text;

    [ObservableProperty]
    private bool _searchMatchCase;

    [ObservableProperty]
    private bool _searchUtf16;

    /// <summary>"3 of 17", "Searching… 37%", "No matches", "Invalid hex", or empty.</summary>
    [ObservableProperty]
    private string _searchStatus = string.Empty;

    /// <summary>The query finds nothing (or is invalid); the find box shows an error border.</summary>
    [ObservableProperty]
    private bool _hasNoMatches;

    /// <summary>The hits for the rows to highlight; null while nothing is to be highlighted.</summary>
    [ObservableProperty]
    private SearchHighlights? _searchHighlights;

    /// <summary>Match case and UTF-16 only apply to a text query.</summary>
    public bool IsTextSearch => SearchMode == SearchMode.Text;

    private bool HasSearchText => SearchText.Length > 0;

    partial void OnIsSearchOpenChanged(bool value) => RestartSearch();
    partial void OnSearchTextChanged(string value) => RestartSearch();
    partial void OnSearchModeChanged(SearchMode value) => RestartSearch();
    partial void OnSearchMatchCaseChanged(bool value) => RestartSearch();
    partial void OnSearchUtf16Changed(bool value) => RestartSearch();

    [RelayCommand(CanExecute = nameof(IsSearchOpen))]
    private void CloseSearch() => IsSearchOpen = false;

    [RelayCommand(CanExecute = nameof(HasSearchText))]
    private void FindNext() => Find(+1);

    [RelayCommand(CanExecute = nameof(HasSearchText))]
    private void FindPrevious() => Find(-1);

    private void Find(int step)
    {
        // F3 on a closed find bar just reopens it, which searches again
        if (!IsSearchOpen)
        {
            IsSearchOpen = true;
            return;
        }
        if (_hits.Count == 0)
            return;

        if (_currentHit is not int current)
        {
            GoToHit(FirstHitFrom(SearchStartOffset()) ?? 0);
            return;
        }

        int next = current + step;
        if (next >= _hits.Count)
        {
            // more hits may still come: take the next one found instead of wrapping around too early
            if (!_searchComplete)
            {
                _jumpPending = true;
                return;
            }
            next = 0;
        }
        else if (next < 0)
        {
            next = _hits.Count - 1;
        }
        GoToHit(next);
    }

    /// <summary>Cancels the running search, and starts a new one if the find bar is open and has a valid query.</summary>
    private void RestartSearch()
    {
        _searchCts?.Cancel();
        _searchCts = null;
        _hits = [];
        _currentHit = null;
        _searchPosition = 0;
        _searchComplete = false;
        _capReached = false;
        _jumpPending = false;
        SearchHighlights = null;
        HasNoMatches = false;
        SearchStatus = string.Empty;
        SearchTask = Task.CompletedTask;

        if (!IsSearchOpen || _filePath == null)
            return;

        if (!SearchPattern.TryCreate(SearchText, SearchMode, SearchMatchCase, SearchUtf16, out var pattern, out var error))
        {
            HasNoMatches = error != null;
            SearchStatus = error ?? string.Empty;
            return;
        }

        _patternLength = pattern!.Length;
        SearchStatus = "Searching…";
        var cts = new CancellationTokenSource();
        _searchCts = cts;
        SearchTask = RunSearchAsync(pattern, _filePath, _fileSize, SearchStartOffset(), cts.Token);
    }

    private async Task RunSearchAsync(SearchPattern pattern, string filePath, long length, long startOffset,
        CancellationToken ct)
    {
        try
        {
            await Task.Delay(SearchDelay, ct);
            await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite,
                bufferSize: 0, FileOptions.SequentialScan);

            bool jumped = false;
            await foreach (var batch in ByteSearch.FindAllAsync(stream, pattern, length, MaxSearchHits,
                               cancellationToken: ct))
            {
                if (ct.IsCancellationRequested)
                    return;

                int firstNew = _hits.Count;
                _hits.AddRange(batch.Hits);
                _searchPosition = batch.Position;
                _searchComplete = batch.IsComplete;
                _capReached = batch.CapReached;

                if (!jumped)
                {
                    // the first hit at or after the start; once the search is done, wrap around to the first one
                    int? first = FirstHitFrom(startOffset, firstNew) ?? (_searchComplete && _hits.Count > 0 ? 0 : null);
                    if (first is int index)
                    {
                        jumped = true;
                        GoToHit(index);
                        continue;
                    }
                }
                else if (_jumpPending && (_hits.Count > firstNew || _searchComplete))
                {
                    _jumpPending = false;
                    GoToHit(_hits.Count > firstNew ? firstNew : 0);
                    continue;
                }
                UpdateSearchView();
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
                SearchStatus = $"Search failed: {ex.Message}";
            }
        }
    }

    /// <summary>The file offset of the selected row, where a new search looks for its first hit.</summary>
    private long SearchStartOffset() => SelectedIndex >= 0 ? (long)SelectedIndex * BytesPerLine : 0;

    /// <summary>The index of the first hit at or after <paramref name="offset"/>, from hit <paramref name="from"/> on.</summary>
    private int? FirstHitFrom(long offset, int from = 0)
    {
        int index = _hits.BinarySearch(from, _hits.Count - from, offset, null);
        if (index < 0)
            index = ~index;
        return index < _hits.Count ? index : null;
    }

    private long? CurrentHitOffset() => _currentHit is int current ? _hits[current] : null;

    private void GoToHit(int index)
    {
        _currentHit = index;
        SelectAndScrollTo((int)(_hits[index] / BytesPerLine));
        UpdateSearchView();
    }

    private void UpdateSearchView()
    {
        SearchHighlights = _hits.Count > 0 ? new SearchHighlights(_hits, _hits.Count, _patternLength, _currentHit) : null;
        HasNoMatches = _searchComplete && _hits.Count == 0;

        string total = _capReached ? $"{_hits.Count}+" : _hits.Count.ToString();
        if (_searchComplete)
        {
            SearchStatus = _currentHit is int current ? $"{current + 1} of {total}"
                : _hits.Count == 0 ? "No matches"
                : $"{total} matches";
        }
        else
        {
            int percent = _fileSize > 0 ? (int)(100 * _searchPosition / _fileSize) : 0;
            SearchStatus = _currentHit is int current
                ? $"{current + 1} of {total}, searching… {percent}%"
                : $"Searching… {percent}%";
        }
    }

    private void CancelSearch() => _searchCts?.Cancel();
}
