using System.Windows;
using System.Windows.Input;
using RiftStrap.Features.PerformanceDashboard;
using RiftStrap.Features.PerformanceDashboard.Models;

namespace RiftStrap.UI.Windows
{
    public partial class PerformanceOverlay : Window
    {
        private readonly PerformanceMonitor _monitor = new();

        public PerformanceOverlay()
        {
            InitializeComponent();

            _monitor.OnSnapshot += snapshot => Dispatcher.Invoke(() => UpdateUI(snapshot));
            _monitor.OnProcessLost += () => Dispatcher.Invoke(Close);

            Loaded += (_, _) => _monitor.Start();
            Closed += (_, _) => _monitor.Dispose();
        }

        private void UpdateUI(PerformanceSnapshot s)
        {
            FpsText.Text = s.Fps > 0 ? $"{s.Fps:F0}" : "--";
            CpuText.Text = $"{s.CpuPercent:F0}%";
            RamText.Text = $"{s.RamMB:F0} MB";
            PingText.Text = s.PingMs > 0 ? $"{s.PingMs} ms" : "-- ms";
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }
    }
}
