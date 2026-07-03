using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Wpf.Ui.Controls;
using RiftStrap.Features.ServerBrowser;
using RiftStrap.Features.ServerBrowser.Models;

namespace RiftStrap.UI.Elements.Settings.Pages
{
    public partial class ServerBrowserPage : UiPage
    {
        private readonly ServerBrowserService _service = new();
        private long _currentPlaceId;
        private string? _nextCursor;
        private string? _prevCursor;

        public ServerBrowserPage()
        {
            InitializeComponent();
        }

        private void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            ((Storyboard)FindResource("SectionEntrance")).Begin(this, true);
        }

        private async void Search_Click(object sender, RoutedEventArgs e)
        {
            if (!long.TryParse(PlaceIdInput.Text.Trim(), out var placeId))
            {
                Frontend.ShowMessageBox("Please enter a valid Place ID (numbers only).", MessageBoxImage.Warning);
                return;
            }

            _currentPlaceId = placeId;
            _nextCursor = null;
            _prevCursor = null;

            await LoadServersAsync();
        }

        private async Task LoadServersAsync(string? cursor = null)
        {
            SearchButton.IsEnabled = false;
            LoadingText.Text = "Loading servers...";
            ServerList.ItemsSource = null;

            var result = await _service.GetServersAsync(_currentPlaceId, 25, cursor);

            if (result == null)
            {
                LoadingText.Text = "Failed to load servers. Check the Place ID.";
                SearchButton.IsEnabled = true;
                return;
            }

            var servers = result.Data ?? new();   // a malformed response can leave Data null -> NRE
            ServerList.ItemsSource = servers;
            LoadingText.Text = servers.Count == 0 ? "No servers found for this game." : "";

            _nextCursor = result.NextPageCursor;
            _prevCursor = result.PreviousPageCursor;

            PaginationPanel.Visibility = (_nextCursor != null || _prevCursor != null) ? Visibility.Visible : Visibility.Collapsed;
            NextButton.IsEnabled = _nextCursor != null;
            PrevButton.IsEnabled = _prevCursor != null;

            GameInfoCard.Visibility = Visibility.Collapsed;
            try
            {

                var universeResponse = await App.HttpClient.GetStringAsync($"https://apis.roblox.com/universes/v1/places/{_currentPlaceId}/universe");
                var universeData = JsonSerializer.Deserialize<JsonElement>(universeResponse);
                if (universeData.TryGetProperty("universeId", out var uid))
                {
                    var details = await _service.GetGameDetailsAsync(uid.GetInt64());
                    if (details != null)
                    {
                        GameNameText.Text = details.Name;
                        GamePlayingText.Text = $"{details.Playing:N0} playing";
                        GameVisitsText.Text = $"{details.Visits:N0} visits";
                        GameInfoCard.Visibility = Visibility.Visible;
                    }
                }
            }
            catch
            {

            }

            SearchButton.IsEnabled = true;
        }

        private async void NextPage_Click(object sender, RoutedEventArgs e)
        {
            if (_nextCursor != null)
                await LoadServersAsync(_nextCursor);
        }

        private async void PrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (_prevCursor != null)
                await LoadServersAsync(_prevCursor);
        }

        private void JoinServer_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { Tag: string serverId })
            {
                var url = ServerBrowserService.BuildJoinUrl(_currentPlaceId, serverId);
                Utilities.ShellExecute(url);
            }
        }

        private void ServerCard_Click(object sender, MouseButtonEventArgs e)
        {
            // The row shows a Hand cursor implying it's clickable; join the row's server,
            // matching the row-click-to-launch behaviour on the QuickLaunch/Screenshots pages.
            if (sender is FrameworkElement { DataContext: ServerInfo server } && !string.IsNullOrEmpty(server.Id))
            {
                var url = ServerBrowserService.BuildJoinUrl(_currentPlaceId, server.Id);
                Utilities.ShellExecute(url);
            }
        }
    }
}
