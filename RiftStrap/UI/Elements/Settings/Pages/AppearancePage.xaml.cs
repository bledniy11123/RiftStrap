using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using RiftStrap.UI.ViewModels.Settings;

namespace RiftStrap.UI.Elements.Settings.Pages
{

    public partial class AppearancePage
    {
        public AppearancePage()
        {
            DataContext = new AppearanceViewModel(this);
            InitializeComponent();
        }

        private void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            var sb = (Storyboard)FindResource("SectionEntrance");
            sb.Begin(this, true);
        }

        public void CustomThemeSelection(object sender, SelectionChangedEventArgs e)
        {
            AppearanceViewModel viewModel = (AppearanceViewModel)DataContext;

            viewModel.SelectedCustomTheme = (string)((ListBox)sender).SelectedItem;
            viewModel.SelectedCustomThemeName = viewModel.SelectedCustomTheme;

            viewModel.OnPropertyChanged(nameof(viewModel.SelectedCustomTheme));
            viewModel.OnPropertyChanged(nameof(viewModel.SelectedCustomThemeName));
        }
    }
}
