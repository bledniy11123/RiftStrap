using System.Windows;
using System.Windows.Input;
using RiftStrap.Features.PerformanceDashboard;
using RiftStrap.Features.PerformanceDashboard.Models;
using RiftStrap.Integrations;

namespace RiftStrap.UI.Windows
{
    public partial class PerformanceOverlay : Window
    {
        private readonly PerformanceMonitor _monitor = new();
        private ActivityWatcher? _logWatcher;
        private double _fps;
        private int _ping;

        public PerformanceOverlay()
        {
            InitializeComponent();

            _monitor.OnSnapshot += snapshot => Dispatcher.Invoke(() => UpdateUI(snapshot));
            _monitor.OnProcessLost += () => Dispatcher.Invoke(Close);

            Loaded += (_, _) =>
            {
                _monitor.Start();
                StartLogWatch();
            };
            Closed += (_, _) =>
            {
                _monitor.Dispose();
                _logWatcher?.Dispose();
            };
        }

        // FPS/ping are only available by parsing the Roblox log. The watcher process feeds its
        // own PerformanceMonitor that way, but this overlay lives in a different process, so it
        // must tail the log itself — otherwise FpsText/PingText stay stuck at "--"/"-- ms".
        private void StartLogWatch()
        {
            try
            {
                _logWatcher = new ActivityWatcher();   // null log path -> auto-locates newest Roblox Player log
                _logWatcher.OnLogEntry += (_, line) =>
                {
                    if (line.Contains("[FLog::ClientProfiler]") && line.Contains("framerate"))
                    {
                        var m = System.Text.RegularExpressions.Regex.Match(line, @"framerate:\s*([\d.]+)");
                        if (m.Success && double.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var fps))
                            _fps = fps;
                    }

                    if (line.Contains("[FLog::Network]") && line.Contains("ping:"))
                    {
                        var m = System.Text.RegularExpressions.Regex.Match(line, @"ping:\s*(\d+)");
                        if (m.Success && int.TryParse(m.Groups[1].Value, out var ping))
                            _ping = ping;
                    }

                    _monitor.SetLogData(_fps, _ping);
                };
                _logWatcher.Start();
            }
            catch { }
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
