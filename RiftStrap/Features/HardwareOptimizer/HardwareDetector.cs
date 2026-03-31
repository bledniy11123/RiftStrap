using System.Management;
using RiftStrap.Features.HardwareOptimizer.Models;

namespace RiftStrap.Features.HardwareOptimizer
{

    public static class HardwareDetector
    {
        private static readonly string[] IntegratedGpuKeywords =
        {
            "intel hd", "intel uhd", "intel iris", "intel arc",
            "amd radeon graphics", "amd radeon vega", "microsoft basic",
        };

        public static HardwareInfo Detect()
        {
            var info = new HardwareInfo();

            try { DetectCpu(info); } catch (Exception ex) { App.Logger.WriteLine("HardwareDetector", $"CPU detection failed: {ex.Message}"); }
            try { DetectGpu(info); } catch (Exception ex) { App.Logger.WriteLine("HardwareDetector", $"GPU detection failed: {ex.Message}"); }
            try { DetectRam(info); } catch (Exception ex) { App.Logger.WriteLine("HardwareDetector", $"RAM detection failed: {ex.Message}"); }
            try { DetectMonitor(info); } catch (Exception ex) { App.Logger.WriteLine("HardwareDetector", $"Monitor detection failed: {ex.Message}"); }

            info.Tier = ClassifyTier(info);

            App.Logger.WriteLine("HardwareDetector",
                $"Detected: {info.CpuName} ({info.CpuCores}c) | {info.GpuName} ({info.GpuVramBytes / 1024 / 1024}MB) | {info.TotalRamGB:F1}GB RAM | {info.MonitorRefreshRate}Hz | Tier: {info.Tier}");

            return info;
        }

        private static void DetectCpu(HardwareInfo info)
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, NumberOfCores, MaxClockSpeed FROM Win32_Processor");
            foreach (var obj in searcher.Get())
            {
                info.CpuName = obj["Name"]?.ToString()?.Trim() ?? "Unknown";
                info.CpuCores = Convert.ToInt32(obj["NumberOfCores"] ?? 0);
                info.CpuMaxClockMHz = Convert.ToInt32(obj["MaxClockSpeed"] ?? 0);
                break;
            }
        }

        private static void DetectGpu(HardwareInfo info)
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, AdapterRAM FROM Win32_VideoController");

            string bestGpuName = "";
            long bestVram = 0;

            foreach (var obj in searcher.Get())
            {
                var name = obj["Name"]?.ToString()?.Trim() ?? "";
                var vram = Convert.ToInt64(obj["AdapterRAM"] ?? 0);

                if (vram > bestVram || string.IsNullOrEmpty(bestGpuName))
                {
                    bestGpuName = name;
                    bestVram = vram;
                }
            }

            info.GpuName = bestGpuName;
            info.GpuVramBytes = bestVram;
            info.IsIntegratedGpu = IntegratedGpuKeywords.Any(k =>
                info.GpuName.Contains(k, StringComparison.OrdinalIgnoreCase));
        }

        private static void DetectRam(HardwareInfo info)
        {
            using var searcher = new ManagementObjectSearcher("SELECT Capacity FROM Win32_PhysicalMemory");
            long total = 0;
            foreach (var obj in searcher.Get())
            {
                total += Convert.ToInt64(obj["Capacity"] ?? 0);
            }
            info.TotalRamBytes = total;
        }

        private static void DetectMonitor(HardwareInfo info)
        {

            var screen = System.Windows.Forms.Screen.PrimaryScreen;
            if (screen != null)
            {
                info.MonitorWidth = screen.Bounds.Width;
                info.MonitorHeight = screen.Bounds.Height;
            }

            using var searcher = new ManagementObjectSearcher("SELECT CurrentRefreshRate FROM Win32_VideoController");
            foreach (var obj in searcher.Get())
            {
                var rate = Convert.ToInt32(obj["CurrentRefreshRate"] ?? 60);
                if (rate > info.MonitorRefreshRate)
                    info.MonitorRefreshRate = rate;
            }
        }

        private static HardwareTier ClassifyTier(HardwareInfo info)
        {

            if (!info.IsIntegratedGpu && info.GpuVramBytes >= 8L * 1024 * 1024 * 1024
                && info.TotalRamGB >= 16 && info.MonitorRefreshRate >= 144)
                return HardwareTier.Ultra;

            if (!info.IsIntegratedGpu && info.GpuVramBytes >= 4L * 1024 * 1024 * 1024
                && info.TotalRamGB >= 16)
                return HardwareTier.High;

            if (!info.IsIntegratedGpu || info.TotalRamGB >= 8)
                return HardwareTier.Mid;

            return HardwareTier.Low;
        }
    }
}
