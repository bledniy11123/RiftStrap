using System.IO.Compression;
using System.Security.Cryptography;
using RiftStrap.Features.InGameUI.Models;

namespace RiftStrap.Features.InGameUI
{

    public class ThemeEngine
    {
        private static readonly string ThemesDir = Path.Combine(Paths.Base, "Themes");
        private static readonly string ActiveThemeFile = Path.Combine(Paths.Base, "ActiveTheme.json");
        private static readonly string[] AllowedExtensions = { ".png", ".jpg", ".jpeg", ".ogg", ".mp3", ".ttf", ".otf", ".rbxl", ".cur" };

        private RiftTheme? _activeTheme;

        public RiftTheme? ActiveTheme => _activeTheme;

        public ThemeEngine()
        {
            Directory.CreateDirectory(ThemesDir);
            LoadActiveTheme();
        }

        public List<RiftTheme> GetInstalledThemes()
        {
            var themes = new List<RiftTheme>();

            foreach (var dir in Directory.GetDirectories(ThemesDir))
            {
                var manifestPath = Path.Combine(dir, "theme.json");
                if (!File.Exists(manifestPath))
                    continue;

                try
                {
                    var json = File.ReadAllText(manifestPath);
                    var theme = JsonSerializer.Deserialize<RiftTheme>(json);
                    if (theme != null)
                        themes.Add(theme);
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine("ThemeEngine", $"Failed to load theme from {dir}: {ex.Message}");
                }
            }

            return themes;
        }

        public RiftTheme? InstallTheme(string zipPath)
        {
            try
            {
                using var archive = ZipFile.OpenRead(zipPath);

                var manifestEntry = archive.GetEntry("theme.json");
                if (manifestEntry == null)
                {
                    App.Logger.WriteLine("ThemeEngine", "Theme ZIP missing theme.json");
                    return null;
                }

                using var stream = manifestEntry.Open();
                var theme = JsonSerializer.Deserialize<RiftTheme>(stream);
                if (theme == null)
                    return null;

                foreach (var entry in archive.Entries)
                {
                    if (entry.FullName == "theme.json")
                        continue;

                    var ext = Path.GetExtension(entry.Name).ToLowerInvariant();
                    if (!AllowedExtensions.Contains(ext) && !string.IsNullOrEmpty(entry.Name))
                    {
                        App.Logger.WriteLine("ThemeEngine", $"Blocked unsafe file: {entry.FullName} ({ext})");
                        return null;
                    }
                }

                var themeDir = Path.Combine(ThemesDir, theme.Id);
                if (Directory.Exists(themeDir))
                    Directory.Delete(themeDir, true);

                archive.ExtractToDirectory(themeDir);

                App.Logger.WriteLine("ThemeEngine", $"Installed theme: {theme.Name} ({theme.Id})");
                return theme;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("ThemeEngine", $"Failed to install theme: {ex.Message}");
                return null;
            }
        }

        public bool ApplyTheme(string themeId)
        {
            var themeDir = Path.Combine(ThemesDir, themeId);
            var manifestPath = Path.Combine(themeDir, "theme.json");

            if (!File.Exists(manifestPath))
                return false;

            try
            {
                var json = File.ReadAllText(manifestPath);
                var theme = JsonSerializer.Deserialize<RiftTheme>(json);
                if (theme == null)
                    return false;

                RemoveActiveTheme();

                foreach (var (contentPath, themePath) in theme.Files)
                {
                    var sourceFile = Path.Combine(themeDir, themePath);
                    var destFile = Path.Combine(Paths.Modifications, contentPath);

                    if (!File.Exists(sourceFile))
                        continue;

                    var ext = Path.GetExtension(sourceFile).ToLowerInvariant();
                    if (!AllowedExtensions.Contains(ext))
                        continue;

                    var destDir = Path.GetDirectoryName(destFile);
                    if (destDir != null)
                        Directory.CreateDirectory(destDir);

                    File.Copy(sourceFile, destFile, true);
                }

                _activeTheme = theme;
                SaveActiveTheme();

                App.Logger.WriteLine("ThemeEngine", $"Applied theme: {theme.Name}");
                return true;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("ThemeEngine", $"Failed to apply theme: {ex.Message}");
                return false;
            }
        }

        public void RemoveActiveTheme()
        {
            if (_activeTheme == null)
                return;

            foreach (var (contentPath, _) in _activeTheme.Files)
            {
                var filePath = Path.Combine(Paths.Modifications, contentPath);
                try
                {
                    if (File.Exists(filePath))
                        File.Delete(filePath);
                }
                catch
                {

                }
            }

            _activeTheme = null;
            SaveActiveTheme();
            App.Logger.WriteLine("ThemeEngine", "Removed active theme");
        }

        public void UninstallTheme(string themeId)
        {
            if (_activeTheme?.Id == themeId)
                RemoveActiveTheme();

            var themeDir = Path.Combine(ThemesDir, themeId);
            if (Directory.Exists(themeDir))
            {
                Directory.Delete(themeDir, true);
                App.Logger.WriteLine("ThemeEngine", $"Uninstalled theme: {themeId}");
            }
        }

        public string? ExportTheme(string themeId, string outputDir)
        {
            var themeDir = Path.Combine(ThemesDir, themeId);
            if (!Directory.Exists(themeDir))
                return null;

            var manifestPath = Path.Combine(themeDir, "theme.json");
            if (!File.Exists(manifestPath))
                return null;

            try
            {
                var json = File.ReadAllText(manifestPath);
                var theme = JsonSerializer.Deserialize<RiftTheme>(json);
                if (theme == null) return null;

                var outputPath = Path.Combine(outputDir, $"{theme.Name.Replace(" ", "_")}.rifttheme");
                if (File.Exists(outputPath))
                    File.Delete(outputPath);

                ZipFile.CreateFromDirectory(themeDir, outputPath);
                return outputPath;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("ThemeEngine", $"Failed to export theme: {ex.Message}");
                return null;
            }
        }

        public static bool VerifyHash(string filePath, string expectedHash)
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hash = Convert.ToHexString(sha.ComputeHash(stream));
            return string.Equals(hash, expectedHash, StringComparison.OrdinalIgnoreCase);
        }

        public static Dictionary<string, string> ScanRobloxContent()
        {
            var map = new Dictionary<string, string>();

            if (!App.IsPlayerInstalled)
                return map;

            var playerData = new RiftStrap.AppData.RobloxPlayerData();
            var robloxDir = Path.GetDirectoryName(playerData.ExecutablePath);
            if (robloxDir == null || !Directory.Exists(robloxDir))
                return map;

            var contentDir = Path.Combine(robloxDir, "content");
            var platformDir = Path.Combine(robloxDir, "PlatformContent", "pc");

            ScanDirectory(contentDir, "content", map);
            ScanDirectory(platformDir, Path.Combine("PlatformContent", "pc"), map);

            return map;
        }

        private static void ScanDirectory(string dir, string prefix, Dictionary<string, string> map)
        {
            if (!Directory.Exists(dir))
                return;

            foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                if (!AllowedExtensions.Contains(ext))
                    continue;

                var relativePath = Path.GetRelativePath(Path.GetDirectoryName(dir)!, file);
                map[relativePath] = file;
            }
        }

        private void LoadActiveTheme()
        {
            if (!File.Exists(ActiveThemeFile))
                return;

            try
            {
                var json = File.ReadAllText(ActiveThemeFile);
                _activeTheme = JsonSerializer.Deserialize<RiftTheme>(json);
            }
            catch
            {
                _activeTheme = null;
            }
        }

        private void SaveActiveTheme()
        {
            try
            {
                if (_activeTheme == null)
                {
                    if (File.Exists(ActiveThemeFile))
                        File.Delete(ActiveThemeFile);
                    return;
                }

                var json = JsonSerializer.Serialize(_activeTheme, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ActiveThemeFile, json);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("ThemeEngine", $"Failed to save active theme: {ex.Message}");
            }
        }
    }
}
