using System.Windows;
using System.Windows.Input;
using RiftStrap.Extensions;

namespace RiftStrap.UI.Controls.Rift
{
    public partial class RiftInputDialog : Window
    {
        public string Result { get; private set; } = "";

        public RiftInputDialog(string title, string prompt, string defaultValue = "")
        {
            InitializeComponent();

            try { Icon = Properties.Resources.IconRiftStrap.GetImageSource(); } catch { }

            TitleText.Text = title;
            PromptText.Text = prompt;
            InputBox.Text = defaultValue;

            MouseLeftButtonDown += (_, _) => DragMove();
            Loaded += (_, _) =>
            {
                InputBox.Focus();
                InputBox.SelectAll();
            };

            InputBox.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    DialogResult = false;
                    Close();
                }
            };
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            Result = InputBox.Text;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        public static string? Show(string title, string prompt, string defaultValue = "")
        {
            var dialog = new RiftInputDialog(title, prompt, defaultValue);

            if (Application.Current.MainWindow is { IsLoaded: true } main)
                dialog.Owner = main;

            return dialog.ShowDialog() == true ? dialog.Result : null;
        }
    }
}
