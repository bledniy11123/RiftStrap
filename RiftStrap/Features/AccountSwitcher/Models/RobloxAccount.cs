namespace RiftStrap.Features.AccountSwitcher.Models
{
    public class RobloxAccount
    {
        [JsonPropertyName("id")]
        public long UserId { get; set; }

        [JsonPropertyName("username")]
        public string Username { get; set; } = "";

        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; } = "";

        [JsonPropertyName("avatar_url")]
        public string? AvatarUrl { get; set; }

        [JsonPropertyName("added_at")]
        public DateTime AddedAt { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("last_used")]
        public DateTime? LastUsed { get; set; }

        [JsonIgnore]
        public string? Cookie { get; set; }
    }

    public class AccountStore
    {
        [JsonPropertyName("accounts")]
        public List<RobloxAccount> Accounts { get; set; } = new();

        [JsonPropertyName("active_id")]
        public long? ActiveUserId { get; set; }
    }
}
