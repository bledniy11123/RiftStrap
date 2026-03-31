using System.Windows;
using System.Windows.Media.Animation;
using Microsoft.Win32;
using Wpf.Ui.Controls;
using RiftStrap.Features.ThemeCreator;
using RiftStrap.Features.InGameUI.Models;

namespace RiftStrap.UI.Elements.Settings.Pages
{
    public partial class ThemeCreatorPage : UiPage
    {
        private readonly ThemeCreatorService _creator = new();
        private Dictionary<string, List<ContentFile>>? _scannedContent;
        private string _activeCategory = "Cursors";

        public ThemeCreatorPage() => InitializeComponent();

        private void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            var sb = (Storyboard)FindResource("SectionEntrance");
            sb.Begin(this, true);

            _creator.NewTheme("", "", ThemeCategory.Full);
        }

        private void Category_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { Tag: string cat }) return;
            _activeCategory = cat;

            _scannedContent ??= _creator.ScanContent();

            if (_scannedContent.TryGetValue(cat, out var files))
            {
                FilesList.ItemsSource = files;
                ScanStatus.Visibility = files.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
                ScanStatus.Text = "No files found in this category.";
            }
            else
            {
                FilesList.ItemsSource = null;
                ScanStatus.Visibility = Visibility.Visible;
                ScanStatus.Text = "No files found. Is Roblox installed?";
            }
        }

        private void ReplaceFile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { Tag: string contentPath }) return;

            var ext = Path.GetExtension(contentPath).ToLowerInvariant();
            var filter = ext switch
            {
                ".png" or ".jpg" => "Images (*.png;*.jpg)|*.png;*.jpg",
                ".ogg" or ".mp3" => "Audio (*.ogg;*.mp3)|*.ogg;*.mp3",
                ".ttf" or ".otf" => "Fonts (*.ttf;*.otf)|*.ttf;*.otf",
                _ => "All Files (*.*)|*.*"
            };

            var dialog = new OpenFileDialog { Title = $"Replace {Path.GetFileName(contentPath)}", Filter = filter };
            if (dialog.ShowDialog() != true) return;

            _creator.ReplaceFile(contentPath, dialog.FileName);
            ReplacementCount.Text = $"{_creator.Replacements.Count} files";

            _scannedContent = _creator.ScanContent();
            if (_scannedContent.TryGetValue(_activeCategory, out var files))
                FilesList.ItemsSource = files;
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            var name = ThemeNameBox.Text?.Trim();
            var author = AuthorBox.Text?.Trim();

            if (string.IsNullOrEmpty(name))
            {
                Frontend.ShowMessageBox("Enter a theme name.", MessageBoxImage.Warning);
                return;
            }

            if (_creator.Replacements.Count == 0)
            {
                Frontend.ShowMessageBox("Replace at least one file before exporting.", MessageBoxImage.Warning);
                return;
            }

            _creator.WorkingTheme.Name = name;
            _creator.WorkingTheme.Author = string.IsNullOrEmpty(author) ? "Unknown" : author;

            var dialog = new System.Windows.Forms.FolderBrowserDialog { Description = "Export theme to..." };
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            var path = _creator.Export(dialog.SelectedPath);
            if (path != null)
                Frontend.ShowMessageBox($"Exported to:\n{path}", MessageBoxImage.Information);
            else
                Frontend.ShowMessageBox("Export failed.", MessageBoxImage.Warning);
        }
    }
}
