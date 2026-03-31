using System.Windows;
using System.Windows.Media.Animation;

using RiftStrap.Models.Persistable;
using RiftStrap.UI.ViewModels.Settings;

namespace RiftStrap.UI.Elements.Settings.Pages
{

    public partial class BehaviourPage
    {
        public BehaviourPage()
        {
            DataContext = new BehaviourViewModel();
            InitializeComponent();
        }

        private void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            var sb = (Storyboard)FindResource("SectionEntrance");
            sb.Begin(this, true);

            if (DataContext is BehaviourViewModel vm)
                vm.LoadVersionHistory();
        }

        private void PinVersion_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: VersionHistoryEntry entry }
                && DataContext is BehaviourViewModel vm)
            {
                vm.PinVersion(entry);
            }
        }

        private void ToggleSwitch_Checked(object sender, RoutedEventArgs e)
        {

        }
    }
}
