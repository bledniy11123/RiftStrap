using System.Windows.Controls;
using Wpf.Ui.Controls.Interfaces;
using Wpf.Ui.Mvvm.Contracts;

namespace RiftStrap.UI.Elements.About
{

    public partial class MainWindow : INavigationWindow
    {
        public MainWindow()
        {
            InitializeComponent();

            VersionLabel.Text = $"v{App.Version}";
            App.Logger.WriteLine("MainWindow", "Initializing about window");
        }

        private void NavBtn_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is System.Windows.FrameworkElement { Tag: string tag } && int.TryParse(tag, out var index))
            {
                var pages = new[] {
                    typeof(Pages.AboutPage),
                    typeof(Pages.SupportersPage),
                    typeof(Pages.TranslatorsPage),
                    typeof(Pages.LicensesPage),
                };
                if (index >= 0 && index < pages.Length)
                    RootNavigation.Navigate(pages[index]);
            }
        }

        public Frame GetFrame() => RootFrame;

        public INavigation GetNavigation() => RootNavigation;

        public bool Navigate(Type pageType) => RootNavigation.Navigate(pageType);

        public void SetPageService(IPageService pageService) => RootNavigation.PageService = pageService;

        public void ShowWindow() => Show();

        public void CloseWindow() => Close();

    }
}
