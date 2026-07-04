namespace RiftStrap.Features.AutoRejoin
{

    public class AutoRejoinService
    {
        private static readonly string[] DisconnectPatterns =
        {
            "Time to disconnect replication data:",
            "Connection lost",
            // bare "Disconnected" removed — it matched benign log lines and triggered false rejoins
        };

        private static readonly string[] KickPatterns =
        {
            "Kicked from server",
            "You were kicked",
        };

        // Markers for an *expected* departure. When one of these precedes the generic
        // replication-disconnect line, that disconnect is intentional (user left / teleport)
        // and must not be treated as an unexpected disconnect worth rejoining.
        private static readonly string[] CleanLeavePatterns =
        {
            "leaveUGCGameInternal",                            // user returned to the desktop app
            "GameJoinUtil::initiateTeleportToPlace",           // in-experience teleport
            "GameJoinUtil::initiateTeleportToReservedServer",  // teleport to a reserved server
        };

        public bool Enabled { get; set; } = false;
        public int MaxRetries { get; set; } = 3;
        public int RetryDelaySeconds { get; set; } = 5;
        public bool RejoinOnKick { get; set; } = false;

        private int _retryCount;
        private bool _isRejoining;
        private long _lastPlaceId;
        private string? _lastServerId;

        // Set when a clean-leave/teleport marker is seen so the replication-disconnect
        // line that follows it is suppressed instead of triggering a false rejoin.
        private bool _cleanLeavePending;

        // Tracks the in-flight rejoin delay so a fresh join can abort it.
        private CancellationTokenSource? _rejoinCts;

        public event Action<string>? OnRejoinAttempt;
        public event Action<bool>? OnRejoinResult;

        public void SetCurrentGame(long placeId, string? serverId)
        {
            // A fresh join supersedes any rejoin still waiting out its delay.
            _rejoinCts?.Cancel();
            _cleanLeavePending = false;
            _lastPlaceId = placeId;
            _lastServerId = serverId;
            _retryCount = 0;
        }

        public async Task<bool> HandleDisconnectAsync(string logLine)
        {
            // Observe intentional-leave / teleport markers first. The replication-disconnect
            // line that follows one of these is expected, so record it and bail — the
            // subsequent disconnect line will be suppressed below.
            if (CleanLeavePatterns.Any(p => logLine.Contains(p, StringComparison.OrdinalIgnoreCase)))
            {
                _cleanLeavePending = true;
                return false;
            }

            if (!Enabled || _lastPlaceId == 0 || _isRejoining)
                return false;

            bool isKick = KickPatterns.Any(p => logLine.Contains(p, StringComparison.OrdinalIgnoreCase));
            bool isDisconnect = DisconnectPatterns.Any(p => logLine.Contains(p, StringComparison.OrdinalIgnoreCase));

            if (!isDisconnect && !isKick) return false;
            if (isKick && !RejoinOnKick) return false;

            // A clean leave / teleport was just observed: this replication disconnect is
            // expected, so consume the marker and do not rejoin. Kicks are keyed off their
            // own distinct pattern and are never suppressed this way.
            if (isDisconnect && !isKick && _cleanLeavePending)
            {
                _cleanLeavePending = false;
                return false;
            }

            if (_retryCount >= MaxRetries)
            {
                App.Logger.WriteLine("AutoRejoin", $"Max retries ({MaxRetries}) reached, giving up");
                OnRejoinResult?.Invoke(false);
                return false;
            }

            // Snapshot the session we are rejoining now. OnGameLeave fires on this same
            // disconnect and clears the live fields before the delay elapses, so the rejoin
            // must rely on the snapshot rather than _lastPlaceId/_lastServerId afterwards.
            long placeId = _lastPlaceId;
            string? serverId = _lastServerId;

            var cts = new CancellationTokenSource();
            _rejoinCts = cts;

            _isRejoining = true;   // block re-entrant rejoins racing on _retryCount until this one finishes
            try
            {
                _retryCount++;
                var reason = isKick ? "Kicked" : "Disconnected";
                App.Logger.WriteLine("AutoRejoin", $"{reason} — rejoining in {RetryDelaySeconds}s (attempt {_retryCount}/{MaxRetries})");
                OnRejoinAttempt?.Invoke($"{reason} — retry {_retryCount}/{MaxRetries}");

                try
                {
                    await Task.Delay(RetryDelaySeconds * 1000, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    App.Logger.WriteLine("AutoRejoin", "Rejoin cancelled — a new session started before the retry elapsed");
                    return false;
                }

                // Re-validate before acting: the feature may have been switched off while we
                // waited, and the snapshot must still identify a real place to rejoin.
                if (!Enabled || placeId == 0)
                {
                    App.Logger.WriteLine("AutoRejoin", "Rejoin no longer applicable after delay, skipping");
                    return false;
                }

                var url = serverId != null
                    ? $"roblox://experiences/start?placeId={placeId}&gameInstanceId={serverId}"
                    : $"roblox://experiences/start?placeId={placeId}";

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
            finally
            {
                _isRejoining = false;
                if (ReferenceEquals(_rejoinCts, cts))
                    _rejoinCts = null;
                cts.Dispose();
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
