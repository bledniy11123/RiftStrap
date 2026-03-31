using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

using RiftStrap.UI.ViewModels.Settings;

namespace RiftStrap.UI.Elements.Settings.Pages
{

    public partial class IntegrationsPage
    {
        public IntegrationsPage()
        {
            DataContext = new IntegrationsViewModel();
            InitializeComponent();
        }

        private void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            var sb = (Storyboard)FindResource("SectionEntrance");
            sb.Begin(this, true);
        }

        public void CustomIntegrationSelection(object sender, SelectionChangedEventArgs e)
        {
            IntegrationsViewModel viewModel = (IntegrationsViewModel)DataContext;
            viewModel.SelectedCustomIntegration = (CustomIntegration)((ListBox)sender).SelectedItem;
            viewModel.OnPropertyChanged(nameof(viewModel.SelectedCustomIntegration));
        }
    }
}
