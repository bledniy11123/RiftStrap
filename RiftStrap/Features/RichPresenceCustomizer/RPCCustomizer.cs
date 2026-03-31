namespace RiftStrap.Features.RichPresenceCustomizer
{

    public class RPCCustomizer
    {
        private static readonly string ConfigFile = Path.Combine(Paths.Base, "RPCConfig.json");

        public RPCConfig Config { get; private set; } = new();

        public RPCCustomizer()
        {
            Load();
        }

        public string GetStateText(string gameName, TimeSpan playTime, string? serverRegion = null)
        {
            var template = Config.StateTemplate;

            template = template.Replace("{game}", gameName);
            template = template.Replace("{time}", FormatPlayTime(playTime));
            template = template.Replace("{region}", serverRegion ?? "Unknown");
            template = template.Replace("{fps_target}", Features.FPSUnlocker.SmartFPSService.GetCurrentTarget().ToString());

            return template;
        }

        public string GetDetailsText(string gameName, int playerCount = 0)
        {
            var template = Config.DetailsTemplate;

            template = template.Replace("{game}", gameName);
            template = template.Replace("{players}", playerCount.ToString());
            template = template.Replace("{launcher}", "RiftStrap");

            return template;
        }

        private static string FormatPlayTime(TimeSpan ts)
        {
            if (ts.TotalHours >= 1)
                return $"{ts.TotalHours:F1}h";
            return $"{ts.TotalMinutes:F0}m";
        }

        public void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(Config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigFile, json);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("RPCCustomizer", $"Failed to save config: {ex.Message}");
            }
        }

        private void Load()
        {
            if (!File.Exists(ConfigFile)) return;
            try
            {
                Config = JsonSerializer.Deserialize<RPCConfig>(File.ReadAllText(ConfigFile)) ?? new();
            }
            catch { Config = new(); }
        }
    }

    public class RPCConfig
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonPropertyName("show_server_region")]
        public bool ShowServerRegion { get; set; } = true;

        [JsonPropertyName("show_play_time")]
        public bool ShowPlayTime { get; set; } = true;

        [JsonPropertyName("show_fps_target")]
        public bool ShowFpsTarget { get; set; } = false;

        [JsonPropertyName("state_template")]
        public string StateTemplate { get; set; } = "Playing {game}";

        [JsonPropertyName("details_template")]
        public string DetailsTemplate { get; set; } = "via RiftStrap";

        [JsonPropertyName("large_image_text")]
        public string LargeImageText { get; set; } = "RiftStrap — Your Roblox, elevated";
    }
}
