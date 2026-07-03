using System.Windows;
using System.Windows.Media.Animation;
using Wpf.Ui.Controls;
using RiftStrap.Features.GameProfiles;
using RiftStrap.Features.GameProfiles.Models;

namespace RiftStrap.UI.Elements.Settings.Pages
{
    public partial class GameProfilesPage : UiPage
    {
        private readonly GameProfileManager _manager = new();

        public GameProfilesPage()
        {
            InitializeComponent();
        }

        private void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            ((Storyboard)FindResource("SectionEntrance")).Begin(this, true);
            AutoDetectToggle.IsChecked = _manager.AutoDetectEnabled;
            RefreshProfiles();
        }

        private void RefreshProfiles()
        {
            var profiles = _manager.Profiles.Values
                .OrderByDescending(p => p.LastPlayed ?? p.CreatedAt)
                .ToList();

            ProfilesList.ItemsSource = profiles;
            S4.Visibility = profiles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void AddProfile_Click(object sender, RoutedEventArgs e)
        {

            var inputText = UI.Controls.Rift.RiftInputDialog.Show(
                "Add Game Profile",
                "Enter the Roblox Place ID for the game:");

            if (string.IsNullOrEmpty(inputText) || !long.TryParse(inputText, out var placeId))
                return;

            if (_manager.GetProfile(placeId) != null)
            {
                Frontend.ShowMessageBox("A profile for this Place ID already exists.", MessageBoxImage.Warning);
                return;
            }

            var gameName = UI.Controls.Rift.RiftInputDialog.Show(
                "Game Name",
                "Enter a name for this game:",
                $"Game {placeId}");

            var profile = new GameProfile
            {
                PlaceId = placeId,
                Name = string.IsNullOrEmpty(gameName) ? $"Game {placeId}" : gameName,
            };

            _manager.SetProfile(profile);
            RefreshProfiles();
        }

        private void DeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { Tag: long placeId })
            {
                _manager.RemoveProfile(placeId);
                RefreshProfiles();
            }
        }

        private void AutoDetect_Changed(object sender, RoutedEventArgs e)
        {
            _manager.AutoDetectEnabled = AutoDetectToggle.IsChecked == true;
        }

        private void ProfileEnabled_Changed(object sender, RoutedEventArgs e)
        {
            // Persist the per-profile Enabled flip. The two-way binding only mutates the
            // in-memory GameProfile; without this the change is never written to
            // GameProfiles.json and is lost on the next launch (the watcher process reloads
            // from disk and re-applies the profile).
            if (sender is FrameworkElement { DataContext: GameProfile profile })
                _manager.SetProfile(profile);
        }
    }
}
