using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Wpf.Ui.Controls;
using RiftStrap.Features.QuickLaunch;

namespace RiftStrap.UI.Elements.Settings.Pages
{
    public partial class QuickLaunchPage : UiPage
    {
        private readonly QuickLaunchService _service = new();
        private List<SearchResult> _lastResults = new();

        public QuickLaunchPage() => InitializeComponent();

        private async void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            ((Storyboard)FindResource("SectionEntrance")).Begin(this, true);
            FavoritesList.ItemsSource = _service.Favorites;
            RecentList.ItemsSource = _service.RecentGames;

            if (_lastResults.Count == 0 && _service.Favorites.Count == 0)
            {
                _lastResults = await _service.SearchGamesAsync("", 8);
                if (_lastResults.Count > 0)
                {
                    SearchResults.ItemsSource = _lastResults;
                    SearchResults.Visibility = Visibility.Visible;
                }
            }
        }

        private async void Search_Click(object sender, RoutedEventArgs e) => await DoSearch();
        private async void Search_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) await DoSearch(); }

        private async Task DoSearch()
        {
            var query = SearchBox.Text?.Trim();
            if (string.IsNullOrEmpty(query)) return;

            _lastResults = await _service.SearchGamesAsync(query);
            SearchResults.ItemsSource = _lastResults;
            SearchResults.Visibility = _lastResults.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void PlayGame_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { Tag: long placeId }) return;
            var game = _lastResults.FirstOrDefault(r => r.PlaceId == placeId);
            if (game != null)
            {
                _service.TrackPlayed(new SavedGame { PlaceId = game.PlaceId, UniverseId = game.UniverseId, Name = game.Name, CreatorName = game.CreatorName });
                _service.LaunchGame(placeId);
                RecentList.ItemsSource = _service.RecentGames;
            }
        }

        private void FavoriteGame_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { Tag: long placeId }) return;
            var game = _lastResults.FirstOrDefault(r => r.PlaceId == placeId);
            if (game != null)
            {
                _service.AddFavorite(new SavedGame { PlaceId = game.PlaceId, UniverseId = game.UniverseId, Name = game.Name, CreatorName = game.CreatorName, ThumbnailUrl = game.ThumbnailUrl });
                FavoritesList.ItemsSource = null;
                FavoritesList.ItemsSource = _service.Favorites;
            }
        }

        private void FavCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: SavedGame game })
                _service.LaunchGame(game.PlaceId);
        }

        private void RecentCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: SavedGame game })
                _service.LaunchGame(game.PlaceId);
        }
    }
}
