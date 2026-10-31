using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.IO;
using System.IO.MemoryMappedFiles;
using TinyHex.Models;
using TinyHex.Services;

namespace TinyHex.ViewModels;

public partial class MainViewModel(IDialogService dialogService) : ObservableObject, IDisposable
{
    private MemoryMappedFile? _mmf;
    private long _fileSize;

    [ObservableProperty]
    private MemoryMappedDataCollection? _records;

    public string WindowTitle => Filename.Length > 0 ? $"TinyHex - {Filename}" : "TinyHex";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    private string _filename = string.Empty;

    public IReadOnlyList<int> BytesPerLineOptions { get; } = [8, 16, 32, 64];

    [ObservableProperty]
    private int _bytesPerLine = 32;

    /// <summary>
    /// Restores the bytes per line (e.g. from saved user settings); values not offered in
    /// <see cref="BytesPerLineOptions"/> are ignored.
    /// </summary>
    public void RestoreBytesPerLine(int? value)
    {
        if (value is int bytesPerLine && BytesPerLineOptions.Contains(bytesPerLine))
            BytesPerLine = bytesPerLine;
    }

    [ObservableProperty]
    private string _goToText = string.Empty;

    [ObservableProperty]
    private int _selectedIndex = -1;

    /// <summary>
    /// Notifier that the view subscribes to in order to scroll the record list.
    /// </summary>
    public ScrollRequestNotifier ScrollRequest { get; } = new();

    partial void OnBytesPerLineChanged(int value) => LoadRecords();

    [RelayCommand]
    private void OpenFile()
    {
        var filePath = dialogService.ShowOpenFileDialog("Open Binary File", "All Files|*.*");
        if (filePath != null)
            LoadFile(filePath);
    }

    [RelayCommand]
    private void Drop(string[]? files)
    {
        // only support one file right now
        if (files?.Length > 0)
            LoadFile(files[0]);
    }

    private void LoadFile(string filePath)
    {
        MemoryMappedFile mmf;
        long fileSize;
        try
        {
            (mmf, fileSize) = OpenReadOnly(filePath);
        }
        catch (Exception ex)
        {
            dialogService.ShowError("Error", $"Could not open file:\n{ex.Message}");
            return;
        }

        // only replace the current file once the new one is open, so a failed open keeps it visible
        Records?.Dispose();
        _mmf?.Dispose();
        _mmf = mmf;
        _fileSize = fileSize;
        LoadRecords();
        Filename = Path.GetFileName(filePath);
    }

    /// <summary>
    /// Maps the file read-only, so read-only files and files other processes have open for writing can be viewed.
    /// </summary>
    private static (MemoryMappedFile Mmf, long FileSize) OpenReadOnly(string filePath)
    {
        var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        try
        {
            // an empty file cannot be memory mapped
            if (stream.Length == 0)
                throw new IOException("The file is empty.");

            var mmf = MemoryMappedFile.CreateFromFile(
                stream, null, 0, MemoryMappedFileAccess.Read, HandleInheritability.None, leaveOpen: false);
            return (mmf, stream.Length);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    [RelayCommand]
    private void GoTo()
    {
        if (!FileOffsetParser.TryParse(GoToText, out long offset) || offset < 0 || offset >= _fileSize)
            return;

        int index = (int)(offset / BytesPerLine);
        SelectedIndex = index;
        ScrollRequest.Request(index);
    }

    private void LoadRecords()
    {
        if (_mmf == null)
            return;

        Records?.Dispose();
        Records = new MemoryMappedDataCollection(_mmf, _fileSize, BytesPerLine);
    }

    public void Dispose()
    {
        Records?.Dispose();
        _mmf?.Dispose();
        GC.SuppressFinalize(this);
    }
}
