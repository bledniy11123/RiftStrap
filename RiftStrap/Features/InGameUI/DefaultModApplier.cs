using System.Runtime.InteropServices;

namespace RiftStrap.Features.InGameUI
{

    public static class DefaultModApplier
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool SetWindowText(IntPtr hWnd, string text);

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string? className, string? windowName);

        public static void EnsureDefaultMods()
        {
            try
            {
                EnsureDefaultFastFlags();

                CleanupCoreScriptMods();
                App.Logger.WriteLine("DefaultModApplier", "Default mods applied");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("DefaultModApplier", $"Failed to apply default mods: {ex.Message}");
            }
        }

        private static void EnsureDefaultFastFlags()
        {

            if (App.FastFlags.Prop.Count > 0)
                return;

            try
            {

                var hwInfo = HardwareOptimizer.HardwareDetector.Detect();
                var optimalFlags = HardwareOptimizer.OptimalConfigGenerator.Generate(hwInfo);

                foreach (var (key, value) in optimalFlags)
                    App.FastFlags.SetValue(key, value);

                App.Logger.WriteLine("DefaultModApplier", $"Auto-optimized for {hwInfo.Tier} tier ({hwInfo.CpuName}, {hwInfo.GpuName})");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("DefaultModApplier", $"Hardware detection failed, using basic defaults: {ex.Message}");
            }

            App.FastFlags.SetValue("DFIntTaskSchedulerTargetFps", 9999);
            App.FastFlags.SetValue("FFlagHandleAltEnterFullscreenManually", "False");

            App.FastFlags.Save();
            App.Logger.WriteLine("DefaultModApplier", "Applied default FastFlags (uncapped FPS)");
        }

        private static void CleanupCoreScriptMods()
        {
            try
            {

                var coreScriptDir = Path.Combine(Paths.Modifications, "ExtraContent", "scripts");
                if (Directory.Exists(coreScriptDir))
                {
                    Directory.Delete(coreScriptDir, true);
                    App.Logger.WriteLine("DefaultModApplier", "Cleaned up CoreScript mods");
                }

                var cursorDir = Path.Combine(Paths.Modifications, "content", "textures", "Cursors", "KeyboardMouse");
                if (Directory.Exists(cursorDir))
                {
                    Directory.Delete(cursorDir, true);
                    App.Logger.WriteLine("DefaultModApplier", "Cleaned up generated cursors from Modifications");
                }

                if (Directory.Exists(Paths.Versions))
                {
                    foreach (var versionDir in Directory.GetDirectories(Paths.Versions))
                    {
                        var vCursorDir = Path.Combine(versionDir, "content", "textures", "Cursors", "KeyboardMouse");
                        if (Directory.Exists(vCursorDir))
                        {

                            foreach (var file in Directory.GetFiles(vCursorDir, "Arrow*.png"))
                            {
                                try
                                {
                                    var info = new FileInfo(file);

                                    if (info.Length > 3000)
                                    {
                                        File.Delete(file);
                                        App.Logger.WriteLine("DefaultModApplier", $"Deleted oversized cursor: {file} ({info.Length} bytes)");
                                    }
                                }
                                catch { }
                            }
                        }

                        var vScriptsDir = Path.Combine(versionDir, "ExtraContent", "scripts", "CoreScripts", "Modules", "Settings", "Pages", "RiftStrapPage.lua");
                        if (File.Exists(vScriptsDir))
                        {
                            try { File.Delete(vScriptsDir); } catch { }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("DefaultModApplier", $"Cleanup error: {ex.Message}");
            }
        }

        private static void EnsureDefaultCursors()
        {
            var cursorDir = Path.Combine(Paths.Modifications, "content", "textures", "Cursors", "KeyboardMouse");

            if (Directory.Exists(cursorDir) && Directory.GetFiles(cursorDir, "*.png").Length > 0)
                return;

            Directory.CreateDirectory(cursorDir);

            GenerateMinimalCursor(Path.Combine(cursorDir, "ArrowCursor.png"), 64, false);
            GenerateMinimalCursor(Path.Combine(cursorDir, "ArrowFarCursor.png"), 64, true);

            App.Logger.WriteLine("DefaultModApplier", "Generated default RiftStrap cursors");
        }

        private static void GenerateMinimalCursor(string path, int size, bool isSmall)
        {
            using var bmp = new System.Drawing.Bitmap(size, size);
            using var g = System.Drawing.Graphics.FromImage(bmp);

            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(System.Drawing.Color.Transparent);

            var scale = isSmall ? 0.65f : 1.0f;
            if (isSmall)
            {
                g.ScaleTransform(scale, scale);
                g.TranslateTransform(8, 6);
            }

            var points = new System.Drawing.Point[]
            {
                new(8, 4),
                new(8, 46),
                new(18, 36),
                new(28, 52),
                new(34, 48),
                new(24, 32),
                new(38, 32),
            };

            using var fill = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(240, 240, 240));
            using var outline = new System.Drawing.Pen(System.Drawing.Color.FromArgb(30, 30, 30), 2f);

            g.FillPolygon(fill, points);
            g.DrawPolygon(outline, points);

            bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        }

        public static void SetRobloxWindowTitle(string title = "Roblox — RiftStrap")
        {
            Task.Run(async () =>
            {

                for (int i = 0; i < 30; i++)
                {
                    await Task.Delay(1000);

                    try
                    {
                        var processes = Process.GetProcessesByName("RobloxPlayerBeta");
                        try
                        {
                            foreach (var proc in processes)
                            {
                                if (proc.MainWindowHandle != IntPtr.Zero)
                                {
                                    SetWindowText(proc.MainWindowHandle, title);
                                    App.Logger.WriteLine("DefaultModApplier", $"Set Roblox window title: {title}");
                                    return;
                                }
                            }
                        }
                        finally
                        {
                            foreach (var proc in processes)
                                proc.Dispose();
                        }
                    }
                    catch { }
                }
            });
        }
    }
}
