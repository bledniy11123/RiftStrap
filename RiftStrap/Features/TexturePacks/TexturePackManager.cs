using System.IO.Compression;
using System.Security.Cryptography;

namespace RiftStrap.Features.TexturePacks
{

    public class TexturePackManager
    {
        private static readonly string PacksDir = Path.Combine(Paths.Base, "TexturePacks");
        private static readonly string ActiveFile = Path.Combine(Paths.Base, "ActiveTexturePack.json");

        private TexturePackInfo? _activePack;

        public TexturePackInfo? ActivePack => _activePack;

        public TexturePackManager()
        {
            Directory.CreateDirectory(PacksDir);
            LoadActive();
        }

        public List<TexturePackInfo> GetInstalledPacks()
        {
            var packs = new List<TexturePackInfo>();

            foreach (var dir in Directory.GetDirectories(PacksDir))
            {
                var infoPath = Path.Combine(dir, "pack.json");
                if (File.Exists(infoPath))
                {
                    try
                    {
                        var json = File.ReadAllText(infoPath);
                        var pack = JsonSerializer.Deserialize<TexturePackInfo>(json);
                        if (pack != null)
                        {
                            pack.Path = dir;
                            pack.FileCount = Directory.GetFiles(dir, "*", SearchOption.AllDirectories).Length - 1;
                            packs.Add(pack);
                        }
                    }
                    catch { }
                }
            }

            return packs;
        }

        public TexturePackInfo? InstallFromZip(string zipPath)
        {
            try
            {
                using var archive = ZipFile.OpenRead(zipPath);

                var manifestEntry = archive.GetEntry("pack.json");
                TexturePackInfo pack;

                if (manifestEntry != null)
                {
                    using var stream = manifestEntry.Open();
                    pack = JsonSerializer.Deserialize<TexturePackInfo>(stream) ?? new();
                }
                else
                {
                    pack = new TexturePackInfo
                    {
                        Id = Path.GetFileNameWithoutExtension(zipPath).ToLowerInvariant().Replace(" ", "-"),
                        Name = Path.GetFileNameWithoutExtension(zipPath),
                        Author = "Unknown",
                    };
                }

                var allowedExtensions = new[] { ".png", ".jpg", ".jpeg", ".dds", ".json", ".ttf", ".otf", ".ogg", ".mp3", ".cur" };
                foreach (var entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name)) continue;
                    if (entry.Name == "pack.json") continue;

                    var ext = System.IO.Path.GetExtension(entry.Name).ToLowerInvariant();
                    if (!allowedExtensions.Contains(ext))
                    {
                        App.Logger.WriteLine("TexturePacks", $"Blocked unsafe file: {entry.FullName}");
                        return null;
                    }
                }

                var packDir = System.IO.Path.Combine(PacksDir, pack.Id);
                if (Directory.Exists(packDir))
                    Directory.Delete(packDir, true);

                archive.ExtractToDirectory(packDir);

                if (manifestEntry == null)
                {
                    var json = JsonSerializer.Serialize(pack, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(System.IO.Path.Combine(packDir, "pack.json"), json);
                }

                pack.Path = packDir;
                App.Logger.WriteLine("TexturePacks", $"Installed texture pack: {pack.Name}");
                return pack;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("TexturePacks", $"Install failed: {ex.Message}");
                return null;
            }
        }

        public TexturePackInfo? InstallFromFolder(string folderPath)
        {
            try
            {
                var name = System.IO.Path.GetFileName(folderPath);
                var pack = new TexturePackInfo
                {
                    Id = name.ToLowerInvariant().Replace(" ", "-"),
                    Name = name,
                    Author = "Local",
                };

                var packDir = System.IO.Path.Combine(PacksDir, pack.Id);
                if (Directory.Exists(packDir))
                    Directory.Delete(packDir, true);

                CopyDirectory(folderPath, packDir);

                var json = JsonSerializer.Serialize(pack, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(System.IO.Path.Combine(packDir, "pack.json"), json);

                pack.Path = packDir;
                App.Logger.WriteLine("TexturePacks", $"Installed from folder: {pack.Name}");
                return pack;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("TexturePacks", $"Folder install failed: {ex.Message}");
                return null;
            }
        }

        public bool Apply(string packId)
        {
            var packDir = System.IO.Path.Combine(PacksDir, packId);
            if (!Directory.Exists(packDir)) return false;

            try
            {

                RemoveActive();

                var contentDir = System.IO.Path.Combine(Paths.Modifications, "content");
                var files = Directory.GetFiles(packDir, "*", SearchOption.AllDirectories);

                foreach (var file in files)
                {
                    if (System.IO.Path.GetFileName(file) == "pack.json") continue;

                    var relativePath = System.IO.Path.GetRelativePath(packDir, file);
                    var destPath = System.IO.Path.Combine(contentDir, relativePath);
                    var destDir = System.IO.Path.GetDirectoryName(destPath);

                    if (destDir != null)
                        Directory.CreateDirectory(destDir);

                    File.Copy(file, destPath, true);
                }

                var infoPath = System.IO.Path.Combine(packDir, "pack.json");
                if (File.Exists(infoPath))
                {
                    _activePack = JsonSerializer.Deserialize<TexturePackInfo>(File.ReadAllText(infoPath));
                    if (_activePack != null) _activePack.Path = packDir;
                }

                SaveActive();
                App.Logger.WriteLine("TexturePacks", $"Applied: {packId}");
                return true;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("TexturePacks", $"Apply failed: {ex.Message}");
                return false;
            }
        }

        public void RemoveActive()
        {
            if (_activePack?.Path == null) return;

            try
            {
                var packDir = _activePack.Path;
                var contentDir = System.IO.Path.Combine(Paths.Modifications, "content");

                foreach (var file in Directory.GetFiles(packDir, "*", SearchOption.AllDirectories))
                {
                    if (System.IO.Path.GetFileName(file) == "pack.json") continue;
                    var relativePath = System.IO.Path.GetRelativePath(packDir, file);
                    var destPath = System.IO.Path.Combine(contentDir, relativePath);

                    if (File.Exists(destPath))
                        File.Delete(destPath);
                }
            }
            catch { }

            _activePack = null;
            SaveActive();
        }

        public void Uninstall(string packId)
        {
            if (_activePack?.Id == packId)
                RemoveActive();

            var packDir = System.IO.Path.Combine(PacksDir, packId);
            if (Directory.Exists(packDir))
                Directory.Delete(packDir, true);
        }

        private void LoadActive()
        {
            if (!File.Exists(ActiveFile)) return;
            try { _activePack = JsonSerializer.Deserialize<TexturePackInfo>(File.ReadAllText(ActiveFile)); }
            catch { _activePack = null; }
        }

        private void SaveActive()
        {
            try
            {
                if (_activePack == null)
                {
                    if (File.Exists(ActiveFile)) File.Delete(ActiveFile);
                    return;
                }
                File.WriteAllText(ActiveFile, JsonSerializer.Serialize(_activePack, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("TexturePacks", $"Failed to save active pack state: {ex.Message}");
            }
        }

        private static void CopyDirectory(string source, string dest)
        {
            Directory.CreateDirectory(dest);
            foreach (var file in Directory.GetFiles(source))
                File.Copy(file, System.IO.Path.Combine(dest, System.IO.Path.GetFileName(file)), true);
            foreach (var dir in Directory.GetDirectories(source))
                CopyDirectory(dir, System.IO.Path.Combine(dest, System.IO.Path.GetFileName(dir)));
        }
    }

    public class TexturePackInfo
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("author")]
        public string Author { get; set; } = "";

        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.0";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        [JsonIgnore]
        public string? Path { get; set; }

        [JsonIgnore]
        public int FileCount { get; set; }

        public string FileCountText => $"{FileCount} files";
    }
}
