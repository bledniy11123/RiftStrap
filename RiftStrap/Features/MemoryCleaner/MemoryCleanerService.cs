using System.Runtime.InteropServices;

namespace RiftStrap.Features.MemoryCleaner
{

    public class MemoryCleanerService : IDisposable
    {
        [DllImport("psapi.dll")]
        private static extern bool EmptyWorkingSet(IntPtr hProcess);

        [DllImport("kernel32.dll")]
        private static extern bool SetProcessWorkingSetSize(IntPtr proc, int min, int max);

        public int ThresholdMB { get; set; } = 2048;
        public int IntervalSeconds { get; set; } = 30;
        public bool AutoCleanEnabled { get; set; } = true;

        private CancellationTokenSource? _cts;
        private long _lastCleanedBytes;
        private int _cleanCount;

        public long LastCleanedMB => _lastCleanedBytes / 1024 / 1024;
        public int TotalCleans => _cleanCount;

        public event Action<long>? OnCleaned;

        public void Start()
        {
            Stop();
            if (!AutoCleanEnabled) return;

            _cts = new CancellationTokenSource();
            Task.Run(() => MonitorLoop(_cts.Token));
            App.Logger.WriteLine("MemoryCleaner", $"Started (threshold: {ThresholdMB}MB, interval: {IntervalSeconds}s)");
        }

        public void Stop()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        public long CleanNow()
        {
            try
            {
                var processes = Process.GetProcessesByName("RobloxPlayerBeta");
                if (processes.Length == 0) return 0;

                long totalFreed = 0;

                foreach (var proc in processes)
                {
                    try
                    {
                        var beforeMem = proc.WorkingSet64;

                        EmptyWorkingSet(proc.Handle);
                        SetProcessWorkingSetSize(proc.Handle, -1, -1);

                        proc.Refresh();
                        var afterMem = proc.WorkingSet64;
                        var freed = Math.Max(0, beforeMem - afterMem);
                        totalFreed += freed;
                    }
                    catch { }
                    finally { proc.Dispose(); }
                }

                _lastCleanedBytes = totalFreed;
                _cleanCount++;
                OnCleaned?.Invoke(totalFreed);

                App.Logger.WriteLine("MemoryCleaner", $"Cleaned {totalFreed / 1024 / 1024}MB");
                return totalFreed;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("MemoryCleaner", $"Clean failed: {ex.Message}");
                return 0;
            }
        }

        public static long GetRobloxMemoryMB()
        {
            try
            {
                var processes = Process.GetProcessesByName("RobloxPlayerBeta");
                long total = 0;
                foreach (var proc in processes)
                {
                    total += proc.WorkingSet64;
                    proc.Dispose();
                }
                return total / 1024 / 1024;
            }
            catch { return 0; }
        }

        private async Task MonitorLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var memMB = GetRobloxMemoryMB();
                    if (memMB > ThresholdMB)
                    {
                        App.Logger.WriteLine("MemoryCleaner", $"Roblox using {memMB}MB (threshold: {ThresholdMB}MB), cleaning...");
                        CleanNow();
                    }
                }
                catch { }

                try { await Task.Delay(IntervalSeconds * 1000, ct); }
                catch (TaskCanceledException) { break; }
            }
        }

        public void Dispose()
        {
            Stop();
            GC.SuppressFinalize(this);
        }
    }
}
