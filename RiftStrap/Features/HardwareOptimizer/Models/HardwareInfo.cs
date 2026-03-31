namespace RiftStrap.Features.HardwareOptimizer.Models
{
    public class HardwareInfo
    {
        public string CpuName { get; set; } = "Unknown";
        public int CpuCores { get; set; }
        public int CpuMaxClockMHz { get; set; }

        public string GpuName { get; set; } = "Unknown";
        public long GpuVramBytes { get; set; }
        public bool IsIntegratedGpu { get; set; }

        public long TotalRamBytes { get; set; }
        public double TotalRamGB => TotalRamBytes / (1024.0 * 1024 * 1024);

        public int MonitorRefreshRate { get; set; } = 60;
        public int MonitorWidth { get; set; }
        public int MonitorHeight { get; set; }

        public HardwareTier Tier { get; set; } = HardwareTier.Mid;

        public string Hash => $"{CpuName}|{GpuName}|{TotalRamBytes}".GetHashCode().ToString("X");
    }

    public enum HardwareTier
    {
        Low,
        Mid,
        High,
        Ultra
    }
}
