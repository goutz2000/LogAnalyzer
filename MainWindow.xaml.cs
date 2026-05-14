using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Text.RegularExpressions;

namespace LogAnalyzer
{
    public partial class MainWindow : Window
    {
        private ObservableCollection<LogEntryViewModel> allEntries = new();
        private string currentFilter = "All";

        public MainWindow()
        {
            InitializeComponent();
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "XML Log Files (*.xml)|*.xml|All Files (*.*)|*.*",
                Title = "Open Log File"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                LoadLogFile(openFileDialog.FileName);
            }
        }

        private void LoadLogFile(string filePath)
        {
            try
            {
                StatusLabel.Text = "Loading file...";
                allEntries.Clear();

                var doc = XDocument.Load(filePath);
                var entries = doc.Descendants("entry");

                int count = 0;
                foreach (var entry in entries)
                {
                    var time = entry.Attribute("time")?.Value ?? "";
                    var ms = entry.Attribute("ms")?.Value ?? "";
                    var type = entry.Attribute("type")?.Value ?? "info";
                    var textElement = entry.Element("text");
                    var text = textElement?.Value ?? "";

                    var viewModel = new LogEntryViewModel
                    {
                        Time = time,
                        Milliseconds = ms,
                        Type = type,
                        RawText = text
                    };

                    // Extract bitstream data
                    ExtractBitstreams(text, viewModel);

                    allEntries.Add(viewModel);
                    count++;
                }

                FilterEntries("All");
                FilePathLabel.Text = System.IO.Path.GetFileName(filePath);
                StatusLabel.Text = $"Loaded {count} entries";
                CountLabel.Text = $"Entries: {count}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusLabel.Text = "Error loading file";
            }
        }

        private void ExtractBitstreams(string text, LogEntryViewModel viewModel)
        {
            // Find all binary strings in square brackets
            var pattern = @"\[(0[01]*|1[01]*)\]";
            var regex = new Regex(pattern);
            var matches = regex.Matches(text);

            foreach (Match match in matches)
            {
                var binary = match.Groups[1].Value;
                if (binary.Length > 0)
                {
                    viewModel.Bitstreams.Add(new BitstreamData
                    {
                        Label = $"Bitstream #{viewModel.Bitstreams.Count + 1}",
                        Binary = binary
                    });
                }
            }
        }

        private void EntriesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (EntriesListBox.SelectedItem is LogEntryViewModel selected)
            {
                TimestampLabel.Text = $"{selected.Time} (ms: {selected.Milliseconds})";
                TypeLabel.Text = selected.Type.ToUpper();
                RawTextLabel.Text = selected.RawText;
                BitstreamItemsControl.ItemsSource = selected.Bitstreams;
                DetailsPanel.Visibility = Visibility.Visible;
            }
        }

        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                var filterType = button.Tag?.ToString() ?? "All";
                currentFilter = filterType;
                FilterEntries(filterType);
            }
        }

        private void FilterEntries(string filterType)
        {
            var filtered = filterType == "All"
                ? new ObservableCollection<LogEntryViewModel>(allEntries)
                : new ObservableCollection<LogEntryViewModel>(allEntries.Where(x => x.Type == filterType));

            EntriesListBox.ItemsSource = filtered;
            CountLabel.Text = $"Entries: {filtered.Count}";
        }
    }

    public class LogEntryViewModel
    {
        public string Time { get; set; }
        public string Milliseconds { get; set; }
        public string Type { get; set; }
        public string RawText { get; set; }
        public ObservableCollection<BitstreamData> Bitstreams { get; } = new();

        public string TimeDisplay => $"{Time} +{Milliseconds}ms";
        public string TypeDisplay => Type switch
        {
            "info" => "ℹ️ Info",
            "warning" => "⚠️ Warning",
            "error" => "❌ Error",
            _ => Type
        };
        public string Preview => RawText.Length > 60 ? RawText.Substring(0, 60) + "..." : RawText;
    }

    public class BitstreamData
    {
        public string Label { get; set; }
        public string Binary { get; set; }

        public string Decimal => Convert.ToUInt64(Binary, 2).ToString();
        public string Hex => "0x" + Convert.ToUInt64(Binary, 2).ToString("X");
        public ObservableCollection<BitIndicator> Bits
        {
            get
            {
                var bits = new ObservableCollection<BitIndicator>();
                foreach (var bit in Binary)
                {
                    bits.Add(new BitIndicator
                    {
                        Value = bit.ToString(),
                        Color = bit == '1' ? new SolidColorBrush(Colors.LimeGreen) : new SolidColorBrush(Colors.Red)
                    });
                }
                return bits;
            }
        }
    }

    public class BitIndicator
    {
        public string Value { get; set; }
        public SolidColorBrush Color { get; set; }
    }
}