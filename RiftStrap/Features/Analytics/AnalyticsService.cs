namespace RiftStrap.Features.Analytics
{

    public class AnalyticsService
    {
        private static readonly string DataFile = Path.Combine(Paths.Base, "Analytics.json");
        private AnalyticsData _data = new();
        private DateTime? _sessionStart;
        private long _currentPlaceId;
        private string _currentGameName = "";

        public IReadOnlyList<PlaySession> Sessions => _data.Sessions;

        public AnalyticsService() => Load();

        public void StartSession(long placeId, string gameName)
        {
            _sessionStart = DateTime.UtcNow;
            _currentPlaceId = placeId;
            _currentGameName = gameName;
        }

        public void EndSession()
        {
            if (_sessionStart == null) return;

            var session = new PlaySession
            {
                PlaceId = _currentPlaceId,
                GameName = _currentGameName,
                StartTime = _sessionStart.Value,
                EndTime = DateTime.UtcNow,
            };

            _data.Sessions.Insert(0, session);
            if (_data.Sessions.Count > 500) _data.Sessions.RemoveAt(500);

            if (!_data.GameTotals.ContainsKey(_currentPlaceId))
                _data.GameTotals[_currentPlaceId] = new GameTotal { PlaceId = _currentPlaceId, Name = _currentGameName };

            _data.GameTotals[_currentPlaceId].TotalMinutes += session.DurationMinutes;
            _data.GameTotals[_currentPlaceId].SessionCount++;
            _data.GameTotals[_currentPlaceId].LastPlayed = session.EndTime;

            _sessionStart = null;
            Save();
        }

        public TimeSpan TotalPlayTime => TimeSpan.FromMinutes(_data.Sessions.Sum(s => s.DurationMinutes));

        public TimeSpan TodayPlayTime
        {
            get
            {
                var today = DateTime.UtcNow.Date;
                return TimeSpan.FromMinutes(_data.Sessions
                    .Where(s => s.StartTime.Date == today)
                    .Sum(s => s.DurationMinutes));
            }
        }

        public TimeSpan WeekPlayTime
        {
            get
            {
                var weekAgo = DateTime.UtcNow.AddDays(-7);
                return TimeSpan.FromMinutes(_data.Sessions
                    .Where(s => s.StartTime > weekAgo)
                    .Sum(s => s.DurationMinutes));
            }
        }

        public List<DailyPlayTime> GetDailyBreakdown(int days = 14)
        {
            var result = new List<DailyPlayTime>();
            for (int i = days - 1; i >= 0; i--)
            {
                var date = DateTime.UtcNow.Date.AddDays(-i);
                var minutes = _data.Sessions
                    .Where(s => s.StartTime.Date == date)
                    .Sum(s => s.DurationMinutes);
                result.Add(new DailyPlayTime { Date = date, Minutes = minutes });
            }

            var max = result.Count > 0 ? result.Max(d => d.Minutes) : 0;
            foreach (var d in result)
                d.MaxMinutes = max;

            return result;
        }

        public List<GameTotal> GetTopGames(int limit = 10)
            => _data.GameTotals.Values
                .OrderByDescending(g => g.TotalMinutes)
                .Take(limit)
                .ToList();

        public int UniqueGamesCount => _data.GameTotals.Count;

        private void Load()
        {
            if (!File.Exists(DataFile)) return;
            try { _data = JsonSerializer.Deserialize<AnalyticsData>(File.ReadAllText(DataFile)) ?? new(); }
            catch { _data = new(); }
        }

        private void Save()
        {
            try
            {
                File.WriteAllText(DataFile, JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("Analytics", $"Failed to save analytics: {ex.Message}");
            }
        }
    }

    public class AnalyticsData
    {
        [JsonPropertyName("sessions")]
        public List<PlaySession> Sessions { get; set; } = new();

        [JsonPropertyName("game_totals")]
        public Dictionary<long, GameTotal> GameTotals { get; set; } = new();
    }

    public class PlaySession
    {
        [JsonPropertyName("place_id")]
        public long PlaceId { get; set; }

        [JsonPropertyName("game_name")]
        public string GameName { get; set; } = "";

        [JsonPropertyName("start")]
        public DateTime StartTime { get; set; }

        [JsonPropertyName("end")]
        public DateTime EndTime { get; set; }

        public double DurationMinutes => (EndTime - StartTime).TotalMinutes;
        public string DurationText
        {
            get
            {
                var span = EndTime - StartTime;
                return span.TotalHours >= 1 ? $"{span.TotalHours:F1}h" : $"{span.TotalMinutes:F0}m";
            }
        }
        public string DateText => StartTime.ToLocalTime().ToString("MMM dd, HH:mm");
    }

    public class GameTotal
    {
        [JsonPropertyName("place_id")]
        public long PlaceId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("total_minutes")]
        public double TotalMinutes { get; set; }

        [JsonPropertyName("session_count")]
        public int SessionCount { get; set; }

        [JsonPropertyName("last_played")]
        public DateTime LastPlayed { get; set; }

        public string TotalTimeText
        {
            get
            {
                if (TotalMinutes >= 60) return $"{TotalMinutes / 60:F1}h";
                return $"{TotalMinutes:F0}m";
            }
        }
    }

    public class DailyPlayTime
    {
        public DateTime Date { get; set; }
        public double Minutes { get; set; }
        public string DayLabel => Date.ToString("ddd");
        public string HoursText => $"{Minutes / 60:F1}h";

        // Set by GetDailyBreakdown so each bar sizes relative to the busiest day. The chart
        // uses two star-weighted columns (fill + remainder) so the bar length is proportional.
        public double MaxMinutes { get; set; }
        public System.Windows.GridLength BarFillStar =>
            new(MaxMinutes > 0 ? Minutes / MaxMinutes : 0, System.Windows.GridUnitType.Star);
        public System.Windows.GridLength BarRestStar =>
            new(MaxMinutes > 0 ? Math.Max(1 - Minutes / MaxMinutes, 0) : 1, System.Windows.GridUnitType.Star);
    }
}
