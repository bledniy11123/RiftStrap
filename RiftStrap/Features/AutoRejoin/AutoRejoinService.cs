namespace RiftStrap.Features.AutoRejoin
{

    public class AutoRejoinService
    {
        private static readonly string[] DisconnectPatterns =
        {
            "Time to disconnect replication data:",
            "Connection lost",
            "Disconnected",
        };

        private static readonly string[] KickPatterns =
        {
            "Kicked from server",
            "You were kicked",
        };

        public bool Enabled { get; set; } = false;
        public int MaxRetries { get; set; } = 3;
        public int RetryDelaySeconds { get; set; } = 5;
        public bool RejoinOnKick { get; set; } = false;

        private int _retryCount;
        private long _lastPlaceId;
        private string? _lastServerId;

        public event Action<string>? OnRejoinAttempt;
        public event Action<bool>? OnRejoinResult;

        public void SetCurrentGame(long placeId, string? serverId)
        {
            _lastPlaceId = placeId;
            _lastServerId = serverId;
            _retryCount = 0;
        }

        public async Task<bool> HandleDisconnectAsync(string logLine)
        {
            if (!Enabled || _lastPlaceId == 0)
                return false;

            bool isKick = KickPatterns.Any(p => logLine.Contains(p, StringComparison.OrdinalIgnoreCase));
            bool isDisconnect = DisconnectPatterns.Any(p => logLine.Contains(p, StringComparison.OrdinalIgnoreCase));

            if (!isDisconnect && !isKick) return false;
            if (isKick && !RejoinOnKick) return false;

            if (_retryCount >= MaxRetries)
            {
                App.Logger.WriteLine("AutoRejoin", $"Max retries ({MaxRetries}) reached, giving up");
                OnRejoinResult?.Invoke(false);
                return false;
            }

            _retryCount++;
            var reason = isKick ? "Kicked" : "Disconnected";
            App.Logger.WriteLine("AutoRejoin", $"{reason} — rejoining in {RetryDelaySeconds}s (attempt {_retryCount}/{MaxRetries})");
            OnRejoinAttempt?.Invoke($"{reason} — retry {_retryCount}/{MaxRetries}");

            await Task.Delay(RetryDelaySeconds * 1000);

            var url = _lastServerId != null
                ? $"roblox://experiences/start?placeId={_lastPlaceId}&gameInstanceId={_lastServerId}"
                : $"roblox://experiences/start?placeId={_lastPlaceId}";

            try
            {
                Utilities.ShellExecute(url);
                OnRejoinResult?.Invoke(true);
                return true;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("AutoRejoin", $"Rejoin failed: {ex.Message}");
                OnRejoinResult?.Invoke(false);
                return false;
            }
        }

        public void ResetRetries() => _retryCount = 0;

        public void Clear()
        {
            _lastPlaceId = 0;
            _lastServerId = null;
            _retryCount = 0;
        }
    }
}
