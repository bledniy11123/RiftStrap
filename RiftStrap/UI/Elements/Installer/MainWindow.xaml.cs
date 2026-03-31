using System.Windows.Controls;
using Wpf.Ui.Controls.Interfaces;
using Wpf.Ui.Mvvm.Contracts;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

using RiftStrap.UI.ViewModels.Installer;
using RiftStrap.UI.Elements.Installer.Pages;
using RiftStrap.UI.Elements.Base;
using RiftStrap.Resources;

namespace RiftStrap.UI.Elements.Installer
{
    public partial class MainWindow : WpfUiWindow, INavigationWindow
    {
        public static string InstallerVersion => App.Version;

        internal readonly MainWindowViewModel _viewModel = new();

        private Type _currentPage = typeof(WelcomePage);

        private List<Type> _pages = new() { typeof(WelcomePage), typeof(InstallPage), typeof(CompletionPage) };

        private DateTimeOffset _lastNavigation = DateTimeOffset.Now;

        public Func<bool>? NextPageCallback;

        public NextAction CloseAction = NextAction.Terminate;

        public bool Finished => _currentPage == _pages.Last();

        public MainWindow()
        {
            _viewModel.CloseWindowRequest += (_, _) => CloseWindow();

            _viewModel.PageRequest += (_, type) =>
            {
                if (DateTimeOffset.Now.Subtract(_lastNavigation).TotalMilliseconds < 500)
                    return;

                if (type == "next")
                    NextPage();
                else if (type == "back")
                    BackPage();

                _lastNavigation = DateTimeOffset.Now;
            };

            DataContext = _viewModel;
            InitializeComponent();

            VersionText.Text = $"v{App.Version}";

            App.Logger.WriteLine("MainWindow", "Initializing installer window");

            Closing += new CancelEventHandler(MainWindow_Closing);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {

            PlayContentEntrance();

            var glowStoryboard = (Storyboard)FindResource("LogoGlow");
            glowStoryboard.Begin(this, true);
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {

            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void MinBtn_Click(object sender, RoutedEventArgs e) => this.WindowState = System.Windows.WindowState.Minimized;
        private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();

        void NextPage()
        {
            if (NextPageCallback is not null && !NextPageCallback())
                return;

            if (_currentPage == _pages.Last())
                return;

            var page = _pages[_pages.IndexOf(_currentPage) + 1];

            Navigate(page);

            SetButtonEnabled("next", page != _pages.Last());
            SetButtonEnabled("back", true);
        }

        void BackPage()
        {
            if (_currentPage == _pages.First())
                return;

            var page = _pages[_pages.IndexOf(_currentPage) - 1];

            Navigate(page);

            SetButtonEnabled("next", true);
            SetButtonEnabled("back", page != _pages.First());
        }

        void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            if (Finished)
                return;

            var result = Frontend.ShowMessageBox(Strings.Installer_ShouldCancel, MessageBoxImage.Warning, MessageBoxButton.YesNo);

            if (result != MessageBoxResult.Yes)
                e.Cancel = true;
        }

        public void SetNextButtonText(string text) => _viewModel.SetNextButtonText(text);

        public void SetButtonEnabled(string type, bool state) => _viewModel.SetButtonEnabled(type, state);

        public Frame GetFrame() => RootFrame;

        public INavigation GetNavigation() => RootNavigation;

        public bool Navigate(Type pageType)
        {
            _currentPage = pageType;
            NextPageCallback = null;
            UpdateStepIndicator();
            PlayContentEntrance();
            return RootNavigation.Navigate(pageType);
        }

        public void SetPageService(IPageService pageService) => RootNavigation.PageService = pageService;

        public void ShowWindow() => Show();

        public void CloseWindow() => Close();

        private void PlayContentEntrance()
        {
            var sb = (Storyboard)FindResource("ContentEntrance");
            sb.Begin(this, true);
        }

        private void UpdateStepIndicator()
        {
            var index = _pages.IndexOf(_currentPage);
            var active = (Color)ColorConverter.ConvertFromString("#FAFAFA");
            var inactive = (Color)ColorConverter.ConvertFromString("#222222");
            var dur = TimeSpan.FromMilliseconds(300);
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            AnimateFill(Step1Dot, index >= 0 ? active : inactive, dur, ease);
            AnimateFill(Step2Dot, index >= 1 ? active : inactive, dur, ease);
            AnimateFill(Step3Dot, index >= 2 ? active : inactive, dur, ease);

            AnimateGradientStop(Line1Start, index >= 1 ? active : inactive, dur, ease);
            AnimateGradientStop(Line1End, index >= 1 ? active : inactive, dur, ease);
            AnimateGradientStop(Line2Start, index >= 2 ? active : inactive, dur, ease);
            AnimateGradientStop(Line2End, index >= 2 ? active : inactive, dur, ease);

            var targetDot = index switch { 0 => Step1Dot, 1 => Step2Dot, 2 => Step3Dot, _ => Step1Dot };
            var dotSb = (Storyboard)FindResource("DotActivate");
            Storyboard.SetTarget(dotSb, targetDot);
            dotSb.Begin(this, true);
        }

        private static void AnimateFill(System.Windows.Shapes.Shape shape, Color target, TimeSpan duration, IEasingFunction ease)
        {
            var anim = new ColorAnimation { To = target, Duration = duration, EasingFunction = ease };
            var brush = new SolidColorBrush();
            brush.BeginAnimation(SolidColorBrush.ColorProperty, anim);
            shape.Fill = brush;
        }

        private static void AnimateGradientStop(GradientStop stop, Color target, TimeSpan duration, IEasingFunction ease)
        {
            var anim = new ColorAnimation { To = target, Duration = duration, EasingFunction = ease };
            stop.BeginAnimation(GradientStop.ColorProperty, anim);
        }
    }
}
