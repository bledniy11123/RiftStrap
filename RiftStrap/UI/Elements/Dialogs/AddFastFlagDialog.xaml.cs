using Microsoft.Win32;
using System.Windows;
using System.Windows.Media.Animation;
using RiftStrap.Resources;

namespace RiftStrap.UI.Elements.Dialogs
{

    public partial class AddFastFlagDialog
    {
        public MessageBoxResult Result = MessageBoxResult.Cancel;

        public AddFastFlagDialog()
        {
            InitializeComponent();

            Loaded += (_, _) =>
            {
                var sb = (Storyboard)FindResource("Entrance");
                sb.Begin(this, true);
            };
        }

        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = $"{Strings.FileTypes_JSONFiles}|*.json"
            };

            if (dialog.ShowDialog() != true)
                return;

            JsonTextBox.Text = File.ReadAllText(dialog.FileName);
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.OK;
            Close();
        }

        private void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left) DragMove();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();
    }
}
