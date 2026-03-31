using System.IO.Compression;
using RiftStrap.Features.InGameUI;
using RiftStrap.Features.InGameUI.Models;

namespace RiftStrap.Features.ThemeCreator
{

    public class ThemeCreatorService
    {
        private static readonly string WorkDir = Path.Combine(Paths.Base, "ThemeWorkspace");
        private static readonly string[] ContentCategories =
        {
            "Cursors", "Textures", "Fonts", "Sounds"
        };

        private RiftTheme _workingTheme = new();
        private Dictionary<string, string> _robloxContentMap = new();
        private Dictionary<string, string> _replacements = new();

        public RiftTheme WorkingTheme => _workingTheme;
        public IReadOnlyDictionary<string, string> RobloxContent => _robloxContentMap;
        public IReadOnlyDictionary<string, string> Replacements => _replacements;

        public ThemeCreatorService()
        {
            Directory.CreateDirectory(WorkDir);
        }

        public void NewTheme(string name, string author, ThemeCategory category)
        {
            _workingTheme = new RiftTheme
            {
                Name = name,
                Author = author,
                Category = category,
            };
            _replacements.Clear();
        }

        public void LoadTheme(RiftTheme theme)
        {
            _workingTheme = theme;
            _replacements.Clear();
            foreach (var (k, v) in theme.Files)
                _replacements[k] = Path.Combine(Paths.Base, "Themes", theme.Id, v);
        }

        public Dictionary<string, List<ContentFile>> ScanContent()
        {
            _robloxContentMap = ThemeEngine.ScanRobloxContent();
            var grouped = new Dictionary<string, List<ContentFile>>();

            foreach (var (relativePath, fullPath) in _robloxContentMap)
            {
                var ext = Path.GetExtension(fullPath).ToLowerInvariant();
                var category = ext switch
                {
                    ".png" or ".jpg" or ".jpeg" when relativePath.Contains("Cursor", StringComparison.OrdinalIgnoreCase) => "Cursors",
                    ".png" or ".jpg" or ".jpeg" => "Textures",
                    ".ttf" or ".otf" => "Fonts",
                    ".ogg" or ".mp3" or ".wav" => "Sounds",
                    _ => "Other"
                };

                if (!grouped.ContainsKey(category))
                    grouped[category] = new();

                grouped[category].Add(new ContentFile
                {
                    RelativePath = relativePath,
                    FullPath = fullPath,
                    FileName = Path.GetFileName(fullPath),
                    Category = category,
                    IsReplaced = _replacements.ContainsKey(relativePath),
                });
            }

            return grouped;
        }

        public void ReplaceFile(string contentRelativePath, string replacementFilePath)
        {
            if (!File.Exists(replacementFilePath)) return;

            var workFile = Path.Combine(WorkDir, Path.GetFileName(replacementFilePath));
            File.Copy(replacementFilePath, workFile, true);

            _replacements[contentRelativePath] = workFile;

            var themeFileName = $"files/{Path.GetFileName(replacementFilePath)}";
            _workingTheme.Files[contentRelativePath] = themeFileName;
        }

        public void RemoveReplacement(string contentRelativePath)
        {
            _replacements.Remove(contentRelativePath);
            _workingTheme.Files.Remove(contentRelativePath);
        }

        public string? Export(string outputDir)
        {
            if (string.IsNullOrWhiteSpace(_workingTheme.Name)) return null;
            if (_replacements.Count == 0) return null;

            try
            {
                var outputPath = Path.Combine(outputDir, $"{_workingTheme.Name.Replace(" ", "_")}.rifttheme");
                if (File.Exists(outputPath)) File.Delete(outputPath);

                var tempDir = Path.Combine(WorkDir, "export_temp");
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
                Directory.CreateDirectory(tempDir);
                Directory.CreateDirectory(Path.Combine(tempDir, "files"));

                foreach (var (contentPath, localFile) in _replacements)
                {
                    if (!File.Exists(localFile)) continue;
                    var destFileName = $"files/{Path.GetFileName(localFile)}";
                    File.Copy(localFile, Path.Combine(tempDir, destFileName), true);
                    _workingTheme.Files[contentPath] = destFileName;
                }

                var json = JsonSerializer.Serialize(_workingTheme, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(Path.Combine(tempDir, "theme.json"), json);

                ZipFile.CreateFromDirectory(tempDir, outputPath);

                Directory.Delete(tempDir, true);

                App.Logger.WriteLine("ThemeCreator", $"Exported theme: {outputPath}");
                return outputPath;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("ThemeCreator", $"Export failed: {ex.Message}");
                return null;
            }
        }
    }

    public class ContentFile
    {
        public string RelativePath { get; set; } = "";
        public string FullPath { get; set; } = "";
        public string FileName { get; set; } = "";
        public string Category { get; set; } = "";
        public bool IsReplaced { get; set; }
    }
}
