using System.Media;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;

using Windows.Win32;
using Windows.Win32.Foundation;

namespace RiftStrap.UI.Elements.Dialogs
{

    public partial class ConnectivityDialog
    {
        public ConnectivityDialog(string title, string description, MessageBoxImage image, Exception exception)
        {
            InitializeComponent();

            SystemSound? sound = null;

            switch (image)
            {
                case MessageBoxImage.Error:
                    sound = SystemSounds.Hand;
                    break;

                case MessageBoxImage.Question:
                    sound = SystemSounds.Question;
                    break;

                case MessageBoxImage.Warning:
                    sound = SystemSounds.Exclamation;
                    break;

                case MessageBoxImage.Information:
                    sound = SystemSounds.Asterisk;
                    break;
            }

            TitleTextBlock.Text = title;
            DescriptionTextBlock.MarkdownText = description;

            AddException(exception);

            CloseButton.Click += delegate
            {
                Close();
            };

            sound?.Play();

            Loaded += delegate
            {
                var hWnd = new WindowInteropHelper(this).Handle;
                PInvoke.FlashWindow((HWND)hWnd, true);

                var sb = (Storyboard)FindResource("Entrance");
                sb.Begin(this, true);
            };
        }

        private void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left) DragMove();
        }

        private void CloseBtn_Click(object sender, System.Windows.RoutedEventArgs e) => Close();

        private void AddException(Exception exception, bool inner = false)
        {
            if (!inner)
                ErrorRichTextBox.Selection.Text = $"{exception.GetType()}: {exception.Message}";

            if (exception.InnerException is null)
                return;

            ErrorRichTextBox.Selection.Text += $"\n\n[Inner Exception]\n{exception.InnerException.GetType()}: {exception.InnerException.Message}";

            AddException(exception.InnerException, true);
        }
    }
}
