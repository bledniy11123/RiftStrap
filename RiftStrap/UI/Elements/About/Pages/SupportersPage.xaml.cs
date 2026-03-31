using System.Windows;

using RiftStrap.UI.ViewModels.About;

namespace RiftStrap.UI.Elements.About.Pages
{

    public partial class SupportersPage
    {
        private readonly SupportersViewModel _viewModel = new();

        public SupportersPage()
        {
            DataContext = _viewModel;
            InitializeComponent();
        }

        private void UiPage_SizeChanged(object sender, SizeChangedEventArgs e)
            => _viewModel.WindowResizeEvent?.Invoke(sender, e);
    }
}
