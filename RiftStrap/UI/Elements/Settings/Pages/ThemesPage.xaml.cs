using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Microsoft.Win32;
using Wpf.Ui.Controls;
using RiftStrap.Features.InGameUI;
using RiftStrap.Features.InGameUI.Models;

namespace RiftStrap.UI.Elements.Settings.Pages
{
    public partial class ThemesPage : UiPage
    {
        private readonly ThemeEngine _themeEngine = new();
        private ThemeCategory? _activeFilter;

        public ThemesPage()
        {
            InitializeComponent();
        }

        private void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            ((Storyboard)FindResource("SectionEntrance")).Begin(this, true);
            DefaultThemes.EnsureInstalled(_themeEngine);
            RefreshThemes();
            RefreshActiveTheme();
        }

        private void RefreshThemes()
        {
            var themes = _themeEngine.GetInstalledThemes();

            if (_activeFilter.HasValue)
                themes = themes.Where(t => t.Category == _activeFilter.Value).ToList();

            ThemesList.ItemsSource = themes;
            S4.Visibility = themes.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void RefreshActiveTheme()
        {
            var active = _themeEngine.ActiveTheme;
            if (active != null)
            {
                ActiveThemeName.Text = active.Name;
                ActiveThemeAuthor.Text = $"by {active.Author} — v{active.Version}";
                RemoveThemeButton.Visibility = Visibility.Visible;
            }
            else
            {
                ActiveThemeName.Text = "None";
                ActiveThemeAuthor.Text = "No theme applied";
                RemoveThemeButton.Visibility = Visibility.Collapsed;
            }
        }

        private void InstallTheme_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Install Theme",
                Filter = "RiftStrap Theme (*.rifttheme;*.zip)|*.rifttheme;*.zip",
                Multiselect = false
            };

            if (dialog.ShowDialog() != true)
                return;

            var theme = _themeEngine.InstallTheme(dialog.FileName);
            if (theme != null)
            {
                RefreshThemes();
                Frontend.ShowMessageBox($"Theme \"{theme.Name}\" installed successfully!", MessageBoxImage.Information);
            }
            else
            {
                Frontend.ShowMessageBox("Failed to install theme. Make sure it's a valid .rifttheme file.", MessageBoxImage.Warning);
            }
        }

        private void ScanContent_Click(object sender, RoutedEventArgs e)
        {
            var content = ThemeEngine.ScanRobloxContent();
            Frontend.ShowMessageBox($"Found {content.Count} moddable content files in Roblox installation.", MessageBoxImage.Information);
        }

        private void RemoveTheme_Click(object sender, RoutedEventArgs e)
        {
            _themeEngine.RemoveActiveTheme();
            RefreshActiveTheme();
        }

        private void ThemeCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: RiftTheme theme })
            {
                var result = Frontend.ShowMessageBox(
                    $"Apply theme \"{theme.Name}\"?\n\nThis will modify your Roblox content files. Changes are reversible.",
                    MessageBoxImage.Question,
                    MessageBoxButton.YesNo);

                if (result == MessageBoxResult.Yes)
                {
                    if (_themeEngine.ApplyTheme(theme.Id))
                    {
                        RefreshActiveTheme();
                    }
                    else
                    {
                        Frontend.ShowMessageBox("Failed to apply theme.", MessageBoxImage.Warning);
                    }
                }
            }
        }

        private void Filter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { Tag: string tag })
            {
                _activeFilter = tag == "All" ? null : Enum.Parse<ThemeCategory>(tag);
                RefreshThemes();
            }
        }
    }
}
