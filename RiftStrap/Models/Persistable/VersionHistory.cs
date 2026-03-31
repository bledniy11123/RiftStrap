namespace RiftStrap.Models.Persistable
{
    public class VersionHistoryEntry
    {
        [JsonPropertyName("version_guid")]
        public string VersionGuid { get; set; } = "";

        [JsonPropertyName("version")]
        public string Version { get; set; } = "";

        [JsonPropertyName("channel")]
        public string Channel { get; set; } = "";

        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public string ShortGuid => VersionGuid.Length > 20 ? VersionGuid[..20] + "..." : VersionGuid;
        public string DateText => Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    }

    public class VersionHistory
    {
        [JsonPropertyName("entries")]
        public List<VersionHistoryEntry> Entries { get; set; } = new();
    }
}
