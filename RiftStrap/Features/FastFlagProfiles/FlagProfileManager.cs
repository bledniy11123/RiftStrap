namespace RiftStrap.Features.FastFlagProfiles
{

    public class FlagProfileManager
    {
        private static readonly string ProfilesDir = Path.Combine(Paths.Base, "FlagProfiles");

        public FlagProfileManager()
        {
            Directory.CreateDirectory(ProfilesDir);
        }

        public List<FlagProfile> GetProfiles()
        {
            var profiles = new List<FlagProfile>();

            foreach (var file in Directory.GetFiles(ProfilesDir, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var profile = JsonSerializer.Deserialize<FlagProfile>(json);
                    if (profile != null)
                        profiles.Add(profile);
                }
                catch { }
            }

            return profiles.OrderBy(p => p.Name).ToList();
        }

        public FlagProfile SaveCurrentAsProfile(string name, string description = "")
        {
            var profile = new FlagProfile
            {
                Name = name,
                Description = description,
                Flags = new Dictionary<string, object>(App.FastFlags.Prop),
                CreatedAt = DateTime.UtcNow,
            };

            try
            {
                var json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(Path.Combine(ProfilesDir, $"{profile.Id}.json"), json);
                App.Logger.WriteLine("FlagProfiles", $"Saved profile: {name} ({profile.Flags.Count} flags)");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("FlagProfiles", $"Failed to save profile {name}: {ex.Message}");
            }

            return profile;
        }

        public void LoadProfile(string profileId)
        {
            var path = Path.Combine(ProfilesDir, $"{profileId}.json");
            if (!File.Exists(path)) return;

            try
            {
                var json = File.ReadAllText(path);
                var profile = JsonSerializer.Deserialize<FlagProfile>(json);
                if (profile == null) return;

                foreach (var key in App.FastFlags.Prop.Keys.ToList())
                    App.FastFlags.SetValue(key, null);

                foreach (var (key, value) in profile.Flags)
                    App.FastFlags.SetValue(key, value);

                App.FastFlags.Save();
                App.Logger.WriteLine("FlagProfiles", $"Loaded profile: {profile.Name}");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("FlagProfiles", $"Failed to load profile {profileId}: {ex.Message}");
            }
        }

        public void DeleteProfile(string profileId)
        {
            try
            {
                var path = Path.Combine(ProfilesDir, $"{profileId}.json");
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("FlagProfiles", $"Failed to delete profile {profileId}: {ex.Message}");
            }
        }

        public string? ExportProfile(string profileId, string outputDir)
        {
            try
            {
                var path = Path.Combine(ProfilesDir, $"{profileId}.json");
                if (!File.Exists(path)) return null;

                var json = File.ReadAllText(path);
                var profile = JsonSerializer.Deserialize<FlagProfile>(json);
                if (profile == null) return null;

                var outPath = Path.Combine(outputDir, $"{profile.Name.Replace(" ", "_")}_flags.json");
                File.WriteAllText(outPath, json);
                return outPath;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("FlagProfiles", $"Failed to export profile {profileId}: {ex.Message}");
                return null;
            }
        }

        public FlagProfile? ImportProfile(string filePath)
        {
            try
            {
                var json = File.ReadAllText(filePath);
                var profile = JsonSerializer.Deserialize<FlagProfile>(json);
                if (profile == null) return null;

                profile.Id = Guid.NewGuid().ToString("N")[..8];
                var outJson = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(Path.Combine(ProfilesDir, $"{profile.Id}.json"), outJson);

                return profile;
            }
            catch
            {
                return null;
            }
        }
    }

    public class FlagProfile
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        [JsonPropertyName("flags")]
        public Dictionary<string, object> Flags { get; set; } = new();

        [JsonPropertyName("created")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string FlagCountText => $"{Flags.Count} flags";
    }
}
