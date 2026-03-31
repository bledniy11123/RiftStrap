namespace RiftStrap.Features.Notifications
{

    public class NotificationService : IDisposable
    {
        private CancellationTokenSource? _cts;

        public bool FriendAlertsEnabled { get; set; } = true;
        public bool GameUpdateAlertsEnabled { get; set; } = true;
        public int PollIntervalSeconds { get; set; } = 60;

        private readonly Dictionary<long, string> _friendStates = new();
        private readonly HashSet<long> _watchedGames = new();

        public event Action<string, string>? OnNotification;

        public void Start()
        {
            Stop();
            _cts = new CancellationTokenSource();
            Task.Run(() => PollLoop(_cts.Token));
            App.Logger.WriteLine("NotificationService", "Started");
        }

        public void Stop()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        public void TrackFriend(long userId) => _friendStates.TryAdd(userId, "Offline");

        public void WatchGame(long universeId) => _watchedGames.Add(universeId);

        public void Dispose()
        {
            Stop();
            GC.SuppressFinalize(this);
        }

        private async Task PollLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    if (FriendAlertsEnabled && _friendStates.Count > 0)
                        await CheckFriendsAsync();

                    if (GameUpdateAlertsEnabled && _watchedGames.Count > 0)
                        await CheckGameUpdatesAsync();
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine("NotificationService", $"Poll error: {ex.Message}");
                }

                try { await Task.Delay(PollIntervalSeconds * 1000, ct); }
                catch (TaskCanceledException) { break; }
            }
        }

        private async Task CheckFriendsAsync()
        {
            if (_friendStates.Count == 0) return;

            var userIds = string.Join(",", _friendStates.Keys);

            try
            {
                var json = await App.HttpClient.GetStringAsync(
                    $"https://presence.roblox.com/v1/presence/users?userIds={userIds}");
                var data = JsonSerializer.Deserialize<JsonElement>(json);

                if (!data.TryGetProperty("userPresences", out var presences)) return;

                foreach (var presence in presences.EnumerateArray())
                {
                    var userId = presence.GetProperty("userId").GetInt64();
                    var userPresenceType = presence.GetProperty("userPresenceType").GetInt32();
                    var newState = userPresenceType switch
                    {
                        0 => "Offline",
                        1 => "Online",
                        2 => "InGame",
                        3 => "InStudio",
                        _ => "Unknown"
                    };

                    if (_friendStates.TryGetValue(userId, out var oldState) && oldState != newState)
                    {
                        _friendStates[userId] = newState;

                        if (oldState == "Offline" && newState is "Online" or "InGame")
                        {
                            var displayName = presence.TryGetProperty("lastLocation", out var loc)
                                ? loc.GetString() ?? ""
                                : "";
                            OnNotification?.Invoke("Friend Online", $"User {userId} is now {newState.ToLower()}" +
                                (string.IsNullOrEmpty(displayName) ? "" : $" — {displayName}"));
                        }
                    }
                    else
                    {
                        _friendStates[userId] = newState;
                    }
                }
            }
            catch { }
        }

        private async Task CheckGameUpdatesAsync()
        {

            var ids = string.Join(",", _watchedGames);

            try
            {
                var json = await App.HttpClient.GetStringAsync(
                    $"https://games.roblox.com/v1/games?universeIds={ids}");
                var data = JsonSerializer.Deserialize<JsonElement>(json);

                if (!data.TryGetProperty("data", out var games)) return;

                foreach (var game in games.EnumerateArray())
                {
                    var name = game.GetProperty("name").GetString() ?? "";
                    var playing = game.GetProperty("playing").GetInt32();

                    if (playing > 10000)
                    {
                        OnNotification?.Invoke("Game Trending", $"{name} has {playing:N0} players right now!");
                    }
                }
            }
            catch { }
        }
    }
}
