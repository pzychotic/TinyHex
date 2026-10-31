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
        _fileSize = new FileInfo(filePath).Length;
        Records?.Dispose();
        _mmf?.Dispose();
        _mmf = MemoryMappedFile.CreateFromFile(filePath, FileMode.Open);
        LoadRecords();
        Filename = Path.GetFileName(filePath);
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
