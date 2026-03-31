using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Wpf.Ui.Controls;
using RiftStrap.Features.Analytics;
using RiftStrap.Features.HardwareOptimizer;

namespace RiftStrap.UI.Elements.Settings.Pages
{
    public partial class DashboardPage : UiPage
    {
        private readonly AnalyticsService _analytics = new();

        public DashboardPage()
        {
            InitializeComponent();
            LoadData();
        }

        private void Page_Loaded_Anim(object sender, RoutedEventArgs e)
        {
            var sb = (Storyboard)FindResource("SectionEntrance");
            sb.Begin(this, true);
        }

        private void LoadData()
        {

            var hour = DateTime.Now.Hour;
            var isRu = Locale.CurrentCulture?.Name?.StartsWith("ru") == true;

            GreetingText.Text = hour switch
            {
                < 6 => isRu ? "Доброй ночи" : "Good night",
                < 12 => isRu ? "Доброе утро" : "Good morning",
                < 17 => isRu ? "Добрый день" : "Good afternoon",
                < 21 => isRu ? "Добрый вечер" : "Good evening",
                _ => isRu ? "Доброй ночи" : "Good night"
            };

            RiftStrapVersionText.Text = $"v{App.Version}";

            try
            {
                var robloxVersion = Utilities.GetRobloxVersionStr(false);
                RobloxVersionText.Text = string.IsNullOrEmpty(robloxVersion) ? (isRu ? "Не установлен" : "Not installed") : robloxVersion;
            }
            catch { RobloxVersionText.Text = isRu ? "Не установлен" : "Not installed"; }

            FastFlagCountText.Text = App.FastFlags.Prop.Count.ToString();

            try
            {
                var modsPath = Path.Combine(Paths.Modifications, "content");
                if (Directory.Exists(modsPath))
                    ModCountText.Text = Directory.GetFiles(modsPath, "*", SearchOption.AllDirectories).Length.ToString();
            }
            catch { }

            _ = Task.Run(() =>
            {
                try
                {
                    var hw = HardwareDetector.Detect();
                    Dispatcher.Invoke(() =>
                    {
                        HardwareTierText.Text = hw.Tier.ToString().ToUpper();
                        HardwareTierDesc.Text = $"{hw.CpuCores} cores · {hw.GpuName.Split(' ')[0]} · {hw.TotalRamGB:F0}GB";
                        HardwareTierText.Foreground = new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Color.FromRgb(0xFA, 0xFA, 0xFA));
                    });
                }
                catch { }
            });

            var todayPlay = _analytics.TodayPlayTime;
            if (todayPlay.TotalMinutes > 0)
                SessionText.Text = $"Today: {todayPlay.TotalHours:F1}h played";
        }

        private void LaunchRoblox_Click(object sender, RoutedEventArgs e) => LaunchHandler.LaunchRoblox(LaunchMode.Player);
        private void LaunchRoblox_Click(object sender, MouseButtonEventArgs e) => LaunchHandler.LaunchRoblox(LaunchMode.Player);

        private void OpenFastFlags_Click(object sender, RoutedEventArgs e) => Nav(typeof(FastFlagsPage));
        private void OpenFastFlags_Click(object sender, MouseButtonEventArgs e) => Nav(typeof(FastFlagsPage));

        private void OpenThemes_Click(object sender, RoutedEventArgs e) => Nav(typeof(ThemesPage));
        private void OpenThemes_Click(object sender, MouseButtonEventArgs e) => Nav(typeof(ThemesPage));

        private void LaunchOverlay_Click(object sender, RoutedEventArgs e) => new UI.Windows.PerformanceOverlay().Show();
        private void LaunchOverlay_Click(object sender, MouseButtonEventArgs e) => new UI.Windows.PerformanceOverlay().Show();

        private void Nav(Type page)
        {
            if (Window.GetWindow(this) is UI.Elements.Settings.MainWindow mw) mw.Navigate(page);
        }
    }
}
