using RiftStrap.Features.PerformanceDashboard.Models;

namespace RiftStrap.Features.PerformanceDashboard
{

    public class PerformanceMonitor : IDisposable
    {
        private const int BufferSize = 300;
        private const int SampleIntervalMs = 1000;

        private readonly List<PerformanceSnapshot> _buffer = new(BufferSize);
        private readonly object _lock = new();
        private CancellationTokenSource? _cts;
        private Process? _robloxProcess;
        private DateTime _lastCpuTime;
        private TimeSpan _lastTotalProcessorTime;
        private double _logFps;
        private int _logPing;

        public void SetLogData(double fps, int ping)
        {
            if (fps > 0) _logFps = fps;
            if (ping > 0) _logPing = ping;
        }

        public event Action<PerformanceSnapshot>? OnSnapshot;
        public event Action? OnProcessLost;

        public IReadOnlyList<PerformanceSnapshot> Buffer
        {
            get { lock (_lock) return _buffer.ToList(); }
        }

        public PerformanceSnapshot? Latest
        {
            get { lock (_lock) return _buffer.Count > 0 ? _buffer[^1] : null; }
        }

        public bool IsRunning => _cts != null && !_cts.IsCancellationRequested;

        public void Start(Process? process = null)
        {
            Stop();

            _robloxProcess = process ?? FindRobloxProcess();
            if (_robloxProcess == null)
            {
                App.Logger.WriteLine("PerformanceMonitor", "No Roblox process found");
                return;
            }

            _lastCpuTime = DateTime.UtcNow;
            try { _lastTotalProcessorTime = _robloxProcess.TotalProcessorTime; } catch { }

            _cts = new CancellationTokenSource();
            Task.Run(() => MonitorLoop(_cts.Token));

            App.Logger.WriteLine("PerformanceMonitor", $"Started monitoring PID {_robloxProcess.Id}");
        }

        public void Stop()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        public void Dispose()
        {
            Stop();
            GC.SuppressFinalize(this);
        }

        public PerformanceSession GetSessionSummary(long placeId, string gameName)
        {
            lock (_lock)
            {
                return new PerformanceSession
                {
                    PlaceId = placeId,
                    GameName = gameName,
                    StartTime = _buffer.Count > 0 ? _buffer[0].Timestamp : DateTime.UtcNow,
                    EndTime = DateTime.UtcNow,
                    AvgFps = _buffer.Count > 0 ? _buffer.Average(s => s.Fps) : 0,
                    AvgPing = _buffer.Count > 0 ? (int)_buffer.Average(s => s.PingMs) : 0,
                    PeakRamMB = _buffer.Count > 0 ? _buffer.Max(s => s.RamMB) : 0,
                };
            }
        }

        private async Task MonitorLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    if (_robloxProcess == null || _robloxProcess.HasExited)
                    {
                        OnProcessLost?.Invoke();
                        break;
                    }

                    var snapshot = CollectSnapshot();

                    lock (_lock)
                    {
                        if (_buffer.Count >= BufferSize)
                            _buffer.RemoveAt(0);
                        _buffer.Add(snapshot);
                    }

                    OnSnapshot?.Invoke(snapshot);
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine("PerformanceMonitor", $"Sample error: {ex.Message}");
                }

                try { await Task.Delay(SampleIntervalMs, ct); } catch (TaskCanceledException) { break; }
            }
        }

        private PerformanceSnapshot CollectSnapshot()
        {
            var snapshot = new PerformanceSnapshot();

            if (_robloxProcess == null) return snapshot;

            try
            {
                _robloxProcess.Refresh();

                snapshot.RamBytes = _robloxProcess.WorkingSet64;

                var now = DateTime.UtcNow;
                var currentCpuTime = _robloxProcess.TotalProcessorTime;
                var elapsed = (now - _lastCpuTime).TotalMilliseconds;

                if (elapsed > 0)
                {
                    var cpuDelta = (currentCpuTime - _lastTotalProcessorTime).TotalMilliseconds;
                    snapshot.CpuPercent = Math.Min(100, cpuDelta / elapsed / Environment.ProcessorCount * 100);
                }

                _lastCpuTime = now;
                _lastTotalProcessorTime = currentCpuTime;
            }
            catch
            {

            }

            snapshot.Fps = _logFps;
            snapshot.PingMs = _logPing;

            return snapshot;
        }

        private static Process? FindRobloxProcess()
        {
            try
            {
                return Process.GetProcessesByName("RobloxPlayerBeta").FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }
    }
}
