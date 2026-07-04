using System.Management;
using System.Text.RegularExpressions;

namespace RiftStrap.Features.FPSUnlocker
{

    public class SmartFPSService
    {
        private static readonly string FpsFlag = "DFIntTaskSchedulerTargetFps";

        // The REAL Roblox FPS cap (the FastFlag above is ignored for capping in current Roblox).
        // Shared across all of the user's Roblox installs.
        private static readonly string GlobalSettingsFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Roblox", "GlobalBasicSettings_13.xml");

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

            // THIS is what actually caps Roblox's FPS. Modern Roblox ignores the FastFlag above,
            // so write the in-game FramerateCap directly — that's the real fix.
            bool ok = SetInGameFramerateCap(targetFps);

            App.Logger.WriteLine("SmartFPS", $"Applied {preset} preset: {targetFps} FPS (FramerateCap written: {ok})");
        }

        // Writes Roblox's in-game FramerateCap. Roblox reads this at startup, so it takes effect on the
        // NEXT launch. If Roblox is running it overwrites this file on exit, so set the cap while it's
        // closed. Returns false only if the settings file doesn't exist yet (Roblox never ran).
        public static bool SetInGameFramerateCap(int fps)
        {
            try
            {
                if (!File.Exists(GlobalSettingsFile))
                    return false;

                string xml = File.ReadAllText(GlobalSettingsFile);
                string node = $"<int name=\"FramerateCap\">{fps}</int>";
                var rx = new Regex("<int name=\"FramerateCap\">\\s*-?\\d+\\s*</int>");

                xml = rx.IsMatch(xml)
                    ? rx.Replace(xml, node, 1)
                    : xml.Replace("</roblox>", "\t" + node + "\r\n</roblox>");

                // Take a one-time backup before the first edit so a bad regex replacement
                // can be recovered, then write atomically (tmp + replace) so a crash mid-write
                // can't corrupt the shared Roblox settings file.
                string bak = GlobalSettingsFile + ".bak";
                if (!File.Exists(bak))
                    File.Copy(GlobalSettingsFile, bak);

                string tmp = GlobalSettingsFile + ".tmp";
                File.WriteAllText(tmp, xml);
                File.Move(tmp, GlobalSettingsFile, true);
                App.Logger.WriteLine("SmartFPS", $"Set in-game FramerateCap = {fps}");
                return true;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("SmartFPS", $"Failed to set in-game FramerateCap: {ex.Message}");
                return false;
            }
        }

        // Reads the current in-game FramerateCap (the real cap), or null if unavailable.
        public static int? GetInGameFramerateCap()
        {
            try
            {
                if (!File.Exists(GlobalSettingsFile))
                    return null;

                var m = Regex.Match(File.ReadAllText(GlobalSettingsFile), "<int name=\"FramerateCap\">\\s*(-?\\d+)\\s*</int>");
                if (m.Success && int.TryParse(m.Groups[1].Value, out var v))
                    return v;
            }
            catch { }
            return null;
        }

        public static int GetCurrentTarget()
        {
            // Prefer the real in-game cap so the UI reflects what Roblox will actually do
            // (including a value the user set inside Roblox itself).
            var inGame = GetInGameFramerateCap();
            if (inGame is > 0)
                return inGame.Value;

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
                // Quality (cap == monitor Hz) must be detected before the 30/60 literals,
                // otherwise a 60 Hz (or 30 Hz) monitor misclassifies Quality as Balanced/Performance.
                _ when current == monitorHz => FPSPreset.Quality,
                30 => FPSPreset.Performance,
                60 => FPSPreset.Balanced,
                9999 => FPSPreset.Unlimited,
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
