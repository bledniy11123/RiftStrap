using System.Management;

namespace RiftStrap.Features.FPSUnlocker
{

    public class SmartFPSService
    {
        private static readonly string FpsFlag = "DFIntTaskSchedulerTargetFps";

        public enum FPSPreset
        {
            Performance,
            Balanced,
            Quality,
            Unlimited,
            Custom
        }

        public static int GetMonitorRefreshRate()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT CurrentRefreshRate FROM Win32_VideoController");
                foreach (var obj in searcher.Get())
                {
                    var rate = Convert.ToInt32(obj["CurrentRefreshRate"] ?? 60);
                    if (rate > 0) return rate;
                }
            }
            catch { }
            return 60;
        }

        public static void ApplyPreset(FPSPreset preset, int customFps = 0)
        {
            int targetFps = preset switch
            {
                FPSPreset.Performance => 30,
                FPSPreset.Balanced => 60,
                FPSPreset.Quality => GetMonitorRefreshRate(),
                FPSPreset.Unlimited => 9999,
                FPSPreset.Custom => Math.Max(15, customFps),
                _ => 60
            };

            App.FastFlags.SetValue(FpsFlag, targetFps);
            App.FastFlags.Save();

            App.Logger.WriteLine("SmartFPS", $"Applied {preset} preset: {targetFps} FPS");
        }

        public static int GetCurrentTarget()
        {
            if (App.FastFlags.Prop.TryGetValue(FpsFlag, out var value))
            {
                if (value is int i) return i;
                if (value is long l) return (int)l;
                if (int.TryParse(value?.ToString(), out var parsed)) return parsed;
            }
            return 60;
        }

        public static FPSPreset GetCurrentPreset()
        {
            var current = GetCurrentTarget();
            var monitorHz = GetMonitorRefreshRate();

            return current switch
            {
                30 => FPSPreset.Performance,
                60 => FPSPreset.Balanced,
                9999 => FPSPreset.Unlimited,
                _ when current == monitorHz => FPSPreset.Quality,
                _ => FPSPreset.Custom
            };
        }

        public static string GetDescription()
        {
            var current = GetCurrentTarget();
            var preset = GetCurrentPreset();
            var hz = GetMonitorRefreshRate();

            return preset switch
            {
                FPSPreset.Performance => $"Performance Mode — 30 FPS (saves battery)",
                FPSPreset.Balanced => $"Balanced — 60 FPS",
                FPSPreset.Quality => $"Quality — {hz} FPS (matches {hz}Hz monitor)",
                FPSPreset.Unlimited => $"Unlimited — uncapped FPS",
                FPSPreset.Custom => $"Custom — {current} FPS",
                _ => $"{current} FPS"
            };
        }
    }
}
