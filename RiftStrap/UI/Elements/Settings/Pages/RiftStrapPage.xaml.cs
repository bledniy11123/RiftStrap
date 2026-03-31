using System.Windows;
using System.Windows.Media.Animation;
using RiftStrap.UI.ViewModels.Settings;

namespace RiftStrap.UI.Elements.Settings.Pages
{

    public partial class RiftStrapPage
    {
        public RiftStrapPage()
        {
            DataContext = new RiftStrapViewModel();
            InitializeComponent();
        }

        private void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            var sb = (Storyboard)FindResource("SectionEntrance");
            sb.Begin(this, true);
        }
    }
}
