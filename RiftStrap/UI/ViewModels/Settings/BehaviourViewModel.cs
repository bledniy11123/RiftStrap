using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Windows.Input;

using CommunityToolkit.Mvvm.Input;

using RiftStrap.Extensions;
using RiftStrap.Models.Attributes;
using RiftStrap.RobloxInterfaces;

namespace RiftStrap.UI.ViewModels.Settings
{
    public class BehaviourViewModel : NotifyPropertyChangedViewModel
    {
        public ICommand ClearPinCommand { get; }
        public ICommand ManualPinCommand { get; }
        public ICommand FetchVersionsCommand { get; }

        private bool _isFetchingVersions;
        public bool IsFetchingVersions
        {
            get => _isFetchingVersions;
            set { _isFetchingVersions = value; OnPropertyChanged(nameof(IsFetchingVersions)); }
        }

        public BehaviourViewModel()
        {
            ClearPinCommand = new RelayCommand(() => PinnedVersionGuid = null);
            ManualPinCommand = new RelayCommand(ManualPin);
            FetchVersionsCommand = new AsyncRelayCommand(FetchVersionsAsync);
        }

        public bool MultiInstanceLaunching
        {
            get => App.Settings.Prop.MultiInstanceLaunching;
            set { App.Settings.Prop.MultiInstanceLaunching = value; OnPropertyChanged(nameof(MultiInstanceLaunching)); }
        }

        public IEnumerable<PreferredBinary> BinaryOptions => Enum.GetValues<PreferredBinary>();

        public PreferredBinary PreferredBinary
        {
            get => App.Settings.Prop.PreferredBinary;
            set { App.Settings.Prop.PreferredBinary = value; OnPropertyChanged(nameof(PreferredBinary)); }
        }

        public bool ConfirmLaunches
        {
            get => App.Settings.Prop.ConfirmLaunches;
            set => App.Settings.Prop.ConfirmLaunches = value;
        }

        public bool AutoRejoinEnabled
        {
            get => App.Settings.Prop.AutoRejoinEnabled;
            set { App.Settings.Prop.AutoRejoinEnabled = value; OnPropertyChanged(nameof(AutoRejoinEnabled)); }
        }

        public bool BackgroundUpdates
        {
            get => App.Settings.Prop.BackgroundUpdatesEnabled;
            set => App.Settings.Prop.BackgroundUpdatesEnabled = value;
        }

        public bool IsRobloxInstallationMissing => !App.IsPlayerInstalled && !App.IsStudioInstalled;

        public bool ForceRobloxReinstallation
        {
            get => App.State.Prop.ForceReinstall || IsRobloxInstallationMissing;
            set => App.State.Prop.ForceReinstall = value;
        }

        public List<string> ChannelDisplayNames { get; } = Enum.GetValues<RobloxChannel>()
            .Select(c =>
            {
                var field = typeof(RobloxChannel).GetField(c.ToString());
                var attr = field?.GetCustomAttribute<EnumNameAttribute>();
                return attr?.StaticName ?? c.ToString();
            }).ToList();

        public string SelectedChannelDisplay
        {
            get
            {
                var field = typeof(RobloxChannel).GetField(App.Settings.Prop.Channel.ToString());
                var attr = field?.GetCustomAttribute<EnumNameAttribute>();
                return attr?.StaticName ?? App.Settings.Prop.Channel.ToString();
            }
            set
            {
                var channels = Enum.GetValues<RobloxChannel>();
                foreach (var ch in channels)
                {
                    var field = typeof(RobloxChannel).GetField(ch.ToString());
                    var attr = field?.GetCustomAttribute<EnumNameAttribute>();
                    var name = attr?.StaticName ?? ch.ToString();
                    if (name == value)
                    {
                        App.Settings.Prop.Channel = ch;
                        break;
                    }
                }
                OnPropertyChanged(nameof(SelectedChannelDisplay));
                OnPropertyChanged(nameof(SelectedChannelDescription));
                PinnedVersionGuid = null;
                LoadVersionHistory();
            }
        }

        public string SelectedChannelDescription
        {
            get
            {
                return App.Settings.Prop.Channel switch
                {
                    RobloxChannel.Production => "Stable release — recommended for most users",
                    RobloxChannel.ZCanary => "Early access / beta builds — may have bugs",
                    RobloxChannel.ZIntegration => "Internal testing builds — may be unstable",
                    RobloxChannel.ZFlag => "Feature flag testing channel",
                    RobloxChannel.ZNext => "Future version preview channel",
                    _ => ""
                };
            }
        }

        public string? PinnedVersionGuid
        {
            get => App.Settings.Prop.PinnedVersionGuid;
            set
            {
                App.Settings.Prop.PinnedVersionGuid = value;
                OnPropertyChanged(nameof(PinnedVersionGuid));
                OnPropertyChanged(nameof(IsVersionPinned));
                OnPropertyChanged(nameof(PinnedVersionDisplay));
            }
        }

        public bool IsVersionPinned => !string.IsNullOrEmpty(PinnedVersionGuid);

        public string PinnedVersionDisplay => string.IsNullOrEmpty(PinnedVersionGuid)
            ? "Following latest"
            : $"Pinned: {PinnedVersionGuid}";

        public bool AllowVersionDowngrade
        {
            get => App.Settings.Prop.AllowVersionDowngrade;
            set { App.Settings.Prop.AllowVersionDowngrade = value; OnPropertyChanged(nameof(AllowVersionDowngrade)); }
        }

        public ObservableCollection<Models.Persistable.VersionHistoryEntry> VersionHistory { get; } = new();

        public void LoadVersionHistory()
        {
            VersionHistory.Clear();
            var channel = App.Settings.Prop.Channel.GetDescription() ?? "production";
            var entries = App.VersionHistoryManager.Prop.Entries
                .Where(e => string.IsNullOrEmpty(e.Channel) || e.Channel.Equals(channel, StringComparison.OrdinalIgnoreCase))
                .Take(20);
            foreach (var entry in entries)
                VersionHistory.Add(entry);
        }

        public async Task FetchVersionsAsync()
        {
            IsFetchingVersions = true;
            try
            {
                var channel = App.Settings.Prop.Channel.GetDescription() ?? "production";
                var versions = await Deployment.FetchChannelVersionsAsync(channel);

                VersionHistory.Clear();
                foreach (var v in versions)
                    VersionHistory.Add(v);
            }
            catch { }
            finally
            {
                IsFetchingVersions = false;
            }
        }

        private void ManualPin()
        {
            var input = Controls.Rift.RiftInputDialog.Show("Pin Version", "Enter version GUID:", "version-");
            if (string.IsNullOrWhiteSpace(input) || !input.StartsWith("version-")) return;
            PinnedVersionGuid = input;
            AllowVersionDowngrade = true;
        }

        public void PinVersion(Models.Persistable.VersionHistoryEntry entry)
        {
            PinnedVersionGuid = entry.VersionGuid;
            AllowVersionDowngrade = true;
        }
    }
}
