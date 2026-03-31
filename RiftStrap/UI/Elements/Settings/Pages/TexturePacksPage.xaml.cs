using System.Windows;
using System.Windows.Media.Animation;
using Microsoft.Win32;
using Wpf.Ui.Controls;
using RiftStrap.Features.TexturePacks;

namespace RiftStrap.UI.Elements.Settings.Pages
{
    public partial class TexturePacksPage : UiPage
    {
        private readonly TexturePackManager _manager = new();

        public TexturePacksPage()
        {
            InitializeComponent();
        }

        private void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            ((Storyboard)FindResource("SectionEntrance")).Begin(this, true);
            RefreshPacks();
            RefreshActive();
        }

        private void RefreshPacks()
        {
            var packs = _manager.GetInstalledPacks();
            PacksList.ItemsSource = packs;
            EmptyState.Visibility = packs.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            PacksList.Visibility = packs.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void RefreshActive()
        {
            var active = _manager.ActivePack;
            if (active != null)
            {
                ActivePackName.Text = active.Name;
                ActivePackInfo.Text = $"by {active.Author} — {active.FileCount} files";
                RemoveActiveButton.Visibility = Visibility.Visible;
            }
            else
            {
                ActivePackName.Text = "None";
                ActivePackInfo.Text = "No texture pack applied";
                RemoveActiveButton.Visibility = Visibility.Collapsed;
            }
        }

        private void InstallZip_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Install Texture Pack",
                Filter = "Texture Pack (*.zip;*.rifttheme)|*.zip;*.rifttheme",
                Multiselect = false
            };

            if (dialog.ShowDialog() != true)
                return;

            var pack = _manager.InstallFromZip(dialog.FileName);
            if (pack != null)
            {
                RefreshPacks();
                Frontend.ShowMessageBox($"Texture pack \"{pack.Name}\" installed successfully!", MessageBoxImage.Information);
            }
            else
            {
                Frontend.ShowMessageBox("Failed to install texture pack. Check the file is valid.", MessageBoxImage.Warning);
            }
        }

        private void InstallFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select texture pack folder",
                ShowNewFolderButton = false
            };

            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            var pack = _manager.InstallFromFolder(dialog.SelectedPath);
            if (pack != null)
            {
                RefreshPacks();
                Frontend.ShowMessageBox($"Texture pack \"{pack.Name}\" installed successfully!", MessageBoxImage.Information);
            }
            else
            {
                Frontend.ShowMessageBox("Failed to install from folder.", MessageBoxImage.Warning);
            }
        }

        private void ApplyPack_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { Tag: string packId })
            {
                if (_manager.Apply(packId))
                {
                    RefreshActive();
                    RefreshPacks();
                }
                else
                {
                    Frontend.ShowMessageBox("Failed to apply texture pack.", MessageBoxImage.Warning);
                }
            }
        }

        private void RemoveActive_Click(object sender, RoutedEventArgs e)
        {
            _manager.RemoveActive();
            RefreshActive();
        }

        private void UninstallPack_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { Tag: string packId })
            {
                var result = Frontend.ShowMessageBox(
                    "Uninstall this texture pack? This cannot be undone.",
                    MessageBoxImage.Question,
                    MessageBoxButton.YesNo);

                if (result == MessageBoxResult.Yes)
                {
                    _manager.Uninstall(packId);
                    RefreshPacks();
                    RefreshActive();
                }
            }
        }
    }
}
