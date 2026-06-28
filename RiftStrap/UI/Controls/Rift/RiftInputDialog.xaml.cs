using System.Windows;
using System.Windows.Input;
using RiftStrap.Extensions;

namespace RiftStrap.UI.Controls.Rift
{
    public partial class RiftInputDialog : Window
    {
        public string Result { get; private set; } = "";

        public RiftInputDialog(string title, string prompt, string defaultValue = "", bool multiline = false)
        {
            InitializeComponent();

            try { Icon = Properties.Resources.IconRiftStrap.GetImageSource(); } catch { }

            TitleText.Text = title;
            PromptText.Text = prompt;
            InputBox.Text = defaultValue;

            if (multiline)
            {
                InputBox.AcceptsReturn = true;
                InputBox.TextWrapping = System.Windows.TextWrapping.Wrap;
                InputBox.VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto;
                InputBox.VerticalContentAlignment = System.Windows.VerticalAlignment.Top;
                InputBox.Height = 140;
                Height = 400;   // grow the window to fit the multi-line box
            }

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

        public static string? Show(string title, string prompt, string defaultValue = "", bool multiline = false)
        {
            var dialog = new RiftInputDialog(title, prompt, defaultValue, multiline);

            if (Application.Current.MainWindow is { IsLoaded: true } main)
                dialog.Owner = main;

            return dialog.ShowDialog() == true ? dialog.Result : null;
        }
    }
}
