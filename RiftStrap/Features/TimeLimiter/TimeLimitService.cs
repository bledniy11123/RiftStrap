namespace RiftStrap.Features.TimeLimiter
{

    public class TimeLimitService : IDisposable
    {
        private static readonly string ConfigFile = Path.Combine(Paths.Base, "TimeLimit.json");

        private CancellationTokenSource? _cts;
        private DateTime _sessionStart;
        private bool _limitReached;

        public TimeLimitConfig Config { get; private set; } = new();
        public bool IsTracking => _cts != null;
        public TimeSpan CurrentSessionTime => DateTime.UtcNow - _sessionStart;

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
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            var duration = CurrentSessionTime;

            var today = DateTime.UtcNow.Date.ToString("yyyy-MM-dd");
            Config.DailyMinutes.TryGetValue(today, out var existing);
            Config.DailyMinutes[today] = existing + (int)duration.TotalMinutes;

            var cutoff = DateTime.UtcNow.AddDays(-30).Date.ToString("yyyy-MM-dd");
            foreach (var key in Config.DailyMinutes.Keys.Where(k => string.Compare(k, cutoff) < 0).ToList())
                Config.DailyMinutes.Remove(key);

            Save();
            App.Logger.WriteLine("TimeLimiter", $"Session ended: {duration.TotalMinutes:F0} minutes");
        }

        public TimeSpan GetTodayPlayTime()
        {
            var today = DateTime.UtcNow.Date.ToString("yyyy-MM-dd");
            Config.DailyMinutes.TryGetValue(today, out var minutes);

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
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var sessionMinutes = (int)CurrentSessionTime.TotalMinutes;

                    if (Config.BreakReminderMinutes > 0 && sessionMinutes > 0 && sessionMinutes % Config.BreakReminderMinutes == 0)
                    {
                        OnReminder?.Invoke($"You've been playing for {sessionMinutes} minutes. Take a break!");
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
            try { Config = JsonSerializer.Deserialize<TimeLimitConfig>(File.ReadAllText(ConfigFile)) ?? new(); }
            catch { Config = new(); }
        }

        private void Save()
        {
            try
            {
                File.WriteAllText(ConfigFile, JsonSerializer.Serialize(Config, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("TimeLimiter", $"Failed to save config: {ex.Message}");
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
