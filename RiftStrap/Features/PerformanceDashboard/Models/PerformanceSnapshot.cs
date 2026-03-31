namespace RiftStrap.Features.PerformanceDashboard.Models
{
    public class PerformanceSnapshot
    {
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public double Fps { get; set; }
        public double CpuPercent { get; set; }
        public long RamBytes { get; set; }
        public double RamMB => RamBytes / (1024.0 * 1024);
        public int PingMs { get; set; }
        public double GpuPercent { get; set; }
    }

    public class PerformanceSession
    {
        [JsonPropertyName("place_id")]
        public long PlaceId { get; set; }

        [JsonPropertyName("game_name")]
        public string GameName { get; set; } = "";

        [JsonPropertyName("start")]
        public DateTime StartTime { get; set; }

        [JsonPropertyName("end")]
        public DateTime? EndTime { get; set; }

        [JsonPropertyName("avg_fps")]
        public double AvgFps { get; set; }

        [JsonPropertyName("avg_ping")]
        public int AvgPing { get; set; }

        [JsonPropertyName("peak_ram_mb")]
        public double PeakRamMB { get; set; }
    }
}
