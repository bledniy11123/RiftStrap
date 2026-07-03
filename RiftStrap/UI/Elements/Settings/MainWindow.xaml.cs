using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

using Wpf.Ui.Controls.Interfaces;
using Wpf.Ui.Mvvm.Contracts;

using RiftStrap.UI.ViewModels.Settings;

namespace RiftStrap.UI.Elements.Settings
{

    public partial class MainWindow : INavigationWindow
    {
        private Models.Persistable.WindowState _state => App.State.Prop.SettingsWindow;

        public MainWindow(bool showAlreadyRunningWarning)
        {
            var viewModel = new MainWindowViewModel();

            viewModel.RequestSaveNoticeEvent += (_, _) => SettingsSavedSnackbar.Show();
            viewModel.RequestCloseWindowEvent += (_, _) => Close();

            DataContext = viewModel;

            InitializeComponent();

            SidebarVersion.Text = $"v{App.Version}";

            App.Logger.WriteLine("MainWindow", "Initializing settings window");

            if (showAlreadyRunningWarning)
                ShowAlreadyRunningSnackbar();

            LoadState();

            UpdateActiveNav("dashboard");
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {

            var entrance = (Storyboard)FindResource("WindowEntrance");
            entrance.Begin(this, true);

            var glow = (Storyboard)FindResource("LogoGlow");
            glow.Begin(this, true);
        }

        public void LoadState()
        {
            if (_state.Left > SystemParameters.VirtualScreenWidth)
                _state.Left = 0;

            if (_state.Top > SystemParameters.VirtualScreenHeight)
                _state.Top = 0;

            if (_state.Width > 0)
                this.Width = _state.Width;

            if (_state.Height > 0)
                this.Height = _state.Height;

            if (_state.Left > 0 && _state.Top > 0)
            {
                this.WindowStartupLocation = WindowStartupLocation.Manual;
                this.Left = _state.Left;
                this.Top = _state.Top;
            }
        }

        private async void ShowAlreadyRunningSnackbar()
        {
            await Task.Delay(500);
            AlreadyRunningSnackbar.Show();
        }

        public Frame GetFrame() => RootFrame;

        public INavigation GetNavigation() => RootNavigation;

        private string? _activeNavTag;

        public bool Navigate(Type pageType)
        {

            PlayPageTransition();

            var tag = NavTagToPage.FirstOrDefault(x => x.Value == pageType).Key;
            UpdateActiveNav(tag);

            return RootNavigation.Navigate(pageType);
        }

        private void PlayPageTransition()
        {
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            var slideIn = new DoubleAnimation(12, 0, TimeSpan.FromMilliseconds(250))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            ContentWrapper.BeginAnimation(OpacityProperty, fadeIn);
            ContentTranslate.BeginAnimation(TranslateTransform.YProperty, slideIn);
        }

        private void UpdateActiveNav(string? tag)
        {
            if (tag == null) return;
            _activeNavTag = tag;

            var sidebar = FindName("NavDashboard")?.GetType();
            var activeColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0FFFFFFF"));
            var activeFg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FAFAFA"));
            var inactiveFg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#888888"));

            foreach (var navTag in NavTagToPage.Keys)
            {
                var btn = FindName($"Nav{char.ToUpper(navTag[0])}{navTag[1..]}") as Button;
                if (btn == null)
                {

                    var names = new[] { "NavDashboard", "NavThemes", "NavProfiles", "NavOptimizer", "NavServers",
                        "NavHub", "NavCreator", "NavAccounts", "NavQuickLaunch", "NavAnalytics", "NavStatus",
                        "NavNetwork", "NavPlugins", "NavTexturePacks", "NavScreenshots", "NavFpsPresets",
                        "NavFastFlags", "NavMods", "NavIntegrations",
                        "NavBehaviour", "NavAppearance", "NavShortcuts" };
                    foreach (var name in names)
                    {
                        var b = FindName(name) as Button;
                        if (b?.Tag?.ToString() == navTag)
                        {
                            btn = b;
                            break;
                        }
                    }
                }

                if (btn != null)
                {
                    btn.Foreground = navTag == tag ? activeFg : inactiveFg;
                    btn.FontWeight = navTag == tag ? FontWeights.SemiBold : FontWeights.Normal;
                }
            }
        }

        public void SetPageService(IPageService pageService) => RootNavigation.PageService = pageService;

        public void ShowWindow() => Show();

        public void CloseWindow() => Close();

        private void WpfUiWindow_Closing(object sender, CancelEventArgs e)
        {
            if (App.FastFlags.Changed || App.PendingSettingTasks.Any())
            {
                var result = Frontend.ShowMessageBox(Strings.Menu_UnsavedChanges, MessageBoxImage.Warning, MessageBoxButton.YesNo);

                if (result != MessageBoxResult.Yes)
                    e.Cancel = true;
            }

            _state.Width = this.Width;
            _state.Height = this.Height;

            _state.Top = this.Top;
            _state.Left = this.Left;

            App.State.Save();

            // Persist standard settings toggles on close too. The Save button is the only
            // other path that calls App.Settings.Save(), so flipping a normal switch (e.g.
            // Multi-Instance) and closing via the X used to silently discard it.
            if (!e.Cancel)
                App.Settings.Save();
        }

        private void WpfUiWindow_Closed(object sender, EventArgs e)
        {
            if (App.LaunchSettings.TestModeFlag.Active)
                LaunchHandler.LaunchRoblox(LaunchMode.Player);
            else
                App.SoftTerminate();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
                DragMove();
        }

        private void MinBtn_Click(object sender, RoutedEventArgs e) => this.WindowState = System.Windows.WindowState.Minimized;
        private void MaxBtn_Click(object sender, RoutedEventArgs e) => this.WindowState = this.WindowState == System.Windows.WindowState.Maximized ? System.Windows.WindowState.Normal : System.Windows.WindowState.Maximized;
        private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();

        private static readonly Dictionary<string, Type> NavTagToPage = new()
        {
            ["dashboard"] = typeof(Pages.DashboardPage),
            ["themes"] = typeof(Pages.ThemesPage),
            ["profiles"] = typeof(Pages.GameProfilesPage),
            ["optimizer"] = typeof(Pages.HardwareOptPage),
            ["servers"] = typeof(Pages.ServerBrowserPage),
            ["hub"] = typeof(Pages.CommunityHubPage),
            ["creator"] = typeof(Pages.ThemeCreatorPage),
            ["accounts"] = typeof(Pages.AccountsPage),
            ["quicklaunch"] = typeof(Pages.QuickLaunchPage),
            ["analytics"] = typeof(Pages.AnalyticsPage),
            ["status"] = typeof(Pages.StatusPage),
            ["network"] = typeof(Pages.NetworkPage),
            ["plugins"] = typeof(Pages.PluginsPage),
            ["fastflags"] = typeof(Pages.FastFlagsPage),
            ["mods"] = typeof(Pages.ModsPage),
            ["integrations"] = typeof(Pages.IntegrationsPage),
            ["bootstrapper"] = typeof(Pages.BehaviourPage),
            ["appearance"] = typeof(Pages.AppearancePage),
            ["shortcuts"] = typeof(Pages.ShortcutsPage),
            ["texturepacks"] = typeof(Pages.TexturePacksPage),
            ["screenshots"] = typeof(Pages.ScreenshotsPage),
            ["fpspresets"] = typeof(Pages.FPSPresetsPage),
            ["settings"] = typeof(Pages.RiftStrapPage),
        };

        private void NavItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { Tag: string tag } && NavTagToPage.TryGetValue(tag, out var pageType))
            {
                Navigate(pageType);
            }
        }

        private void OpenAbout_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm)
                vm.OpenAboutCommand.Execute(null);
        }
    }
}
