using RiftStrap.Features.GameProfiles.Models;
using RiftStrap.Features.InGameUI;

namespace RiftStrap.Features.GameProfiles
{

    public class GameProfileManager
    {
        private static readonly string ProfilesFile = Path.Combine(Paths.Base, "GameProfiles.json");

        // On-disk snapshot of the pre-profile FastFlags. Its presence doubles as the
        // "profile-applied" marker: the profile is applied in the bootstrapper process but
        // restored in the watcher process (and must survive an abnormal watcher exit), so the
        // baseline cannot live only in memory.
        private static readonly string BaselineFile = Path.Combine(Paths.Base, "GameProfileBaseline.json");

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
            // Roll back any FastFlags leaked by a previous profile-applied session that exited
            // abnormally (stale on-disk baseline) so a stale profile can't linger — even if this
            // join has no profile of its own.
            if (File.Exists(BaselineFile))
            {
                RestoreOriginal();
                App.Logger.WriteLine("GameProfileManager", "Rolled back stale profile FastFlags");
            }

            if (!_store.AutoDetectEnabled)
                return;

            if (!_store.Profiles.TryGetValue(placeId, out var profile))
                return;

            if (!profile.Enabled)
                return;

            // DESIGN LIMITATION: this fires from the join log AFTER the client has started and read
            // ClientAppSettings.json, so the profile's FastFlags only take effect on the NEXT launch
            // of this game (the in-game UI theme, applied by RiftStrap's own overlay, does take effect
            // live). Truly applying per-game FastFlags to the CURRENT session would require applying
            // them before launch keyed on the target PlaceId, which the bootstrapper launch path does
            // not currently know. Applying here also mutates the shared global config, so with multiple
            // concurrent instances a per-game profile can influence another instance's next launch.
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
            // Snapshot the pre-profile FastFlags and persist them to disk *before* mutating the
            // global config, so the restore (which runs in the watcher process and must survive
            // an abnormal exit) can roll back to the exact pre-profile state.
            _originalFastFlags = new Dictionary<string, object>(App.FastFlags.Prop);
            WriteBaseline(_originalFastFlags);

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
            // Prefer the on-disk baseline: the profile may have been applied in a different
            // process, so the in-memory snapshot can be null here.
            var baseline = ReadBaseline() ?? _originalFastFlags;

            if (baseline == null)
                return;

            foreach (var key in App.FastFlags.Prop.Keys.ToList())
            {
                if (baseline.TryGetValue(key, out var value))
                    App.FastFlags.SetValue(key, value);
                else
                    App.FastFlags.SetValue(key, null);
            }

            foreach (var (key, value) in baseline)
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

            DeleteBaseline();

            _activeProfile = null;
            _originalFastFlags = null;
        }

        private static void WriteBaseline(Dictionary<string, object> baseline)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(BaselineFile)!);
                var json = JsonSerializer.Serialize(baseline, new JsonSerializerOptions { WriteIndented = true });

                // atomic write so a crash mid-write cannot leave a truncated baseline marker
                var tmp = BaselineFile + ".tmp";
                File.WriteAllText(tmp, json);
                File.Move(tmp, BaselineFile, true);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("GameProfileManager", $"Failed to persist profile baseline: {ex.Message}");
            }
        }

        private static Dictionary<string, object>? ReadBaseline()
        {
            if (!File.Exists(BaselineFile))
                return null;

            try
            {
                var json = File.ReadAllText(BaselineFile);
                return JsonSerializer.Deserialize<Dictionary<string, object>>(json);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("GameProfileManager", $"Failed to read profile baseline: {ex.Message}");
                return null;
            }
        }

        private static void DeleteBaseline()
        {
            try
            {
                if (File.Exists(BaselineFile))
                    File.Delete(BaselineFile);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("GameProfileManager", $"Failed to delete profile baseline: {ex.Message}");
            }
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

                // Preserve the unreadable file for recovery instead of silently discarding every
                // saved profile by resetting to an empty store.
                try
                {
                    var backup = ProfilesFile + ".bak";
                    File.Move(ProfilesFile, backup, true);
                    App.Logger.WriteLine("GameProfileManager", $"Backed up unreadable profiles to {backup}");
                }
                catch (Exception moveEx)
                {
                    App.Logger.WriteLine("GameProfileManager", $"Failed to back up unreadable profiles: {moveEx.Message}");
                }

                _store = new();
            }
        }

        private void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ProfilesFile)!);
                var json = JsonSerializer.Serialize(_store, new JsonSerializerOptions { WriteIndented = true });

                // atomic write: a crash or concurrent write mid-save must not truncate/wipe the
                // profile store (mirrors JsonManager.Save)
                var tmp = ProfilesFile + ".tmp";
                File.WriteAllText(tmp, json);
                File.Move(tmp, ProfilesFile, true);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("GameProfileManager", $"Failed to save profiles: {ex.Message}");
            }
        }
    }
}
