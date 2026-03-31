using System.Windows;
using System.Windows.Media.Animation;
using Wpf.Ui.Controls;
using RiftStrap.Features.Plugins;

namespace RiftStrap.UI.Elements.Settings.Pages
{
    public partial class PluginsPage : UiPage
    {
        private readonly PluginHost _host = new();
        public PluginsPage() => InitializeComponent();

        private async void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            var sb = (Storyboard)FindResource("SectionEntrance");
            sb.Begin(this, true);

            await _host.LoadAllAsync();
            RefreshList();
        }

        private void RefreshList()
        {
            PluginsList.ItemsSource = _host.Plugins;
            EmptyState.Visibility = _host.Plugins.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
            => Utilities.ShellExecute(Path.Combine(Paths.Base, "Plugins"));

        private async void Unload_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { Tag: string id })
            {
                await _host.UnloadPluginAsync(id);
                RefreshList();
            }
        }
    }
}
