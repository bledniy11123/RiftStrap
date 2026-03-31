using RiftStrap.Features.ServerBrowser.Models;

namespace RiftStrap.Features.ServerBrowser
{

    public class ServerBrowserService
    {
        private const string BaseUrl = "https://games.roblox.com/v1/games";

        public async Task<ServerListResponse?> GetServersAsync(long placeId, int limit = 25, string? cursor = null)
        {
            try
            {
                var url = $"{BaseUrl}/{placeId}/servers/Public?sortOrder=Asc&limit={limit}";
                if (!string.IsNullOrEmpty(cursor))
                    url += $"&cursor={cursor}";

                var response = await App.HttpClient.GetStringAsync(url);
                return JsonSerializer.Deserialize<ServerListResponse>(response);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("ServerBrowser", $"Failed to fetch servers: {ex.Message}");
                return null;
            }
        }

        public async Task<GameDetails?> GetGameDetailsAsync(long universeId)
        {
            try
            {
                var url = $"https://games.roblox.com/v1/games?universeIds={universeId}";
                var response = await App.HttpClient.GetStringAsync(url);
                var wrapper = JsonSerializer.Deserialize<GameDetailsWrapper>(response);
                return wrapper?.Data?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("ServerBrowser", $"Failed to fetch game details: {ex.Message}");
                return null;
            }
        }

        public static string BuildJoinUrl(long placeId, string serverId)
        {
            return $"roblox://experiences/start?placeId={placeId}&gameInstanceId={serverId}";
        }
    }

    public class GameDetails
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("rootPlaceId")]
        public long RootPlaceId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("playing")]
        public int Playing { get; set; }

        [JsonPropertyName("visits")]
        public long Visits { get; set; }

        [JsonPropertyName("maxPlayers")]
        public int MaxPlayers { get; set; }

        [JsonPropertyName("created")]
        public DateTime Created { get; set; }

        [JsonPropertyName("updated")]
        public DateTime Updated { get; set; }
    }

    public class GameDetailsWrapper
    {
        [JsonPropertyName("data")]
        public List<GameDetails>? Data { get; set; }
    }
}
