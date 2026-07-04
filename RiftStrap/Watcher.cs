using RiftStrap.AppData;
using RiftStrap.Integrations;
using RiftStrap.Models;
using RiftStrap.Features.GameProfiles;
using RiftStrap.Features.PerformanceDashboard;
using RiftStrap.Features.Notifications;
using RiftStrap.Features.Plugins;
using RiftStrap.Features.Analytics;
using RiftStrap.Features.AutoRejoin;
using RiftStrap.Features.MemoryCleaner;
using RiftStrap.Features.TimeLimiter;
using RiftStrap.Features.ScreenshotManager;

namespace RiftStrap
{
    public class Watcher : IDisposable
    {
        private readonly InterProcessLock _lock = new("Watcher");

        private readonly WatcherData? _watcherData;

        private readonly NotifyIconWrapper? _notifyIcon;

        public readonly ActivityWatcher? ActivityWatcher;

        public readonly DiscordRichPresence? RichPresence;

        private readonly GameProfileManager _gameProfiles = new();
        private readonly PerformanceMonitor _perfMonitor = new();
        private readonly NotificationService _notifications = new();
        private readonly PluginHost _pluginHost = new();
        private readonly AnalyticsService _analytics = new();
        private readonly AutoRejoinService _autoRejoin = new();
        private readonly MemoryCleanerService _memoryCleaner = new();
        private readonly TimeLimitService _timeLimiter = new();
        private readonly ScreenshotService _screenshotService = new();
        private double _lastParsedFps;
        private int _lastParsedPing;

        public Watcher()
        {
            const string LOG_IDENT = "Watcher";

            if (!_lock.IsAcquired)
            {
                App.Logger.WriteLine(LOG_IDENT, "Watcher instance already exists");
                return;
            }

            string? watcherDataArg = App.LaunchSettings.WatcherFlag.Data;

            if (String.IsNullOrEmpty(watcherDataArg))
            {
#if DEBUG
                string path = new RobloxPlayerData().ExecutablePath;
                if (!File.Exists(path))
                    throw new ApplicationException("Roblox player is not been installed");

                using var gameClientProcess = Process.Start(path);

                _watcherData = new() { ProcessId = gameClientProcess.Id };
#else
                throw new Exception("Watcher data not specified");
#endif
            }
            else
            {
                _watcherData = JsonSerializer.Deserialize<WatcherData>(Encoding.UTF8.GetString(Convert.FromBase64String(watcherDataArg)));
            }

            if (_watcherData is null)
                throw new Exception("Watcher data is invalid");

            if (App.Settings.Prop.EnableActivityTracking)
            {
                ActivityWatcher = new(_watcherData.LogFile);

                // Wire the auto-rejoin service from settings — it was previously never enabled,
                // so HandleDisconnectAsync always returned at its first guard and the feature
                // could never fire for anyone.
                _autoRejoin.Enabled = App.Settings.Prop.AutoRejoinEnabled;
                _autoRejoin.RejoinOnKick = App.Settings.Prop.AutoRejoinOnKick;

                if (App.Settings.Prop.UseDisableAppPatch)
                {
                    ActivityWatcher.OnAppClose += delegate
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Received desktop app exit, closing Roblox");
                        // CloseProcess swallows the case where the client has already exited;
                        // Process.GetProcessById would throw ArgumentException out of this handler.
                        CloseProcess(_watcherData.ProcessId);
                    };
                }

                if (App.Settings.Prop.UseDiscordRichPresence)
                    RichPresence = new(ActivityWatcher);

                ActivityWatcher.OnGameJoin += (_, _) =>
                {
                    var data = ActivityWatcher.Data;
                    if (data.PlaceId > 0)
                    {
                        _gameProfiles.OnGameJoin(data.PlaceId, data.UniverseId);
                        _pluginHost.NotifyGameJoin(data.PlaceId, data.UniverseId);

                        _ = Task.Run(async () =>
                        {
                            var name = $"Place {data.PlaceId}";
                            try
                            {
                                var uj = await App.HttpClient.GetStringAsync($"https://apis.roblox.com/universes/v1/places/{data.PlaceId}/universe");
                                var ud = JsonSerializer.Deserialize<JsonElement>(uj);
                                if (ud.TryGetProperty("universeId", out var uid))
                                {
                                    var gj = await App.HttpClient.GetStringAsync($"https://games.roblox.com/v1/games?universeIds={uid.GetInt64()}");
                                    var gd = JsonSerializer.Deserialize<JsonElement>(gj);
                                    if (gd.TryGetProperty("data", out var arr) && arr.GetArrayLength() > 0)
                                        name = arr[0].GetProperty("name").GetString() ?? name;
                                }
                            }
                            catch { }
                            _analytics.StartSession(data.PlaceId, name);
                        });
                        _autoRejoin.SetCurrentGame(data.PlaceId, data.JobId);
                        _autoRejoin.ResetRetries();
                        _perfMonitor.Start();

                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                var regionService = new Features.ServerRegion.ServerRegionService();
                                var region = await regionService.GetRegionFromAddressAsync(data.MachineAddress);
                                if (region != null)
                                    App.Logger.WriteLine("Watcher", $"Server region: {region.DisplayName} ({region.ISP})");
                            }
                            catch { }
                        });
                    }
                };

                ActivityWatcher.OnGameLeave += (_, _) =>
                {
                    _gameProfiles.OnGameLeave();
                    // Clear AutoRejoin session state on leave — otherwise _lastPlaceId stays set and a
                    // stray disconnect line while out-of-game would trigger a false rejoin to the game
                    // the user deliberately left. (Any in-flight rejoin already snapshotted its target.)
                    _autoRejoin.Clear();
                    _pluginHost.NotifyGameLeave();
                    _analytics.EndSession();
                    _perfMonitor.Stop();
                };

                ActivityWatcher.OnLogEntry += (_, logLine) =>
                {

                    _ = _autoRejoin.HandleDisconnectAsync(logLine);

                    if (logLine.Contains("[FLog::ClientProfiler]") && logLine.Contains("framerate"))
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(logLine, @"framerate:\s*([\d.]+)");
                        if (match.Success && double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var fps))
                            _lastParsedFps = fps;
                    }

                    if (logLine.Contains("[FLog::Network]") && logLine.Contains("ping:"))
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(logLine, @"ping:\s*(\d+)");
                        if (match.Success && int.TryParse(match.Groups[1].Value, out var ping))
                            _lastParsedPing = ping;
                    }

                    _perfMonitor.SetLogData(_lastParsedFps, _lastParsedPing);
                };
            }

            _notifications.Start();
            _memoryCleaner.Start();
            // Enforce the daily play-time limit: nothing subscribed to these events, so the limit and
            // break reminders did nothing (bug: "daily limit never enforced"). Surface reminders as tray
            // balloons; on hitting the daily limit, notify and close the Roblox client.
            _timeLimiter.OnReminder += msg =>
                Frontend.ShowBalloonTip("RiftStrap", msg, System.Windows.Forms.ToolTipIcon.Info);
            _timeLimiter.OnLimitReached += () =>
            {
                Frontend.ShowBalloonTip("RiftStrap", "Daily play-time limit reached - closing Roblox.",
                    System.Windows.Forms.ToolTipIcon.Warning);
                KillRobloxProcess();
            };
            _timeLimiter.StartSession();
            _screenshotService.StartWatching();

            _notifyIcon = new(this);
        }

        public void KillRobloxProcess() => CloseProcess(_watcherData!.ProcessId, true);

        public void CloseProcess(int pid, bool force = false)
        {
            const string LOG_IDENT = "Watcher::CloseProcess";

            try
            {
                using var process = Process.GetProcessById(pid);

                App.Logger.WriteLine(LOG_IDENT, $"Killing process '{process.ProcessName}' (pid={pid}, force={force})");

                if (process.HasExited)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"PID {pid} has already exited");
                    return;
                }

                if (force)
                    process.Kill();
                else
                    process.CloseMainWindow();
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"PID {pid} could not be closed");
                App.Logger.WriteException(LOG_IDENT, ex);
            }
        }

        public async Task Run()
        {
            if (!_lock.IsAcquired || _watcherData is null)
                return;

            await _pluginHost.LoadAllAsync();

            ActivityWatcher?.Start();

            while (Utilities.IsProcessRunning(_watcherData.ProcessId))
                await Task.Delay(1000);

            if (_watcherData.AutoclosePids is not null)
            {
                foreach (int pid in _watcherData.AutoclosePids)
                    CloseProcess(pid);
            }

            if (App.LaunchSettings.TestModeFlag.Active)
                Process.Start(Paths.Process, "-settings -testmode");
        }

        public void Dispose()
        {
            App.Logger.WriteLine("Watcher::Dispose", "Disposing Watcher");

            _perfMonitor.Dispose();
            _notifications.Dispose();
            _memoryCleaner.Dispose();
            _timeLimiter.Dispose();
            _pluginHost.Dispose();
            _screenshotService.Dispose();
            _notifyIcon?.Dispose();
            RichPresence?.Dispose();

            // Mutex has thread-affinity: if Dispose runs on a thread other than the one
            // that acquired the lock, ReleaseMutex throws. Swallow it so a failed release
            // cannot propagate out of Dispose and skip App.Terminate()/fault reporting.
            try
            {
                _lock.Dispose();
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("Watcher::Dispose", ex);
            }

            GC.SuppressFinalize(this);
        }
    }
}
