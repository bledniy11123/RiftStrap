namespace RiftStrap.Features.ServerBrowser.Models
{
    public class ServerInfo
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("maxPlayers")]
        public int MaxPlayers { get; set; }

        [JsonPropertyName("playing")]
        public int Playing { get; set; }

        [JsonPropertyName("playerTokens")]
        public List<string> PlayerTokens { get; set; } = new();

        [JsonPropertyName("fps")]
        public double Fps { get; set; }

        [JsonPropertyName("ping")]
        public int Ping { get; set; }

        public string FillText => $"{Playing}/{MaxPlayers}";
        public double FillPercent => MaxPlayers > 0 ? (double)Playing / MaxPlayers * 100 : 0;
        public double FillFraction => MaxPlayers > 0 ? (double)Playing / MaxPlayers : 0;
    }

    public class ServerListResponse
    {
        [JsonPropertyName("previousPageCursor")]
        public string? PreviousPageCursor { get; set; }

        [JsonPropertyName("nextPageCursor")]
        public string? NextPageCursor { get; set; }

        [JsonPropertyName("data")]
        public List<ServerInfo> Data { get; set; } = new();
    }
}
