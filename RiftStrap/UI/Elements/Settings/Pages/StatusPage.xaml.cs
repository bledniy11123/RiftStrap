using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Wpf.Ui.Controls;
using RiftStrap.Features.StatusMonitor;

namespace RiftStrap.UI.Elements.Settings.Pages
{
    public partial class StatusPage : UiPage
    {
        private readonly RobloxStatusService _status = new();
        public StatusPage() => InitializeComponent();

        private async void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            var sb = (Storyboard)FindResource("SectionEntrance");
            sb.Begin(this, true);

            var pulse = (Storyboard)FindResource("PulseAnimation");
            pulse.Begin(this, true);

            await CheckStatus();
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e) => await CheckStatus();

        private async Task CheckStatus()
        {
            OverallText.Text = "Checking...";
            OverallDetail.Text = "";
            StatusDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#444444"));
            ServiceList.ItemsSource = null;

            var result = await _status.GetOverallStatusAsync();

            OverallText.Text = result.Status;
            OverallDetail.Text = $"{result.Healthy}/{result.Total} services operational";
            StatusDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(
                result.IsFullyOperational ? "#FAFAFA" : "#666666"));
            ServiceList.ItemsSource = result.Results;
        }
    }
}
