using System.Windows;
using System.Windows.Media.Animation;
using Wpf.Ui.Controls;
using RiftStrap.Features.HardwareOptimizer;
using RiftStrap.Features.HardwareOptimizer.Models;

namespace RiftStrap.UI.Elements.Settings.Pages
{
    public partial class HardwareOptPage : UiPage
    {
        private HardwareInfo? _hwInfo;
        private Dictionary<string, object>? _recommendedFlags;

        public HardwareOptPage()
        {
            InitializeComponent();
        }

        private void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            ((Storyboard)FindResource("SectionEntrance")).Begin(this, true);
            RunDetection();
        }

        private void RunDetection()
        {
            _hwInfo = HardwareDetector.Detect();
            _recommendedFlags = OptimalConfigGenerator.Generate(_hwInfo);

            CpuText.Text = _hwInfo.CpuName;
            CpuCoresText.Text = $"{_hwInfo.CpuCores} cores, {_hwInfo.CpuMaxClockMHz} MHz";

            GpuText.Text = _hwInfo.GpuName;
            GpuVramText.Text = _hwInfo.IsIntegratedGpu
                ? "Integrated"
                : $"{_hwInfo.GpuVramBytes / 1024 / 1024} MB VRAM";

            RamText.Text = $"{_hwInfo.TotalRamGB:F1} GB";
            DisplayText.Text = $"{_hwInfo.MonitorWidth}x{_hwInfo.MonitorHeight} @ {_hwInfo.MonitorRefreshRate}Hz";

            TierText.Text = _hwInfo.Tier.ToString().ToUpper();
            TierDescription.Text = OptimalConfigGenerator.GetDescription(_hwInfo.Tier);
            FlagCountText.Text = $"{_recommendedFlags.Count} FastFlags will be configured";

            FlagsList.ItemsSource = _recommendedFlags.ToList();
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            if (_recommendedFlags == null)
                return;

            var result = Frontend.ShowMessageBox(
                $"Apply {_recommendedFlags.Count} optimized FastFlags for {_hwInfo?.Tier} tier?\n\nThis will override your current FastFlag settings.",
                MessageBoxImage.Question,
                MessageBoxButton.YesNo);

            if (result != MessageBoxResult.Yes)
                return;

            foreach (var (key, value) in _recommendedFlags)
            {
                App.FastFlags.SetValue(key, value);
            }

            App.FastFlags.Save();

            Frontend.ShowMessageBox("Optimization applied! Changes will take effect on next Roblox launch.", MessageBoxImage.Information);
        }

        private void Redetect_Click(object sender, RoutedEventArgs e)
        {
            RunDetection();
        }
    }
}
