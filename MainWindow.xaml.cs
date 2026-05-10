using System.ComponentModel;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace TinyHex
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private MemoryMappedFile? _mmf = null;
        private MemoryMappedViewAccessor? _accessor = null;
        private string _filePath = "";
        private long _fileSize = 0;
        private int _recordSize = 32;

        public event PropertyChangedEventHandler? PropertyChanged;

        private MemoryMappedDataCollection? _records;
        public MemoryMappedDataCollection? Records
        {
            get => _records;
            private set
            {
                _records = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Records)));
            }
        }

        public MainWindow()
        {
            InitializeComponent();
        }

        private void LoadRecords(int recordSize)
        {
            if(_accessor==null)
            {
                return;
            }
            _recordSize = recordSize;
            long count = _fileSize / recordSize;
            Records = new MemoryMappedDataCollection(_accessor, recordSize, count);
            DataContext = this;
        }

        private void RecordSizeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RecordSizeCombo.SelectedItem is ComboBoxItem item &&
                int.TryParse(item.Content.ToString(), out int newSize))
            {
                LoadRecords(newSize);
            }
        }

        private T? FindVisualChild<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj is T found) return found;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);
                var result = FindVisualChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }

        private void GoToTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                string input = GoToTextBox.Text;
                long offset = 0;
                bool success = false;
                
                if (input.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
                    success = long.TryParse(input.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out offset);
                }
                else
                {
                    success = long.TryParse(input, out offset);
                }

                if (success && offset >= 0 && offset < _fileSize)
                {
                    int index = (int)(offset / _recordSize);
                    RecordListView.SelectedIndex = index;

                    var panel = FindVisualChild<VirtualizingPanel>(RecordListView);
                    panel?.BringIndexIntoViewPublic(index);
                }
            }
        }

        private void MenuOpenFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Open Binary File",
                Filter = "All Files|*.*"
            };
            if (dlg.ShowDialog() == true)
            {
                _filePath = dlg.FileName;
                _fileSize = new FileInfo(_filePath).Length;
                _mmf?.Dispose();
                _mmf = MemoryMappedFile.CreateFromFile(_filePath, FileMode.Open);
                _accessor?.Dispose();
                _accessor = _mmf.CreateViewAccessor(0, _fileSize, MemoryMappedFileAccess.Read);
                LoadRecords(_recordSize > 0 ? _recordSize : 32);
            }
        }
    }
}
