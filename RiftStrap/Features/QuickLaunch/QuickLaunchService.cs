namespace RiftStrap.Features.QuickLaunch
{

    public class QuickLaunchService
    {
        private static readonly string DataFile = Path.Combine(Paths.Base, "QuickLaunch.json");
        private QuickLaunchData _data = new();

        public IReadOnlyList<SavedGame> Favorites => _data.Favorites;
        public IReadOnlyList<SavedGame> RecentGames => _data.Recent;

        public QuickLaunchService() => Load();

        public async Task<List<SearchResult>> SearchGamesAsync(string query, int limit = 12)
        {
            try
            {
                var url = $"https://games.roblox.com/v1/games/list?model.keyword={Uri.EscapeDataString(query)}&model.maxRows={limit}&model.sortToken=";
                var json = await App.HttpClient.GetStringAsync(url);
                var data = JsonSerializer.Deserialize<JsonElement>(json);

                var results = new List<SearchResult>();
                if (!data.TryGetProperty("games", out var games)) return results;

                foreach (var game in games.EnumerateArray())
                {
                    if (!game.TryGetProperty("universeId", out var universeId) ||
                        !game.TryGetProperty("placeId", out var placeId) ||
                        !game.TryGetProperty("name", out var name))
                        continue;

                    results.Add(new SearchResult
                    {
                        UniverseId = universeId.GetInt64(),
                        PlaceId = placeId.GetInt64(),
                        Name = name.GetString() ?? "",
                        CreatorName = game.TryGetProperty("creatorName", out var cn) ? cn.GetString() ?? "" : "",
                        Playing = game.TryGetProperty("playerCount", out var pc) ? pc.GetInt32() : 0,
                        TotalUpVotes = game.TryGetProperty("totalUpVotes", out var uv) ? uv.GetInt64() : 0,
                        TotalDownVotes = game.TryGetProperty("totalDownVotes", out var dv) ? dv.GetInt64() : 0,
                    });
                }

                if (results.Count > 0)
                {
                    var ids = string.Join(",", results.Select(r => r.UniverseId));
                    try
                    {
                        var thumbJson = await App.HttpClient.GetStringAsync(
                            $"https://thumbnails.roblox.com/v1/games/icons?universeIds={ids}&returnPolicy=PlaceHolder&size=150x150&format=Png");
                        var thumbData = JsonSerializer.Deserialize<JsonElement>(thumbJson);
                        if (thumbData.TryGetProperty("data", out var thumbs))
                        {
                            foreach (var thumb in thumbs.EnumerateArray())
                            {
                                var uid = thumb.GetProperty("targetId").GetInt64();
                                var url2 = thumb.GetProperty("imageUrl").GetString();
                                var match = results.FirstOrDefault(r => r.UniverseId == uid);
                                if (match != null) match.ThumbnailUrl = url2;
                            }
                        }
                    }
                    catch { }
                }

                return results;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("QuickLaunch", $"Search failed: {ex.Message}");
                return new();
            }
        }

        public void LaunchGame(long placeId, string? serverId = null)
        {
            var url = serverId != null
                ? $"roblox://experiences/start?placeId={placeId}&gameInstanceId={serverId}"
                : $"roblox://experiences/start?placeId={placeId}";

            Utilities.ShellExecute(url);
            App.Logger.WriteLine("QuickLaunch", $"Launching PlaceId {placeId}");
        }

        public void AddFavorite(SavedGame game)
        {
            if (_data.Favorites.Any(f => f.PlaceId == game.PlaceId)) return;
            _data.Favorites.Insert(0, game);
            if (_data.Favorites.Count > 50) _data.Favorites.RemoveAt(50);
            Save();
        }

        public void RemoveFavorite(long placeId)
        {
            _data.Favorites.RemoveAll(f => f.PlaceId == placeId);
            Save();
        }

        public void TrackPlayed(SavedGame game)
        {
            _data.Recent.RemoveAll(r => r.PlaceId == game.PlaceId);
            game.LastPlayed = DateTime.UtcNow;
            _data.Recent.Insert(0, game);
            if (_data.Recent.Count > 30) _data.Recent.RemoveAt(30);
            Save();
        }

        public bool IsFavorite(long placeId) => _data.Favorites.Any(f => f.PlaceId == placeId);

        private void Load()
        {
            if (!File.Exists(DataFile)) return;
            try
            {
                _data = JsonSerializer.Deserialize<QuickLaunchData>(File.ReadAllText(DataFile)) ?? new();
            }
            catch
            {
                try
                {
                    var backup = DataFile + ".bak";
                    if (File.Exists(backup))
                        _data = JsonSerializer.Deserialize<QuickLaunchData>(File.ReadAllText(backup)) ?? new();
                    else
                        _data = new();
                }
                catch { _data = new(); }
            }
        }

        private void Save()
        {
            try
            {
                var tmp = DataFile + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true }));

                if (File.Exists(DataFile))
                    File.Replace(tmp, DataFile, DataFile + ".bak");
                else
                    File.Move(tmp, DataFile);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("QuickLaunch", $"Failed to save data: {ex.Message}");
            }
        }
    }

    public class QuickLaunchData
    {
        [JsonPropertyName("favorites")]
        public List<SavedGame> Favorites { get; set; } = new();

        [JsonPropertyName("recent")]
        public List<SavedGame> Recent { get; set; } = new();
    }

    public class SavedGame
    {
        [JsonPropertyName("place_id")]
        public long PlaceId { get; set; }

        [JsonPropertyName("universe_id")]
        public long UniverseId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("creator")]
        public string CreatorName { get; set; } = "";

        [JsonPropertyName("thumbnail")]
        public string? ThumbnailUrl { get; set; }

        [JsonPropertyName("last_played")]
        public DateTime? LastPlayed { get; set; }
    }

    public class SearchResult
    {
        public long UniverseId { get; set; }
        public long PlaceId { get; set; }
        public string Name { get; set; } = "";
        public string CreatorName { get; set; } = "";
        public int Playing { get; set; }
        public long TotalUpVotes { get; set; }
        public long TotalDownVotes { get; set; }
        public string? ThumbnailUrl { get; set; }

        public string PlayingText => Playing > 1000
            ? (Playing / 1000.0).ToString("F1", CultureInfo.InvariantCulture) + "k"
            : Playing.ToString(CultureInfo.InvariantCulture);
        public double LikePercent => TotalUpVotes + TotalDownVotes > 0
            ? (double)TotalUpVotes / (TotalUpVotes + TotalDownVotes) * 100 : 0;
    }
}
