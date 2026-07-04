namespace RiftStrap.Features.TimeLimiter
{

    public class TimeLimitService : IDisposable
    {
        private static readonly string ConfigFile = Path.Combine(Paths.Base, "TimeLimit.json");

        private readonly object _sync = new();

        private CancellationTokenSource? _cts;
        private DateTime? _sessionStart;
        private bool _limitReached;

        public TimeLimitConfig Config { get; private set; } = new();
        public bool IsTracking => _cts != null;
        public TimeSpan CurrentSessionTime =>
            _sessionStart.HasValue ? DateTime.UtcNow - _sessionStart.Value : TimeSpan.Zero;

        public event Action<string>? OnReminder;
        public event Action? OnLimitReached;

        public TimeLimitService()
        {
            Load();
        }

        public void StartSession()
        {
            _sessionStart = DateTime.UtcNow;
            _limitReached = false;
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            if (Config.Enabled)
                Task.Run(() => MonitorLoop(_cts.Token));

            App.Logger.WriteLine("TimeLimiter", "Session started");
        }

        public void StopSession()
        {
            // Never-started (or already-stopped) session: nothing to record. Without this guard
            // a null/default _sessionStart produced a ~1-billion-minute bogus session.
            if (_cts == null)
                return;

            _cts.Cancel();
            _cts.Dispose();
            _cts = null;

            lock (_sync)
            {
                var minutes = (int)CurrentSessionTime.TotalMinutes;
                _sessionStart = null;

                // Reject implausible/negative durations (e.g. clock changes) before recording.
                if (minutes > 0 && minutes <= 24 * 60)
                {
                    var today = DateTime.UtcNow.Date.ToString("yyyy-MM-dd");
                    Config.DailyMinutes.TryGetValue(today, out var existing);
                    Config.DailyMinutes[today] = existing + minutes;

                    var cutoff = DateTime.UtcNow.AddDays(-30).Date.ToString("yyyy-MM-dd");
                    foreach (var key in Config.DailyMinutes.Keys.Where(k => string.Compare(k, cutoff) < 0).ToList())
                        Config.DailyMinutes.Remove(key);
                }

                Save();
                App.Logger.WriteLine("TimeLimiter", $"Session ended: {minutes} minutes");
            }
        }

        public TimeSpan GetTodayPlayTime()
        {
            var today = DateTime.UtcNow.Date.ToString("yyyy-MM-dd");

            int minutes;
            lock (_sync)
            {
                Config.DailyMinutes.TryGetValue(today, out minutes);
            }

            if (IsTracking)
                minutes += (int)CurrentSessionTime.TotalMinutes;

            return TimeSpan.FromMinutes(minutes);
        }

        public TimeSpan? GetRemainingTime()
        {
            if (!Config.Enabled || Config.DailyLimitMinutes <= 0)
                return null;

            var played = GetTodayPlayTime();
            var remaining = TimeSpan.FromMinutes(Config.DailyLimitMinutes) - played;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }

        private async Task MonitorLoop(CancellationToken ct)
        {
            // Track the next reminder threshold explicitly. The old modulo/equality test against
            // the drifting 60s loop clock could skip a minute (never firing) or double-fire.
            int lastReminderMinute = 0;

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var sessionMinutes = (int)CurrentSessionTime.TotalMinutes;

                    if (Config.BreakReminderMinutes > 0 && sessionMinutes >= lastReminderMinute + Config.BreakReminderMinutes)
                    {
                        lastReminderMinute += Config.BreakReminderMinutes;
                        OnReminder?.Invoke($"You've been playing for {lastReminderMinute} minutes. Take a break!");
                    }

                    if (Config.DailyLimitMinutes > 0 && !_limitReached)
                    {
                        var todayTotal = GetTodayPlayTime();
                        if (todayTotal.TotalMinutes >= Config.DailyLimitMinutes)
                        {
                            _limitReached = true;
                            OnLimitReached?.Invoke();
                            OnReminder?.Invoke($"Daily limit reached ({Config.DailyLimitMinutes} minutes). Consider stopping.");
                        }
                    }
                }
                catch { }

                try { await Task.Delay(60_000, ct); }
                catch (TaskCanceledException) { break; }
            }
        }

        private void Load()
        {
            if (!File.Exists(ConfigFile)) return;
            try
            {
                Config = JsonSerializer.Deserialize<TimeLimitConfig>(File.ReadAllText(ConfigFile)) ?? new();
            }
            catch (Exception ex)
            {
                // Preserve the corrupt file for diagnosis instead of silently discarding play history.
                App.Logger.WriteLine("TimeLimiter", $"Failed to parse config, backing up corrupt file: {ex.Message}");
                try { File.Copy(ConfigFile, ConfigFile + ".corrupt", true); }
                catch { }
                Config = new();
            }
        }

        private void Save()
        {
            lock (_sync)
            {
                try
                {
                    // Serialize cross-process access so concurrent instances don't clobber each other.
                    using var ipLock = new InterProcessLock("TimeLimit", TimeSpan.FromSeconds(5));

                    var json = JsonSerializer.Serialize(Config, new JsonSerializerOptions { WriteIndented = true });

                    // Write to a temp file then atomically swap, so a crash mid-write can't corrupt the file.
                    var tempFile = ConfigFile + ".tmp";
                    File.WriteAllText(tempFile, json);

                    if (File.Exists(ConfigFile))
                        File.Replace(tempFile, ConfigFile, null);
                    else
                        File.Move(tempFile, ConfigFile);
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine("TimeLimiter", $"Failed to save config: {ex.Message}");
                }
            }
        }

        public void Dispose()
        {
            StopSession();
            GC.SuppressFinalize(this);
        }
    }

    public class TimeLimitConfig
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = false;

        [JsonPropertyName("daily_limit_minutes")]
        public int DailyLimitMinutes { get; set; } = 0;

        [JsonPropertyName("break_reminder_minutes")]
        public int BreakReminderMinutes { get; set; } = 60;

        [JsonPropertyName("daily_minutes")]
        public Dictionary<string, int> DailyMinutes { get; set; } = new();
    }
}
