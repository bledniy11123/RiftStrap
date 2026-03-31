using System.Windows;
using System.Windows.Input;

using RiftStrap.Integrations;
using RiftStrap.UI.ViewModels.ContextMenu;

namespace RiftStrap.UI.Elements.ContextMenu
{

    public partial class ServerHistory
    {
        public ServerHistory(ActivityWatcher watcher)
        {
            var viewModel = new ServerHistoryViewModel(watcher);

            viewModel.RequestCloseEvent += (_, _) => Close();

            DataContext = viewModel;
            InitializeComponent();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();
    }
}
