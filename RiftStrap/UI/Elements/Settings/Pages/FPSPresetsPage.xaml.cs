using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Wpf.Ui.Controls;
using RiftStrap.Features.FPSUnlocker;

namespace RiftStrap.UI.Elements.Settings.Pages
{
    public partial class FPSPresetsPage : UiPage
    {
        public FPSPresetsPage()
        {
            InitializeComponent();
        }

        private void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            ((Storyboard)FindResource("SectionEntrance")).Begin(this, true);
            RefreshDisplay();
        }

        private void RefreshDisplay()
        {
            var current = SmartFPSService.GetCurrentTarget();
            var preset = SmartFPSService.GetCurrentPreset();
            var hz = SmartFPSService.GetMonitorRefreshRate();

            CurrentFpsNumber.Text = current == 9999 ? "\u221e" : current.ToString();
            CurrentDescription.Text = SmartFPSService.GetDescription();

            MonitorHz.Text = $"{hz} Hz";
            QualityHzLabel.Text = $"{hz} FPS";

            var activeBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#12FFFFFF"));
            var activeBorder = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18FFFFFF"));
            var inactiveBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#06FFFFFF"));

            PresetPerformance.Background = preset == SmartFPSService.FPSPreset.Performance ? activeBg : inactiveBg;
            PresetPerformance.BorderBrush = preset == SmartFPSService.FPSPreset.Performance ? activeBorder : Brushes.Transparent;
            PresetPerformance.BorderThickness = new Thickness(preset == SmartFPSService.FPSPreset.Performance ? 1 : 0);

            PresetBalanced.Background = preset == SmartFPSService.FPSPreset.Balanced ? activeBg : inactiveBg;
            PresetBalanced.BorderBrush = preset == SmartFPSService.FPSPreset.Balanced ? activeBorder : Brushes.Transparent;
            PresetBalanced.BorderThickness = new Thickness(preset == SmartFPSService.FPSPreset.Balanced ? 1 : 0);

            PresetQuality.Background = preset == SmartFPSService.FPSPreset.Quality ? activeBg : inactiveBg;
            PresetQuality.BorderBrush = preset == SmartFPSService.FPSPreset.Quality ? activeBorder : Brushes.Transparent;
            PresetQuality.BorderThickness = new Thickness(preset == SmartFPSService.FPSPreset.Quality ? 1 : 0);

            PresetUnlimited.Background = preset == SmartFPSService.FPSPreset.Unlimited ? activeBg : inactiveBg;
            PresetUnlimited.BorderBrush = preset == SmartFPSService.FPSPreset.Unlimited ? activeBorder : Brushes.Transparent;
            PresetUnlimited.BorderThickness = new Thickness(preset == SmartFPSService.FPSPreset.Unlimited ? 1 : 0);
        }

        private void Preset_Performance(object sender, MouseButtonEventArgs e)
        {
            SmartFPSService.ApplyPreset(SmartFPSService.FPSPreset.Performance);
            RefreshDisplay();
        }

        private void Preset_Balanced(object sender, MouseButtonEventArgs e)
        {
            SmartFPSService.ApplyPreset(SmartFPSService.FPSPreset.Balanced);
            RefreshDisplay();
        }

        private void Preset_Quality(object sender, MouseButtonEventArgs e)
        {
            SmartFPSService.ApplyPreset(SmartFPSService.FPSPreset.Quality);
            RefreshDisplay();
        }

        private void Preset_Unlimited(object sender, MouseButtonEventArgs e)
        {
            SmartFPSService.ApplyPreset(SmartFPSService.FPSPreset.Unlimited);
            RefreshDisplay();
        }

        private void ApplyCustom_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(CustomFpsInput.Text.Trim(), out int fps) && fps >= 15)
            {
                SmartFPSService.ApplyPreset(SmartFPSService.FPSPreset.Custom, fps);
                RefreshDisplay();
            }
            else
            {
                Frontend.ShowMessageBox("Enter a valid FPS value (minimum 15).", MessageBoxImage.Warning);
            }
        }
    }
}
