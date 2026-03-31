using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using RiftStrap.UI.ViewModels.Dialogs;
using RiftStrap.UI.ViewModels.Installer;

namespace RiftStrap.UI.Elements.Dialogs
{
    public partial class LaunchMenuDialog
    {
        public NextAction CloseAction = NextAction.Terminate;

        public LaunchMenuDialog()
        {
            var viewModel = new LaunchMenuViewModel();
            viewModel.CloseWindowRequest += (_, closeAction) =>
            {
                CloseAction = closeAction;
                Close();
            };

            DataContext = viewModel;
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {

            var entrance = (Storyboard)FindResource("EntranceAnim");
            entrance.Begin(this, true);

            var pulse = (Storyboard)FindResource("LogoPulse");
            pulse.Begin(this, true);
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();
    }
}
