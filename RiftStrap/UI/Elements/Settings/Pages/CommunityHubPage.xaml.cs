using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Wpf.Ui.Controls;
using RiftStrap.Features.CommunityHub;
using RiftStrap.Features.CommunityHub.Models;
using RiftStrap.Features.InGameUI;

namespace RiftStrap.UI.Elements.Settings.Pages
{
    public partial class CommunityHubPage : UiPage
    {
        private readonly HubService _hub = new();
        private readonly ThemeEngine _engine = new();
        private string _activeCategory = "All";

        public CommunityHubPage() => InitializeComponent();

        private async void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            ((Storyboard)FindResource("SectionEntrance")).Begin(this, true);
            await LoadCatalog();
        }

        private async Task LoadCatalog()
        {
            StatusText.Text = "Loading themes...";
            StatusText.Visibility = Visibility.Visible;

            var catalog = await _hub.FetchCatalogAsync();
            if (catalog == null || catalog.Themes.Count == 0)
            {
                StatusText.Text = "No themes available. Check your connection.";
                return;
            }

            StatusText.Visibility = Visibility.Collapsed;
            RefreshList();
        }

        private void RefreshList()
        {
            var query = SearchBox.Text?.Trim() ?? "";
            var themes = string.IsNullOrEmpty(query)
                ? _hub.FilterByCategory(_activeCategory)
                : _hub.Search(query);

            ThemesList.ItemsSource = themes;
            StatusText.Visibility = themes.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            StatusText.Text = themes.Count == 0 ? "No themes match your search." : "";
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadCatalog();

        private void Search_KeyUp(object sender, KeyEventArgs e) => RefreshList();

        private void Category_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { Tag: string tag })
            {
                _activeCategory = tag;
                RefreshList();
            }
        }

        private async void Install_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { Tag: string id }) return;
            var theme = _hub.Catalog?.Themes.FirstOrDefault(t => t.Id == id);
            if (theme == null) return;

            var btn = sender as RiftStrap.UI.Controls.Rift.RiftButton;
            if (btn != null) { btn.Content = "Installing..."; btn.IsEnabled = false; }

            var success = await _hub.DownloadAndInstallAsync(theme, _engine);

            if (btn != null) { btn.Content = success ? "Installed" : "Failed"; btn.IsEnabled = true; }

            if (success)
                Frontend.ShowMessageBox($"\"{theme.Name}\" installed! Go to Themes to apply it.", MessageBoxImage.Information);
        }
    }
}
