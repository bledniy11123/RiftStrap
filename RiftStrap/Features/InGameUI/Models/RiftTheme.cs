namespace RiftStrap.Features.InGameUI.Models
{
    public class RiftTheme
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

        [JsonPropertyName("name")]
        public string Name { get; set; } = "Untitled Theme";

        [JsonPropertyName("author")]
        public string Author { get; set; } = "Unknown";

        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.0.0";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        [JsonPropertyName("preview")]
        public string? PreviewImage { get; set; }

        [JsonPropertyName("files")]
        public Dictionary<string, string> Files { get; set; } = new();

        [JsonPropertyName("category")]
        public ThemeCategory Category { get; set; } = ThemeCategory.Full;

        [JsonPropertyName("created")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public enum ThemeCategory
    {
        Full,
        Cursors,
        Fonts,
        Sounds,
        Textures
    }

    public class ThemeManifest
    {
        [JsonPropertyName("themes")]
        public List<ThemeInfo> Themes { get; set; } = new();
    }

    public class ThemeInfo
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("author")]
        public string Author { get; set; } = "";

        [JsonPropertyName("version")]
        public string Version { get; set; } = "";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        [JsonPropertyName("category")]
        public ThemeCategory Category { get; set; }

        [JsonPropertyName("download_url")]
        public string DownloadUrl { get; set; } = "";

        [JsonPropertyName("preview_url")]
        public string? PreviewUrl { get; set; }

        [JsonPropertyName("sha256")]
        public string Sha256 { get; set; } = "";

        [JsonPropertyName("downloads")]
        public int Downloads { get; set; }

        [JsonPropertyName("size_bytes")]
        public long SizeBytes { get; set; }
    }
}
