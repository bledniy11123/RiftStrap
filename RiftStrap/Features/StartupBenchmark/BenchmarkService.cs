namespace RiftStrap.Features.StartupBenchmark
{

    public class BenchmarkService
    {
        private static readonly string DataFile = Path.Combine(Paths.Base, "StartupBenchmarks.json");

        private Stopwatch? _timer;
        private BenchmarkData _data = new();

        public BenchmarkService()
        {
            Load();
        }

        public void StartTiming()
        {
            _timer = Stopwatch.StartNew();
        }

        public void RecordPhase(string phaseName)
        {
            if (_timer == null) return;

            App.Logger.WriteLine("Benchmark", $"Phase '{phaseName}' at {_timer.ElapsedMilliseconds}ms");
        }

        public void StopTiming(string? notes = null)
        {
            if (_timer == null) return;
            _timer.Stop();

            var result = new BenchmarkResult
            {
                Timestamp = DateTime.UtcNow,
                TotalMs = _timer.ElapsedMilliseconds,
                Notes = notes ?? "",
            };

            _data.Results.Insert(0, result);
            if (_data.Results.Count > 50)
                _data.Results.RemoveAt(50);

            Save();
            App.Logger.WriteLine("Benchmark", $"Startup took {result.TotalMs}ms ({result.TotalSeconds:F1}s)");

            _timer = null;
        }

        public double GetAverageMs()
        {
            return _data.Results.Count > 0 ? _data.Results.Average(r => r.TotalMs) : 0;
        }

        public long GetFastestMs()
        {
            return _data.Results.Count > 0 ? _data.Results.Min(r => r.TotalMs) : 0;
        }

        public List<BenchmarkResult> GetResults(int limit = 10)
        {
            return _data.Results.Take(limit).ToList();
        }

        public string GetComparison()
        {
            if (_timer == null || _data.Results.Count < 2) return "";
            var avg = GetAverageMs();
            var current = _timer.ElapsedMilliseconds;
            var diff = current - avg;

            if (Math.Abs(diff) < 500) return "About average";
            return diff > 0 ? $"{diff / 1000.0:F1}s slower than average" : $"{Math.Abs(diff) / 1000.0:F1}s faster than average";
        }

        private void Load()
        {
            if (!File.Exists(DataFile)) return;
            try { _data = JsonSerializer.Deserialize<BenchmarkData>(File.ReadAllText(DataFile)) ?? new(); }
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
                App.Logger.WriteLine("Benchmark", $"Failed to save data: {ex.Message}");
            }
        }
    }

    public class BenchmarkData
    {
        [JsonPropertyName("results")]
        public List<BenchmarkResult> Results { get; set; } = new();
    }

    public class BenchmarkResult
    {
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }

        [JsonPropertyName("total_ms")]
        public long TotalMs { get; set; }

        [JsonPropertyName("notes")]
        public string Notes { get; set; } = "";

        public double TotalSeconds => TotalMs / 1000.0;
        public string DisplayText => $"{TotalSeconds:F1}s";
        public string DateText => Timestamp.ToLocalTime().ToString("dd MMM HH:mm");
    }
}
