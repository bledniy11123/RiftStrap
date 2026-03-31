using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Wpf.Ui.Controls;
using RiftStrap.Features.ScreenshotManager;

namespace RiftStrap.UI.Elements.Settings.Pages
{
    public partial class ScreenshotsPage : UiPage
    {
        private readonly ScreenshotService _service = new();

        public ScreenshotsPage()
        {
            InitializeComponent();
        }

        private void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            ((Storyboard)FindResource("SectionEntrance")).Begin(this, true);
            RefreshScreenshots();
            RefreshStats();
        }

        private void RefreshScreenshots()
        {
            var screenshots = _service.GetAll();
            ScreenshotsList.ItemsSource = screenshots;
            EmptyState.Visibility = screenshots.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            ScreenshotsList.Visibility = screenshots.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void RefreshStats()
        {
            var (count, sizeBytes) = _service.GetStats();
            StatsCount.Text = $"{count} screenshot{(count != 1 ? "s" : "")}";

            if (sizeBytes > 1024 * 1024)
                StatsDiskUsage.Text = $"{sizeBytes / 1024.0 / 1024:F1} MB on disk";
            else
                StatsDiskUsage.Text = $"{sizeBytes / 1024.0:F0} KB on disk";
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            ScreenshotService.OpenFolder();
        }

        private void CleanOld_Click(object sender, RoutedEventArgs e)
        {
            var result = Frontend.ShowMessageBox(
                "Delete screenshots older than 30 days?",
                MessageBoxImage.Question,
                MessageBoxButton.YesNo);

            if (result == MessageBoxResult.Yes)
            {
                int deleted = _service.CleanOlderThan(30);
                RefreshScreenshots();
                RefreshStats();
                Frontend.ShowMessageBox($"Deleted {deleted} old screenshot{(deleted != 1 ? "s" : "")}.", MessageBoxImage.Information);
            }
        }

        private void ScreenshotItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: ScreenshotInfo info })
            {
                ScreenshotService.OpenScreenshot(info.Path);
            }
        }
    }
}
