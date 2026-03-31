namespace RiftStrap.Features.GameProfiles.Models
{
    public class GameProfile
    {
        [JsonPropertyName("place_id")]
        public long PlaceId { get; set; }

        [JsonPropertyName("universe_id")]
        public long UniverseId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = "Unknown Game";

        [JsonPropertyName("icon_url")]
        public string? IconUrl { get; set; }

        [JsonPropertyName("fast_flags")]
        public Dictionary<string, object> FastFlags { get; set; } = new();

        [JsonPropertyName("theme_id")]
        public string? ThemeId { get; set; }

        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonPropertyName("notes")]
        public string Notes { get; set; } = "";

        [JsonPropertyName("last_played")]
        public DateTime? LastPlayed { get; set; }

        [JsonPropertyName("created")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class GameProfilesStore
    {
        [JsonPropertyName("profiles")]
        public Dictionary<long, GameProfile> Profiles { get; set; } = new();

        [JsonPropertyName("auto_detect")]
        public bool AutoDetectEnabled { get; set; } = true;
    }
}
