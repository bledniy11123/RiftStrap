using System.Windows;
using System.Windows.Input;

using CommunityToolkit.Mvvm.Input;

using RiftStrap.Enums.FlagPresets;

namespace RiftStrap.UI.ViewModels.Settings
{
    public class FastFlagsViewModel : NotifyPropertyChangedViewModel
    {
        private Dictionary<string, object>? _preResetFlags;

        public event EventHandler? RequestPageReloadEvent;

        public event EventHandler? OpenFlagEditorEvent;

        private void OpenFastFlagEditor() => OpenFlagEditorEvent?.Invoke(this, EventArgs.Empty);

        public ICommand OpenFastFlagEditorCommand => new RelayCommand(OpenFastFlagEditor);

        public Visibility CanShowFastFlagEditor => App.IsStudioInstalled ? Visibility.Visible : Visibility.Collapsed;

        public bool UseFastFlagManager
        {
            get => App.Settings.Prop.UseFastFlagManager;
            set => App.Settings.Prop.UseFastFlagManager = value;
        }

        public IReadOnlyDictionary<MSAAMode, string?> MSAALevels => FastFlagManager.MSAAModes;

        public MSAAMode SelectedMSAALevel
        {
            get => MSAALevels.FirstOrDefault(x => x.Value == App.FastFlags.GetPreset("Rendering.MSAA")).Key;
            set => App.FastFlags.SetPreset("Rendering.MSAA", MSAALevels[value]);
        }

        public IReadOnlyDictionary<RenderingMode, string> RenderingModes => FastFlagManager.RenderingModes;

        public RenderingMode SelectedRenderingMode
        {
            get => App.FastFlags.GetPresetEnum(RenderingModes, "Rendering.Mode", "True");
            set => App.FastFlags.SetPresetEnum("Rendering.Mode", RenderingModes[value], "True");
        }

        public bool FixDisplayScaling
        {
            get => App.FastFlags.GetPreset("Rendering.DisableScaling") == "True";
            set => App.FastFlags.SetPreset("Rendering.DisableScaling", value ? "True" : null);
        }

        public IReadOnlyDictionary<TextureQuality, string?> TextureQualities => FastFlagManager.TextureQualityLevels;

        public TextureQuality SelectedTextureQuality
        {
            get => TextureQualities.Where(x => x.Value == App.FastFlags.GetPreset("Rendering.TextureQuality.Level")).FirstOrDefault().Key;
            set
            {
                if (value == TextureQuality.Default)
                {
                    App.FastFlags.SetPreset("Rendering.TextureQuality", null);
                }
                else
                {
                    App.FastFlags.SetPreset("Rendering.TextureQuality.OverrideEnabled", "True");
                    App.FastFlags.SetPreset("Rendering.TextureQuality.Level", TextureQualities[value]);
                }
            }
        }
        public bool ResetConfiguration
        {
            get => _preResetFlags is not null;

            set
            {
                const string LOG_IDENT = "FastFlagsViewModel::ResetConfiguration";

                // Use a dedicated suffix so this preview snapshot never clashes with the
                // corruption backup JsonManager.Load writes to "<FileLocation>.bak".
                string backupLocation = App.FastFlags.FileLocation + ".reset.bak";

                if (value)
                {
                    // Snapshot the live flags, then persist that snapshot to disk BEFORE clearing
                    // the live singleton. If a Save() runs while the preview is active it would
                    // otherwise write an empty config, and a navigation/crash would drop the only
                    // in-memory copy — the on-disk snapshot lets the flags always be recovered.
                    _preResetFlags = new(App.FastFlags.Prop);

                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(backupLocation)!);
                        string contents = JsonSerializer.Serialize(_preResetFlags, new JsonSerializerOptions { WriteIndented = true });
                        File.WriteAllText(backupLocation, contents);
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteException(LOG_IDENT, ex);
                    }

                    App.FastFlags.Prop.Clear();
                }
                else
                {
                    // Restore from the in-memory snapshot when present; fall back to the on-disk
                    // snapshot (survives navigation/crash) and finally to an empty config.
                    Dictionary<string, object>? restored = _preResetFlags;

                    if (restored is null)
                    {
                        try
                        {
                            if (File.Exists(backupLocation))
                                restored = JsonSerializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(backupLocation));
                        }
                        catch (Exception ex)
                        {
                            App.Logger.WriteException(LOG_IDENT, ex);
                        }
                    }

                    App.FastFlags.Prop = restored ?? new();
                    _preResetFlags = null;

                    try
                    {
                        if (File.Exists(backupLocation))
                            File.Delete(backupLocation);
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteException(LOG_IDENT, ex);
                    }
                }

                RequestPageReloadEvent?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
