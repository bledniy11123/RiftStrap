namespace RiftStrap.Features.CommunityHub.Models
{
    public class HubCatalog
    {
        [JsonPropertyName("version")]
        public int Version { get; set; } = 1;

        [JsonPropertyName("themes")]
        public List<HubTheme> Themes { get; set; } = new();
    }

    public class HubTheme
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("author")]
        public string Author { get; set; } = "";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.0.0";

        [JsonPropertyName("category")]
        public string Category { get; set; } = "Full";

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

        [JsonPropertyName("tags")]
        public List<string> Tags { get; set; } = new();

        public string SizeText => SizeBytes > 1024 * 1024
            ? $"{SizeBytes / 1024.0 / 1024:F1} MB"
            : $"{SizeBytes / 1024.0:F0} KB";

        public string DownloadsText => Downloads > 1000
            ? $"{Downloads / 1000.0:F1}k"
            : Downloads.ToString();
    }
}
