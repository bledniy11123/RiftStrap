using System.Security.Cryptography;
using RiftStrap.Features.CommunityHub.Models;
using RiftStrap.Features.InGameUI;

namespace RiftStrap.Features.CommunityHub
{

    public class HubService
    {
        private const string CatalogUrl = "https://raw.githubusercontent.com/riftstrap/riftstrap-themes/main/catalog.json";
        private static readonly string CacheDir = Path.Combine(Paths.Base, "HubCache");

        private HubCatalog? _catalog;

        public HubCatalog? Catalog => _catalog;

        public HubService()
        {
            Directory.CreateDirectory(CacheDir);
        }

        public async Task<HubCatalog?> FetchCatalogAsync()
        {
            // The default themes catalog repo does not exist yet (404). Skip the failing request and
            // fall back to any cached catalog; callers already tolerate a null/empty catalog.
            if (string.IsNullOrEmpty(CatalogUrl) || CatalogUrl.Contains("riftstrap/riftstrap-themes"))
            {
                App.Logger.WriteLine("HubService", "Themes catalog URL not configured; using cache");
                return LoadCachedCatalog();
            }

            try
            {
                var json = await App.HttpClient.GetStringAsync(CatalogUrl);
                _catalog = JsonSerializer.Deserialize<HubCatalog>(json);

                await File.WriteAllTextAsync(Path.Combine(CacheDir, "catalog.json"), json);

                App.Logger.WriteLine("HubService", $"Fetched catalog: {_catalog?.Themes.Count ?? 0} themes");
                return _catalog;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("HubService", $"Failed to fetch catalog: {ex.Message}");

                return LoadCachedCatalog();
            }
        }

        public List<HubTheme> Search(string query)
        {
            if (_catalog == null) return new();
            if (string.IsNullOrWhiteSpace(query)) return _catalog.Themes;

            var q = query.ToLowerInvariant();
            return _catalog.Themes
                .Where(t => t.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || t.Author.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || t.Description.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || t.Tags.Any(tag => tag.Contains(q, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        public List<HubTheme> FilterByCategory(string category)
        {
            if (_catalog == null) return new();
            if (category == "All") return _catalog.Themes;
            return _catalog.Themes.Where(t => t.Category == category).ToList();
        }

        public async Task<bool> DownloadAndInstallAsync(HubTheme hubTheme, ThemeEngine engine, IProgress<double>? progress = null)
        {
            var tempPath = Path.Combine(CacheDir, $"{hubTheme.Id}.rifttheme");

            try
            {

                using var response = await App.HttpClient.GetAsync(hubTheme.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1;
                var downloadedBytes = 0L;

                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None);

                var buffer = new byte[8192];
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                    downloadedBytes += bytesRead;

                    if (totalBytes > 0)
                        progress?.Report((double)downloadedBytes / totalBytes);
                }

                fileStream.Close();

                if (!string.IsNullOrEmpty(hubTheme.Sha256))
                {
                    if (!ThemeEngine.VerifyHash(tempPath, hubTheme.Sha256))
                    {
                        App.Logger.WriteLine("HubService", $"Hash mismatch for {hubTheme.Name}");
                        File.Delete(tempPath);
                        return false;
                    }
                }

                var result = engine.InstallTheme(tempPath);

                try { File.Delete(tempPath); } catch { }

                App.Logger.WriteLine("HubService", $"Installed hub theme: {hubTheme.Name}");
                return result != null;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("HubService", $"Failed to download theme: {ex.Message}");
                try { File.Delete(tempPath); } catch { }
                return false;
            }
        }

        private HubCatalog? LoadCachedCatalog()
        {
            var cachePath = Path.Combine(CacheDir, "catalog.json");
            if (!File.Exists(cachePath)) return null;

            try
            {
                var json = File.ReadAllText(cachePath);
                _catalog = JsonSerializer.Deserialize<HubCatalog>(json);
                return _catalog;
            }
            catch
            {
                return null;
            }
        }
    }
}
