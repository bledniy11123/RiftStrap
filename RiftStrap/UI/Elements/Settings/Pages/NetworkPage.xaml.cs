using System.Windows;
using System.Windows.Media.Animation;
using Wpf.Ui.Controls;
using RiftStrap.Features.NetworkOptimizer;

namespace RiftStrap.UI.Elements.Settings.Pages
{
    public partial class NetworkPage : UiPage
    {
        private readonly NetworkOptService _net = new();
        public NetworkPage() => InitializeComponent();

        private void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            var sb = (Storyboard)FindResource("SectionEntrance");
            sb.Begin(this, true);
        }

        private async void TestPing_Click(object sender, RoutedEventArgs e)
        {
            PingStatus.Text = "Testing...";
            var result = await _net.PingRobloxAsync();
            if (result.AvgPing > 0)
                PingStatus.Text = $"Average ping: {result.AvgPing} ms ({result.Endpoints.Count} endpoints)";
            else
                PingStatus.Text = "Could not reach Roblox servers";
        }

        private async void Benchmark_Click(object sender, RoutedEventArgs e)
        {
            DnsResults.ItemsSource = null;
            var results = await _net.BenchmarkDnsAsync();
            DnsResults.ItemsSource = results;
        }

        private async void Mtu_Click(object sender, RoutedEventArgs e)
        {
            MtuStatus.Text = "Discovering optimal MTU...";
            var mtu = await _net.DiscoverMtuAsync();
            MtuStatus.Text = $"Optimal MTU: {mtu} bytes";
        }
    }
}
