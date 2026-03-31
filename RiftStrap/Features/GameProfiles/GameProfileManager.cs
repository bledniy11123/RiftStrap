using RiftStrap.Features.GameProfiles.Models;
using RiftStrap.Features.InGameUI;

namespace RiftStrap.Features.GameProfiles
{

    public class GameProfileManager
    {
        private static readonly string ProfilesFile = Path.Combine(Paths.Base, "GameProfiles.json");

        private GameProfilesStore _store = new();
        private GameProfile? _activeProfile;
        private Dictionary<string, object>? _originalFastFlags;

        public GameProfile? ActiveProfile => _activeProfile;
        public IReadOnlyDictionary<long, GameProfile> Profiles => _store.Profiles;
        public bool AutoDetectEnabled
        {
            get => _store.AutoDetectEnabled;
            set { _store.AutoDetectEnabled = value; Save(); }
        }

        public GameProfileManager()
        {
            Load();
        }

        public void OnGameJoin(long placeId, long universeId)
        {
            if (!_store.AutoDetectEnabled)
                return;

            if (!_store.Profiles.TryGetValue(placeId, out var profile))
                return;

            if (!profile.Enabled)
                return;

            ApplyProfile(profile);
            profile.LastPlayed = DateTime.UtcNow;
            Save();

            App.Logger.WriteLine("GameProfileManager", $"Applied profile for {profile.Name} (PlaceId: {placeId})");
        }

        public void OnGameLeave()
        {
            RestoreOriginal();
            App.Logger.WriteLine("GameProfileManager", "Restored original settings");
        }

        public void SetProfile(GameProfile profile)
        {
            _store.Profiles[profile.PlaceId] = profile;
            Save();
        }

        public void RemoveProfile(long placeId)
        {
            _store.Profiles.Remove(placeId);
            Save();
        }

        public GameProfile? GetProfile(long placeId)
        {
            _store.Profiles.TryGetValue(placeId, out var profile);
            return profile;
        }

        private void ApplyProfile(GameProfile profile)
        {

            _originalFastFlags = new Dictionary<string, object>(App.FastFlags.Prop);

            foreach (var (key, value) in profile.FastFlags)
            {
                App.FastFlags.SetValue(key, value);
            }

            App.FastFlags.Save();

            if (!string.IsNullOrEmpty(profile.ThemeId))
            {
                var themeEngine = new ThemeEngine();
                themeEngine.ApplyTheme(profile.ThemeId);
            }

            _activeProfile = profile;
        }

        private void RestoreOriginal()
        {
            if (_originalFastFlags == null)
                return;

            foreach (var key in App.FastFlags.Prop.Keys.ToList())
            {
                if (_originalFastFlags.TryGetValue(key, out var value))
                    App.FastFlags.SetValue(key, value);
                else
                    App.FastFlags.SetValue(key, null);
            }

            foreach (var (key, value) in _originalFastFlags)
            {
                if (!App.FastFlags.Prop.ContainsKey(key))
                    App.FastFlags.SetValue(key, value);
            }

            App.FastFlags.Save();

            if (_activeProfile?.ThemeId != null)
            {
                var themeEngine = new ThemeEngine();
                themeEngine.RemoveActiveTheme();
            }

            _activeProfile = null;
            _originalFastFlags = null;
        }

        private void Load()
        {
            if (!File.Exists(ProfilesFile))
                return;

            try
            {
                var json = File.ReadAllText(ProfilesFile);
                _store = JsonSerializer.Deserialize<GameProfilesStore>(json) ?? new();
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("GameProfileManager", $"Failed to load profiles: {ex.Message}");
                _store = new();
            }
        }

        private void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(_store, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ProfilesFile, json);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("GameProfileManager", $"Failed to save profiles: {ex.Message}");
            }
        }
    }
}
