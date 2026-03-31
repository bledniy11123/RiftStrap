
#if DEBUG_UPDATER
#warning "Automatic updater debugging is enabled"
#endif

using System.ComponentModel;
using System.Data;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Shell;

using Microsoft.Win32;

using RiftStrap.AppData;
using RiftStrap.RobloxInterfaces;
using RiftStrap.UI.Elements.Bootstrapper.Base;

using ICSharpCode.SharpZipLib.Zip;

namespace RiftStrap
{
    public class Bootstrapper
    {
        private const int ProgressBarMaximum = 10000;

        private const string AppSettings =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n" +
            "<Settings>\r\n" +
            "	<ContentFolder>content</ContentFolder>\r\n" +
            "	<BaseUrl>http://www.roblox.com</BaseUrl>\r\n" +
            "</Settings>\r\n";

        private readonly FastZipEvents _fastZipEvents = new();
        private readonly CancellationTokenSource _cancelTokenSource = new();

        private IAppData AppData = default!;
        private LaunchMode _launchMode;

        private string _launchCommandLine = App.LaunchSettings.RobloxLaunchArgs;
        private Version? _latestVersion = null;
        private string _latestVersionGuid = null!;
        private string _latestVersionDirectory = null!;
        private PackageManifest _versionPackageManifest = null!;
        private bool _channelFetched = false;

        private bool _isInstalling = false;
        private double _progressIncrement;
        private double _taskbarProgressIncrement;
        private long _totalDownloadedBytes = 0;
        private bool _packageExtractionSuccess = true;

        private bool _mustUpgrade => App.LaunchSettings.ForceFlag.Active || App.State.Prop.ForceReinstall || String.IsNullOrEmpty(AppData.DistributionState.VersionGuid) || !File.Exists(AppData.ExecutablePath);
        private bool _noConnection = false;

        private AsyncMutex? _mutex;

        private int _appPid = 0;

        public IBootstrapperDialog? Dialog = null;

        public bool IsStudioLaunch => _launchMode != LaunchMode.Player;

        public string MutexName => $"{MutexNamePrefix}-{_launchMode}";
        public string BackgroundUpdaterMutexName => $"RiftStrap-BackgroundUpdater-{_launchMode}";

        public string MutexNamePrefix { get; set; } = "RiftStrap-Bootstrapper";
        public bool QuitIfMutexExists { get; set; } = false;

        public Bootstrapper(LaunchMode launchMode)
        {
            _launchMode = launchMode;

            _fastZipEvents.FileFailure += (_, e) =>
            {

                if (!e.Name.EndsWith(".ttf"))
                    throw e.Exception;

                App.Logger.WriteLine("FastZipEvents::OnFileFailure", $"Failed to extract {e.Name}");
                _packageExtractionSuccess = false;
            };
            _fastZipEvents.DirectoryFailure += (_, e) => throw e.Exception;
            _fastZipEvents.ProcessFile += (_, e) => e.ContinueRunning = !_cancelTokenSource.IsCancellationRequested;

            SetupAppData();
        }

        private void SetupAppData()
        {
            AppData = IsStudioLaunch ? new RobloxStudioData() : new RobloxPlayerData();
            Deployment.BinaryType = AppData.BinaryType;
        }

        private void SetStatus(string message)
        {
            App.Logger.WriteLine("Bootstrapper::SetStatus", message);

            message = message.Replace("{product}", AppData.ProductName);

            if (Dialog is not null)
                Dialog.Message = message;
        }

        private void UpdateProgressBar()
        {
            if (Dialog is null)
                return;

            int progressValue = (int)Math.Floor(_progressIncrement * _totalDownloadedBytes);

            progressValue = Math.Clamp(progressValue, 0, ProgressBarMaximum);

            Dialog.ProgressValue = progressValue;

            double taskbarProgressValue = _taskbarProgressIncrement * _totalDownloadedBytes;
            taskbarProgressValue = Math.Clamp(taskbarProgressValue, 0, App.TaskbarProgressMaximum);

            Dialog.TaskbarProgressValue = taskbarProgressValue;
        }

        private void HandleConnectionError(Exception exception)
        {
            const string LOG_IDENT = "Bootstrapper::HandleConnectionError";

            _noConnection = true;

            App.Logger.WriteLine(LOG_IDENT, "Connectivity check failed");
            App.Logger.WriteException(LOG_IDENT, exception);

            string message = Strings.Dialog_Connectivity_BadConnection;

            if (exception is AggregateException)
                exception = exception.InnerException!;

            if (exception is HttpRequestException && exception.InnerException is null)
                message = String.Format(Strings.Dialog_Connectivity_RobloxDown, "[status.roblox.com](https://status.roblox.com)");

            if (_mustUpgrade)
                message += $"\n\n{Strings.Dialog_Connectivity_RobloxUpgradeNeeded}\n\n{Strings.Dialog_Connectivity_TryAgainLater}";
            else
                message += $"\n\n{Strings.Dialog_Connectivity_RobloxUpgradeSkip}";

            Frontend.ShowConnectivityDialog(
                String.Format(Strings.Dialog_Connectivity_UnableToConnect, "Roblox"),
                message,
                _mustUpgrade ? MessageBoxImage.Error : MessageBoxImage.Warning,
                exception);

            if (_mustUpgrade)
                App.Terminate(ErrorCode.ERROR_CANCELLED);
        }

        public async Task Run()
        {
            const string LOG_IDENT = "Bootstrapper::Run";

            App.Logger.WriteLine(LOG_IDENT, "Running bootstrapper");

            if (Dialog is not null)
                Dialog.CancelEnabled = true;

            SetStatus(Strings.Bootstrapper_Status_Connecting);

            var connectionResult = await Deployment.InitializeConnectivity();

            App.Logger.WriteLine(LOG_IDENT, "Connectivity check finished");

            if (connectionResult is not null)
                HandleConnectionError(connectionResult);

#if (!DEBUG || DEBUG_UPDATER) && !QA_BUILD
            if (App.Settings.Prop.CheckForUpdates && !App.LaunchSettings.UpgradeFlag.Active)
            {
                bool updatePresent = await CheckForUpdates();

                if (updatePresent)
                    return;
            }
#endif

            App.AssertWindowsOSVersion();

            if (_launchMode == LaunchMode.Unknown)
            {
                await SafeGetLatestVersionInfo();

                if (_launchMode == LaunchMode.Unknown)
                    throw new ApplicationException("Failed to deduce launch type");
            }

            bool mutexExists = Utilities.DoesMutexExist(MutexName);

            if (mutexExists)
            {
                if (!QuitIfMutexExists)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"{MutexName} mutex exists, waiting...");
                    SetStatus(Strings.Bootstrapper_Status_WaitingOtherInstances);
                }
                else
                {
                    App.Logger.WriteLine(LOG_IDENT, $"{MutexName} mutex exists, exiting!");
                    return;
                }
            }

            await using var mutex = new AsyncMutex(false, MutexName);
            await mutex.AcquireAsync(_cancelTokenSource.Token);

            _mutex = mutex;

            if (mutexExists)
            {
                App.Settings.Load();
                App.State.Load();
                AppData.DistributionStateManager.Load();
            }

            await SafeGetLatestVersionInfo();

            CleanupVersionsFolder();

            bool allModificationsApplied = true;

            if (!_noConnection)
            {
                if (AppData.DistributionState.VersionGuid != _latestVersionGuid || _mustUpgrade)
                {
                    bool backgroundUpdaterMutexOpen = !App.LaunchSettings.BackgroundUpdaterFlag.Active && Utilities.DoesMutexExist(BackgroundUpdaterMutexName);

                    App.Logger.WriteLine(LOG_IDENT, $"Background updater running: {backgroundUpdaterMutexOpen}");

                    if (backgroundUpdaterMutexOpen && _mustUpgrade)
                    {

                        Utilities.KillBackgroundUpdater();
                        backgroundUpdaterMutexOpen = false;
                    }

                    if (!backgroundUpdaterMutexOpen)
                    {
                        if (IsEligibleForBackgroundUpdate())
                            StartBackgroundUpdater();
                        else
                            await UpgradeRoblox();
                    }
                }

                if (_cancelTokenSource.IsCancellationRequested)
                    return;

                Features.InGameUI.DefaultModApplier.EnsureDefaultMods();

                allModificationsApplied = await ApplyModifications();
            }

            if (IsStudioLaunch)
                WindowsRegistry.RegisterStudio();
            else
                WindowsRegistry.RegisterPlayer();

            if (_launchMode != LaunchMode.Player)
                await mutex.ReleaseAsync();

            if (!App.LaunchSettings.NoLaunchFlag.Active && !_cancelTokenSource.IsCancellationRequested)
            {
                if (!App.LaunchSettings.QuietFlag.Active)
                {

                    if (!_packageExtractionSuccess)
                        Frontend.ShowBalloonTip(Strings.Bootstrapper_ExtractionFailed_Title, Strings.Bootstrapper_ExtractionFailed_Message, ToolTipIcon.Warning);
                    else if (!allModificationsApplied)
                        Frontend.ShowBalloonTip(Strings.Bootstrapper_ModificationsFailed_Title, Strings.Bootstrapper_ModificationsFailed_Message, ToolTipIcon.Warning);
                }

                StartRoblox();
            }

            await mutex.ReleaseAsync();

            Dialog?.CloseBootstrapper();
        }

        private RegistryKey GetChannelRegistryKey() => Registry.CurrentUser.CreateSubKey($"SOFTWARE\\ROBLOX Corporation\\Environments\\{AppData.RegistryName}\\Channel");

        private string? GetCurrentChannelFromArgs()
        {
            const string LOG_IDENT = "Bootstrapper::GetCurrentChannelFromArgs";

            if (App.LaunchSettings.ChannelFlag.Active && !string.IsNullOrEmpty(App.LaunchSettings.ChannelFlag.Data))
            {
                App.Logger.WriteLine(LOG_IDENT, "Got from channel arg");
                return App.LaunchSettings.ChannelFlag.Data.ToLowerInvariant();
            }

            Match match = Regex.Match(
                App.LaunchSettings.RobloxLaunchArgs,
                "channel:([a-zA-Z0-9-_]+)",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
            );

            if (match.Groups.Count == 2)
            {
                App.Logger.WriteLine(LOG_IDENT, "Got from launch URI");
                return match.Groups[1].Value.ToLowerInvariant();
            }

            if (_launchMode != LaunchMode.Unknown)
            {
                using RegistryKey key = GetChannelRegistryKey();
                if (key.GetValue("www.roblox.com") is string value && !String.IsNullOrEmpty(value))
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Got from registry ({AppData.RegistryName})");
                    return value;
                }
            }
            else
            {
                App.Logger.WriteLine(LOG_IDENT, "Skipping registry check, unknown launch");
            }

            App.Logger.WriteLine(LOG_IDENT, "Could not find channel");
            return null;
        }

        private void FetchCurrentChannel()
        {
            const string LOG_IDENT = "Bootstrapper::FetchCurrentChannel";

            if (_channelFetched)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Channel has already been fetched");
                return;
            }

            string? settingsChannel = App.Settings.Prop.Channel.GetDescription();

            if (!String.IsNullOrEmpty(settingsChannel))
            {
                App.Logger.WriteLine(LOG_IDENT, $"Using channel from RiftStrap settings: {settingsChannel}");
                Deployment.Channel = settingsChannel;

                if (!Deployment.IsDefaultChannel)
                    App.SendStat("robloxChannel", settingsChannel);
            }
            else
            {
                string? channel = GetCurrentChannelFromArgs();

                if (!String.IsNullOrEmpty(channel))
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Got channel from args/registry: {channel}");
                    Deployment.Channel = channel;

                    if (!Deployment.IsDefaultChannel)
                        App.SendStat("robloxChannel", channel);
                }
                else
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Defaulting to {Deployment.DefaultChannel}");
                    Deployment.Channel = Deployment.DefaultChannel;
                }
            }

            _channelFetched = true;
        }

        private void UpdateChannelRegistry()
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey($"SOFTWARE\\ROBLOX Corporation\\Environments\\{AppData.RegistryName}\\Channel");
            key.SetValueSafe("www.roblox.com", Deployment.IsDefaultChannel ? "" : Deployment.Channel);
        }

        private async Task GetLatestVersionInfo()
        {
            const string LOG_IDENT = "Bootstrapper::GetLatestVersionInfo";

            FetchCurrentChannel();

            // Check for pinned version before fetching from API
            if (!string.IsNullOrEmpty(App.Settings.Prop.PinnedVersionGuid))
            {
                App.Logger.WriteLine(LOG_IDENT, $"Checking pinned version: {App.Settings.Prop.PinnedVersionGuid}");

                bool valid = await Deployment.ValidateVersionGuid(App.Settings.Prop.PinnedVersionGuid);

                if (valid)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Using pinned version: {App.Settings.Prop.PinnedVersionGuid}");

                    string pinnedGuid = App.Settings.Prop.PinnedVersionGuid;

                    if (pinnedGuid != _latestVersionGuid)
                    {
                        _latestVersionGuid = pinnedGuid;
                        _latestVersion = null;

                        _latestVersionDirectory = Path.Combine(Paths.Versions, _latestVersionGuid);

                        string pkgManifestUrl = Deployment.GetLocation($"/{_latestVersionGuid}-rbxPkgManifest.txt");
                        var pkgManifestData = await App.HttpClient.GetStringAsync(pkgManifestUrl);

                        _versionPackageManifest = new(pkgManifestData);
                    }

                    Deployment.RecordVersionHistory(_latestVersionGuid, "", Deployment.Channel);

                    return;
                }
                else
                {
                    App.Logger.WriteLine(LOG_IDENT, "Pinned version invalid, falling back to latest");
                    App.Settings.Prop.PinnedVersionGuid = null;
                    App.Settings.Save();
                }
            }

            string? newVersionGuid = null;
            Version? newVersion = null;

            if (!App.LaunchSettings.VersionFlag.Active || string.IsNullOrEmpty(App.LaunchSettings.VersionFlag.Data))
            {
                ClientVersion clientVersion;

                try
                {
                    clientVersion = await Deployment.GetInfo();
                }
                catch (InvalidChannelException ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Resetting channel from {Deployment.Channel} because {ex.StatusCode}");

                    Deployment.Channel = Deployment.DefaultChannel;
                    clientVersion = await Deployment.GetInfo();
                }

                UpdateChannelRegistry();

                newVersionGuid = clientVersion.VersionGuid;
                newVersion = Utilities.ParseVersionSafe(clientVersion.Version);
            }
            else
            {
                App.Logger.WriteLine(LOG_IDENT, $"Version set to {App.LaunchSettings.VersionFlag.Data} from arguments");
                newVersionGuid = App.LaunchSettings.VersionFlag.Data;

            }

            if (newVersionGuid != _latestVersionGuid)
            {
                _latestVersionGuid = newVersionGuid!;
                _latestVersion = newVersion;

                _latestVersionDirectory = Path.Combine(Paths.Versions, _latestVersionGuid);

                string pkgManifestUrl = Deployment.GetLocation($"/{_latestVersionGuid}-rbxPkgManifest.txt");
                var pkgManifestData = await App.HttpClient.GetStringAsync(pkgManifestUrl);

                _versionPackageManifest = new(pkgManifestData);
            }

            // Record version history after successful fetch
            Deployment.RecordVersionHistory(
                _latestVersionGuid,
                _latestVersion?.ToString() ?? "",
                Deployment.Channel
            );

            if (_launchMode == LaunchMode.Unknown)
            {
                if (_versionPackageManifest.Count != 0)
                {
                    App.Logger.WriteLine(LOG_IDENT, "Identifying launch mode from package manifest");

                    bool isPlayer = _versionPackageManifest.Exists(x => x.Name == "RobloxApp.zip");
                    App.Logger.WriteLine(LOG_IDENT, $"isPlayer: {isPlayer}");

                    _launchMode = isPlayer ? LaunchMode.Player : LaunchMode.Studio;

                    SetupAppData();

                    UpdateChannelRegistry();
                }
                else
                {
                    App.Logger.WriteLine(LOG_IDENT, "Could not identify launch mode as package manifest is empty");
                }
            }
        }

        private async Task SafeGetLatestVersionInfo()
        {
            if (!_noConnection)
            {
                try
                {
                    await GetLatestVersionInfo();
                }
                catch (Exception ex)
                {
                    HandleConnectionError(ex);
                }
            }
        }

        private bool IsEligibleForBackgroundUpdate()
        {
            const string LOG_IDENT = "Bootstrapper::IsEligibleForBackgroundUpdate";

            if (App.LaunchSettings.BackgroundUpdaterFlag.Active)
            {
                App.Logger.WriteLine(LOG_IDENT, "Not eligible: Is the background updater process");
                return false;
            }

            if (!App.Settings.Prop.BackgroundUpdatesEnabled)
            {
                App.Logger.WriteLine(LOG_IDENT, "Not eligible: Background updates disabled");
                return false;
            }

            if (_mustUpgrade)
            {
                App.Logger.WriteLine(LOG_IDENT, "Not eligible: Must upgrade is true");
                return false;
            }

            const long minimumFreeSpace = 5_000_000_000;
            long space = Filesystem.GetFreeDiskSpace(Paths.Base);
            if (space < minimumFreeSpace)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Not eligible: User has {space} free space, at least {minimumFreeSpace} is required");
                return false;
            }

            if (_latestVersion == default)
            {
                App.Logger.WriteLine(LOG_IDENT, "Not eligible: Latest version is undefined");
                return false;
            }

            Version? currentVersion = Utilities.GetRobloxVersion(AppData);
            if (currentVersion == default)
            {
                App.Logger.WriteLine(LOG_IDENT, "Not eligible: Current version is undefined");
                return false;
            }

            if (currentVersion.Minor > _latestVersion.Minor)
            {
                if (App.Settings.Prop.AllowVersionDowngrade)
                {
                    App.Logger.WriteLine(LOG_IDENT, "Downgrade detected but allowed by settings");
                }
                else
                {
                    App.Logger.WriteLine(LOG_IDENT, "Not eligible: Downgrade");
                    return false;
                }
            }

            int diff = _latestVersion.Minor - currentVersion.Minor;
            if (diff == 0 || diff == 1)
            {
                App.Logger.WriteLine(LOG_IDENT, "Eligible");
                return true;
            }
            else
            {
                App.Logger.WriteLine(LOG_IDENT, $"Not eligible: Major version diff is {diff}");
                return false;
            }
        }

        private void StartRoblox()
        {
            const string LOG_IDENT = "Bootstrapper::StartRoblox";

            SetStatus(Strings.Bootstrapper_Status_Starting);

            var startInfo = new ProcessStartInfo()
            {
                FileName = AppData.ExecutablePath,
                Arguments = _launchCommandLine,
                WorkingDirectory = AppData.Directory
            };

            if (_launchMode == LaunchMode.Player && ShouldRunAsAdmin())
            {
                startInfo.Verb = "runas";
                startInfo.UseShellExecute = true;
            }
            else if (_launchMode == LaunchMode.StudioAuth)
            {
                Process.Start(startInfo);
                return;
            }

            string? logFileName = null;

            string rbxDir = Path.Combine(Paths.LocalAppData, "Roblox");
            if (!Directory.Exists(rbxDir))
                Directory.CreateDirectory(rbxDir);

            string rbxLogDir = Path.Combine(rbxDir, "logs");
            if (!Directory.Exists(rbxLogDir))
                Directory.CreateDirectory(rbxLogDir);

            var logWatcher = new FileSystemWatcher()
            {
                Path = rbxLogDir,
                Filter = "*.log",
                EnableRaisingEvents = true
            };

            var logCreatedEvent = new AutoResetEvent(false);

            logWatcher.Created += (_, e) =>
            {
                logWatcher.EnableRaisingEvents = false;
                logFileName = e.FullPath;
                logCreatedEvent.Set();
            };

            try
            {
                using var process = Process.Start(startInfo)!;
                _appPid = process.Id;
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {

                return;
            }
            catch (Exception)
            {

                File.Delete(AppData.ExecutablePath);
                throw;
            }

            App.Logger.WriteLine(LOG_IDENT, $"Started Roblox (PID {_appPid}), waiting for log file");

            if (App.Settings.Prop.MultiInstanceLaunching)
            {
                Task.Run(() =>
                {
                    Thread.Sleep(3000);
                    try
                    {
                        foreach (var name in new[] { "ROBLOX_singletonMutex", "ROBLOX_singletonEvent" })
                        {
                            if (EventWaitHandle.TryOpenExisting(name, out var handle))
                            {
                                handle.Close();
                                App.Logger.WriteLine(LOG_IDENT, $"Closed {name} for multi-instance");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Multi-instance mutex cleanup failed: {ex.Message}");
                    }
                });
            }

            logCreatedEvent.WaitOne(TimeSpan.FromSeconds(15));

            if (String.IsNullOrEmpty(logFileName))
            {
                App.Logger.WriteLine(LOG_IDENT, "Unable to identify log file");
                Frontend.ShowPlayerErrorDialog();
                return;
            }
            else
            {
                App.Logger.WriteLine(LOG_IDENT, $"Got log file as {logFileName}");
            }

            _mutex?.ReleaseAsync();

            if (IsStudioLaunch)
                return;

            var autoclosePids = new List<int>();

            foreach (var integration in App.Settings.Prop.CustomIntegrations)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Launching custom integration '{integration.Name}' ({integration.Location} {integration.LaunchArgs} - autoclose is {integration.AutoClose})");

                int pid = 0;

                try
                {
                    var process = Process.Start(new ProcessStartInfo
                    {
                        FileName = integration.Location,
                        Arguments = integration.LaunchArgs.Replace("\r\n", " "),
                        WorkingDirectory = Path.GetDirectoryName(integration.Location),
                        UseShellExecute = true
                    })!;

                    pid = process.Id;
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Failed to launch integration '{integration.Name}'!");
                    App.Logger.WriteLine(LOG_IDENT, ex.Message);
                }

                if (integration.AutoClose && pid != 0)
                    autoclosePids.Add(pid);
            }

            if (App.Settings.Prop.EnableActivityTracking || App.LaunchSettings.TestModeFlag.Active || autoclosePids.Any())
            {
                using var ipl = new InterProcessLock("Watcher", TimeSpan.FromSeconds(5));

                var watcherData = new WatcherData
                {
                    ProcessId = _appPid,
                    LogFile = logFileName,
                    AutoclosePids = autoclosePids
                };

                string watcherDataArg = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(watcherData)));

                string args = $"-watcher \"{watcherDataArg}\"";

                if (App.LaunchSettings.TestModeFlag.Active)
                    args += " -testmode";

                if (ipl.IsAcquired)
                    Process.Start(Paths.Process, args);
            }

            Thread.Sleep(1000);
        }

        private bool ShouldRunAsAdmin()
        {
            foreach (var root in WindowsRegistry.Roots)
            {
                using var key = root.OpenSubKey("SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\AppCompatFlags\\Layers");

                if (key is null)
                    continue;

                string? flags = (string?)key.GetValue(AppData.ExecutablePath);

                if (flags is not null && flags.Contains("RUNASADMIN", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public void Cancel()
        {
            const string LOG_IDENT = "Bootstrapper::Cancel";

            if (_cancelTokenSource.IsCancellationRequested)
                return;

            App.Logger.WriteLine(LOG_IDENT, "Cancelling launch...");

            _cancelTokenSource.Cancel();

            if (Dialog is not null)
                Dialog.CancelEnabled = false;

            if (_isInstalling)
            {
                try
                {

                    if (Directory.Exists(_latestVersionDirectory))
                        Directory.Delete(_latestVersionDirectory, true);
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, "Could not fully clean up installation!");
                    App.Logger.WriteException(LOG_IDENT, ex);
                }
            }
            else if (_appPid != 0)
            {
                try
                {
                    using var process = Process.GetProcessById(_appPid);
                    process.Kill();
                }
                catch (Exception) { }
            }

            Dialog?.CloseBootstrapper();

            App.SoftTerminate(ErrorCode.ERROR_CANCELLED);
        }

        private async Task<bool> CheckForUpdates()
        {
            const string LOG_IDENT = "Bootstrapper::CheckForUpdates";

            if (Process.GetProcessesByName(App.ProjectName).Length > 1)
            {
                App.Logger.WriteLine(LOG_IDENT, $"More than one RiftStrap instance running, aborting update check");
                return false;
            }

            App.Logger.WriteLine(LOG_IDENT, "Checking for updates...");

#if !DEBUG_UPDATER
            var releaseInfo = await App.GetLatestRelease();

            if (releaseInfo is null)
                return false;

            var versionComparison = Utilities.CompareVersions(App.Version, releaseInfo.TagName);

            if (App.IsProductionBuild && versionComparison == VersionComparison.Equal || versionComparison == VersionComparison.GreaterThan)
            {
                App.Logger.WriteLine(LOG_IDENT, "No updates found");
                return false;
            }

            if (Dialog is not null)
                Dialog.CancelEnabled = false;

            string version = releaseInfo.TagName;
#else
            string version = App.Version;
#endif

            SetStatus(Strings.Bootstrapper_Status_UpgradingRiftStrap);

            try
            {
#if DEBUG_UPDATER
                string downloadLocation = Path.Combine(Paths.TempUpdates, "RiftStrap.exe");

                Directory.CreateDirectory(Paths.TempUpdates);

                File.Copy(Paths.Process, downloadLocation, true);
#else
                var asset = releaseInfo.Assets![0];

                string downloadLocation = Path.Combine(Paths.TempUpdates, asset.Name);

                Directory.CreateDirectory(Paths.TempUpdates);

                App.Logger.WriteLine(LOG_IDENT, $"Downloading {releaseInfo.TagName}...");

                if (!File.Exists(downloadLocation))
                {
                    var response = await App.HttpClient.GetAsync(asset.BrowserDownloadUrl);

                    await using var fileStream = new FileStream(downloadLocation, FileMode.OpenOrCreate, FileAccess.Write);
                    await response.Content.CopyToAsync(fileStream);
                }
#endif

                App.Logger.WriteLine(LOG_IDENT, $"Starting {version}...");

                ProcessStartInfo startInfo = new()
                {
                    FileName = downloadLocation,
                };

                startInfo.ArgumentList.Add("-upgrade");

                foreach (string arg in App.LaunchSettings.Args)
                    startInfo.ArgumentList.Add(arg);

                if (_launchMode == LaunchMode.Player && !startInfo.ArgumentList.Contains("-player"))
                    startInfo.ArgumentList.Add("-player");
                else if (_launchMode == LaunchMode.Studio && !startInfo.ArgumentList.Contains("-studio"))
                    startInfo.ArgumentList.Add("-studio");

                App.Settings.Save();

                new InterProcessLock("AutoUpdater");

                Process.Start(startInfo);

                return true;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "An exception occurred when running the auto-updater");
                App.Logger.WriteException(LOG_IDENT, ex);

                Frontend.ShowMessageBox(
                    string.Format(Strings.Bootstrapper_AutoUpdateFailed, version),
                    MessageBoxImage.Information
                );

                Utilities.ShellExecute(App.ProjectDownloadLink);
            }

            return false;
        }

        private static bool TryDeleteRobloxInDirectory(string dir)
        {

            string clientPath = Path.Combine(dir, "RobloxPlayerBeta.exe");
            if (!File.Exists(clientPath))
            {
                clientPath = Path.Combine(dir, "RobloxStudioBeta.exe");
                if (!File.Exists(clientPath))
                    return true;
            }

            try
            {
                File.Delete(clientPath);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static void CleanupVersionsFolder()
        {
            const string LOG_IDENT = "Bootstrapper::CleanupVersionsFolder";

            if (App.LaunchSettings.BackgroundUpdaterFlag.Active)
            {
                App.Logger.WriteLine(LOG_IDENT, "Background updater tried to cleanup, stopping!");
                return;
            }

            if (!Directory.Exists(Paths.Versions))
            {
                App.Logger.WriteLine(LOG_IDENT, "Versions directory does not exist, skipping cleanup.");
                return;
            }

            foreach (string dir in Directory.GetDirectories(Paths.Versions))
            {
                string dirName = Path.GetFileName(dir);

                if (dirName != App.PlayerState.Prop.VersionGuid && dirName != App.StudioState.Prop.VersionGuid)
                {

                    if (!TryDeleteRobloxInDirectory(dir))
                        continue;

                    try
                    {
                        Directory.Delete(dir, true);
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Failed to delete {dir}");
                        App.Logger.WriteException(LOG_IDENT, ex);
                    }
                }
            }
        }

        private void MigrateCompatibilityFlags()
        {
            const string LOG_IDENT = "Bootstrapper::MigrateCompatibilityFlags";

            string oldClientLocation = Path.Combine(Paths.Versions, AppData.DistributionState.VersionGuid, AppData.ExecutableName);
            string newClientLocation = Path.Combine(_latestVersionDirectory, AppData.ExecutableName);

            using RegistryKey appFlagsKey = Registry.CurrentUser.CreateSubKey($"SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\AppCompatFlags\\Layers");
            string? appFlags = appFlagsKey.GetValue(oldClientLocation) as string;

            if (appFlags is not null)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Migrating app compatibility flags from {oldClientLocation} to {newClientLocation}...");
                appFlagsKey.SetValueSafe(newClientLocation, appFlags);
                appFlagsKey.DeleteValueSafe(oldClientLocation);
            }
        }

        private void KillRobloxInstances()
        {
            const string LOG_IDENT = "Bootstrapper::KillRobloxInstances";

            List<Process> processes = new List<Process>();
            processes.AddRange(Process.GetProcessesByName(AppData.ProcessName));
            processes.AddRange(Process.GetProcessesByName("RobloxCrashHandler"));

            foreach (Process process in processes)
            {
                try
                {
                    process.Kill();
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Failed to close process {process.Id}");
                    App.Logger.WriteException(LOG_IDENT, ex);
                }
            }
        }

        private async Task GracefullyCloseRobloxInstances()
        {
            const string LOG_IDENT = "Bootstrapper::GracefullyCloseRobloxInstances";

            while (true)
            {
                Process[] processes = Process.GetProcessesByName(AppData.ProcessName);
                if (processes.Length == 0)
                    break;

                foreach (Process process in processes)
                {
                    try
                    {
                        process.CloseMainWindow();
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Failed to close process {process.Id}");
                        App.Logger.WriteException(LOG_IDENT, ex);
                    }
                }

                try
                {
                    await Task.Delay(1000, _cancelTokenSource.Token);
                }
                catch (TaskCanceledException)
                {
                    return;
                }
            }
        }

        private async Task UpgradeRoblox()
        {
            const string LOG_IDENT = "Bootstrapper::UpgradeRoblox";

            Directory.CreateDirectory(Paths.Base);
            Directory.CreateDirectory(Paths.Downloads);
            Directory.CreateDirectory(Paths.Versions);

            _isInstalling = true;

            if (!App.LaunchSettings.BackgroundUpdaterFlag.Active)
            {
                SetStatus(Strings.Bootstrapper_Status_ShuttingDown);

                if (IsStudioLaunch)
                    await GracefullyCloseRobloxInstances();
                else
                    KillRobloxInstances();

                if (_cancelTokenSource.IsCancellationRequested)
                    return;

                if (Directory.Exists(_latestVersionDirectory))
                {
                    try
                    {
                        Directory.Delete(_latestVersionDirectory, true);
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Failed to delete the latest version directory");
                        App.Logger.WriteException(LOG_IDENT, ex);
                    }
                }
            }

            if (String.IsNullOrEmpty(AppData.DistributionState.VersionGuid))
                SetStatus(Strings.Bootstrapper_Status_Installing);
            else
                SetStatus(Strings.Bootstrapper_Status_Upgrading);

            Directory.CreateDirectory(_latestVersionDirectory);

            var cachedPackageHashes = Directory.GetFiles(Paths.Downloads).Select(x => Path.GetFileName(x));

            int totalSizeRequired = 0;

            totalSizeRequired += _versionPackageManifest.Where(x => !cachedPackageHashes.Contains(x.Signature)).Sum(x => x.PackedSize);
            totalSizeRequired += _versionPackageManifest.Sum(x => x.Size);

            if (Filesystem.GetFreeDiskSpace(Paths.Base) < totalSizeRequired)
            {
                Frontend.ShowMessageBox(Strings.Bootstrapper_NotEnoughSpace, MessageBoxImage.Error);
                App.Terminate(ErrorCode.ERROR_INSTALL_FAILURE);
                return;
            }

            if (Dialog is not null)
            {
                Dialog.ProgressStyle = ProgressBarStyle.Continuous;
                Dialog.TaskbarProgressState = TaskbarItemProgressState.Normal;

                Dialog.ProgressMaximum = ProgressBarMaximum;

                int totalPackedSize = _versionPackageManifest.Sum(package => package.PackedSize);
                _progressIncrement = (double)ProgressBarMaximum / totalPackedSize;

                _taskbarProgressIncrement = App.TaskbarProgressMaximum / (double)totalPackedSize;
            }

            var extractionTasks = new List<Task>();

            foreach (var package in _versionPackageManifest)
            {
                if (_cancelTokenSource.IsCancellationRequested)
                    return;

                await DownloadPackage(package);

                if (package.Name == "WebView2RuntimeInstaller.zip")
                    continue;

                extractionTasks.Add(Task.Run(() => ExtractPackage(package), _cancelTokenSource.Token));
            }

            if (_cancelTokenSource.IsCancellationRequested)
                return;

            if (Dialog is not null)
            {
                Dialog.ProgressStyle = ProgressBarStyle.Marquee;
                Dialog.TaskbarProgressState = TaskbarItemProgressState.Indeterminate;
                SetStatus(Strings.Bootstrapper_Status_Configuring);
            }

            await Task.WhenAll(extractionTasks);

            App.Logger.WriteLine(LOG_IDENT, "Writing AppSettings.xml...");
            await File.WriteAllTextAsync(Path.Combine(_latestVersionDirectory, "AppSettings.xml"), AppSettings);

            if (_cancelTokenSource.IsCancellationRequested)
                return;

            if (App.State.Prop.PromptWebView2Install)
            {
                using var hklmKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\\WOW6432Node\\Microsoft\\EdgeUpdate\\Clients\\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}");
                using var hkcuKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\EdgeUpdate\\Clients\\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}");

                if (hklmKey is not null || hkcuKey is not null)
                {

                    App.State.Prop.PromptWebView2Install = true;
                }
                else
                {
                    var result = Frontend.ShowMessageBox(Strings.Bootstrapper_WebView2NotFound, MessageBoxImage.Warning, MessageBoxButton.YesNo, MessageBoxResult.Yes);

                    if (result != MessageBoxResult.Yes)
                    {
                        App.State.Prop.PromptWebView2Install = false;
                    }
                    else
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Installing WebView2 runtime...");

                        var package = _versionPackageManifest.Find(x => x.Name == "WebView2RuntimeInstaller.zip");

                        if (package is null)
                        {
                            App.Logger.WriteLine(LOG_IDENT, "Aborted runtime install because package does not exist, has WebView2 been added in this Roblox version yet?");
                            return;
                        }

                        string baseDirectory = Path.Combine(_latestVersionDirectory, AppData.PackageDirectoryMap[package.Name]);

                        ExtractPackage(package);

                        SetStatus(Strings.Bootstrapper_Status_InstallingWebView2);

                        var startInfo = new ProcessStartInfo()
                        {
                            WorkingDirectory = baseDirectory,
                            FileName = Path.Combine(baseDirectory, "MicrosoftEdgeWebview2Setup.exe"),
                            Arguments = "/silent /install"
                        };

                        await Process.Start(startInfo)!.WaitForExitAsync();

                        App.Logger.WriteLine(LOG_IDENT, "Finished installing runtime");

                        Directory.Delete(baseDirectory, true);
                    }
                }
            }

            MigrateCompatibilityFlags();

            AppData.DistributionState.VersionGuid = _latestVersionGuid;

            AppData.DistributionState.PackageHashes.Clear();

            foreach (var package in _versionPackageManifest)
                AppData.DistributionState.PackageHashes.Add(package.Name, package.Signature);

            CleanupVersionsFolder();

            var allPackageHashes = new List<string>();

            allPackageHashes.AddRange(App.PlayerState.Prop.PackageHashes.Values);
            allPackageHashes.AddRange(App.StudioState.Prop.PackageHashes.Values);

            if (!App.Settings.Prop.DebugDisableVersionPackageCleanup)
            {
                foreach (string hash in cachedPackageHashes)
                {
                    if (!allPackageHashes.Contains(hash))
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Deleting unused package {hash}");

                        try
                        {
                            File.Delete(Path.Combine(Paths.Downloads, hash));
                        }
                        catch (Exception ex)
                        {
                            App.Logger.WriteLine(LOG_IDENT, $"Failed to delete {hash}!");
                            App.Logger.WriteException(LOG_IDENT, ex);
                        }
                    }
                }
            }

            App.Logger.WriteLine(LOG_IDENT, "Registering approximate program size...");

            int distributionSize = _versionPackageManifest.Sum(x => x.Size + x.PackedSize) / 1024;

            AppData.DistributionState.Size = distributionSize;

            int totalSize = App.PlayerState.Prop.Size + App.PlayerState.Prop.Size;

            using (var uninstallKey = Registry.CurrentUser.CreateSubKey(App.UninstallKey))
            {
                uninstallKey.SetValueSafe("EstimatedSize", totalSize);
            }

            App.Logger.WriteLine(LOG_IDENT, $"Registered as {totalSize} KB");

            App.State.Prop.ForceReinstall = false;

            App.State.Save();
            AppData.DistributionStateManager.Save();

            _isInstalling = false;
        }

        private void StartBackgroundUpdater()
        {
            const string LOG_IDENT = "Bootstrapper::StartBackgroundUpdater";

            if (Utilities.DoesMutexExist(BackgroundUpdaterMutexName))
            {
                App.Logger.WriteLine(LOG_IDENT, "Background updater already running");
                return;
            }

            App.Logger.WriteLine(LOG_IDENT, "Starting background updater");

            Process.Start(Paths.Process, $"-backgroundupdater {_launchMode}");
        }

        private async Task<bool> ApplyModifications()
        {
            const string LOG_IDENT = "Bootstrapper::ApplyModifications";

            bool success = true;

            SetStatus(Strings.Bootstrapper_Status_ApplyingModifications);

            App.Logger.WriteLine(LOG_IDENT, "Checking file mods...");

            File.Delete(Path.Combine(Paths.Base, "ModManifest.txt"));

            List<string> modFolderFiles = new();

            Directory.CreateDirectory(Paths.Modifications);

            string modFontFamiliesFolder = Path.Combine(Paths.Modifications, "content\\fonts\\families");

            if (File.Exists(Paths.CustomFont))
            {
                App.Logger.WriteLine(LOG_IDENT, "Begin font check");

                Directory.CreateDirectory(modFontFamiliesFolder);

                const string path = "rbxasset://fonts/CustomFont.ttf";

                string contentFolder = Path.Combine(_latestVersionDirectory, "content");
                Directory.CreateDirectory(contentFolder);

                string fontsFolder = Path.Combine(contentFolder, "fonts");
                Directory.CreateDirectory(fontsFolder);

                string familiesFolder = Path.Combine(fontsFolder, "families");
                Directory.CreateDirectory(familiesFolder);

                foreach (string jsonFilePath in Directory.GetFiles(familiesFolder))
                {
                    string jsonFilename = Path.GetFileName(jsonFilePath);
                    string modFilepath = Path.Combine(modFontFamiliesFolder, jsonFilename);

                    if (File.Exists(modFilepath))
                        continue;

                    App.Logger.WriteLine(LOG_IDENT, $"Setting font for {jsonFilename}");

                    var fontFamilyData = JsonSerializer.Deserialize<FontFamily>(File.ReadAllText(jsonFilePath));

                    if (fontFamilyData is null)
                        continue;

                    bool shouldWrite = false;

                    foreach (var fontFace in fontFamilyData.Faces)
                    {
                        if (fontFace.AssetId != path)
                        {
                            fontFace.AssetId = path;
                            shouldWrite = true;
                        }
                    }

                    if (shouldWrite)
                        File.WriteAllText(modFilepath, JsonSerializer.Serialize(fontFamilyData, new JsonSerializerOptions { WriteIndented = true }));
                }

                App.Logger.WriteLine(LOG_IDENT, "End font check");
            }
            else if (Directory.Exists(modFontFamiliesFolder))
            {
                Directory.Delete(modFontFamiliesFolder, true);
            }

            foreach (string file in Directory.GetFiles(Paths.Modifications, "*.*", SearchOption.AllDirectories))
            {
                if (_cancelTokenSource.IsCancellationRequested)
                    return true;

                string relativeFile = file.Substring(Paths.Modifications.Length + 1);

                if (relativeFile == "README.txt")
                {
                    File.Delete(file);
                    continue;
                }

                if (!App.Settings.Prop.UseFastFlagManager && String.Equals(relativeFile, "ClientSettings\\ClientAppSettings.json", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (relativeFile.EndsWith(".lock"))
                    continue;

                modFolderFiles.Add(relativeFile);

                string fileModFolder = Path.Combine(Paths.Modifications, relativeFile);
                string fileVersionFolder = Path.Combine(_latestVersionDirectory, relativeFile);

                if (File.Exists(fileVersionFolder) && MD5Hash.FromFile(fileModFolder) == MD5Hash.FromFile(fileVersionFolder))
                {
                    App.Logger.WriteLine(LOG_IDENT, $"{relativeFile} already exists in the version folder, and is a match");
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(fileVersionFolder)!);

                Filesystem.AssertReadOnly(fileVersionFolder);
                try
                {
                    File.Copy(fileModFolder, fileVersionFolder, true);
                    Filesystem.AssertReadOnly(fileVersionFolder);
                    App.Logger.WriteLine(LOG_IDENT, $"{relativeFile} has been copied to the version folder");
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Failed to apply modification ({relativeFile})");
                    App.Logger.WriteException(LOG_IDENT, ex);
                    success = false;
                }
            }

            var fileRestoreMap = new Dictionary<string, List<string>>();

            foreach (string fileLocation in AppData.DistributionState.ModManifest)
            {
                if (modFolderFiles.Contains(fileLocation))
                    continue;

                var packageMapEntry = AppData.PackageDirectoryMap.SingleOrDefault(x => !String.IsNullOrEmpty(x.Value) && fileLocation.StartsWith(x.Value));
                string packageName = packageMapEntry.Key;

                if (String.IsNullOrEmpty(packageName))
                {
                    App.Logger.WriteLine(LOG_IDENT, $"{fileLocation} was removed as a mod but does not belong to a package");

                    string versionFileLocation = Path.Combine(_latestVersionDirectory, fileLocation);

                    if (File.Exists(versionFileLocation))
                        File.Delete(versionFileLocation);

                    continue;
                }

                string fileName = fileLocation.Substring(packageMapEntry.Value.Length);

                if (!fileRestoreMap.ContainsKey(packageName))
                    fileRestoreMap[packageName] = new();

                fileRestoreMap[packageName].Add(fileName);

                App.Logger.WriteLine(LOG_IDENT, $"{fileLocation} was removed as a mod, restoring from {packageName}");
            }

            foreach (var entry in fileRestoreMap)
            {
                var package = _versionPackageManifest.Find(x => x.Name == entry.Key);

                if (package is not null)
                {
                    if (_cancelTokenSource.IsCancellationRequested)
                        return true;

                    await DownloadPackage(package);
                    ExtractPackage(package, entry.Value);
                }
            }

            if (App.LaunchSettings.BackgroundUpdaterFlag.Active || !AppData.DistributionStateManager.HasFileOnDiskChanged())
            {
                AppData.DistributionState.ModManifest = modFolderFiles;
                AppData.DistributionStateManager.Save();
            }
            else
            {
                App.Logger.WriteLine(LOG_IDENT, $"{AppData.DistributionStateManager.ClassName} disk mismatch, not saving ModManifest");
            }

            App.Logger.WriteLine(LOG_IDENT, $"Finished checking file mods");

            if (!success)
                App.Logger.WriteLine(LOG_IDENT, "Failed to apply all modifications");

            return success;
        }

        private async Task DownloadPackage(Package package)
        {
            string LOG_IDENT = $"Bootstrapper::DownloadPackage.{package.Name}";

            if (_cancelTokenSource.IsCancellationRequested)
                return;

            Directory.CreateDirectory(Paths.Downloads);

            string packageUrl = Deployment.GetLocation($"/{_latestVersionGuid}-{package.Name}");
            string robloxPackageLocation = Path.Combine(Paths.LocalAppData, "Roblox", "Downloads", package.Signature);

            if (File.Exists(package.DownloadPath))
            {
                var file = new FileInfo(package.DownloadPath);

                string calculatedMD5 = MD5Hash.FromFile(package.DownloadPath);

                if (calculatedMD5 != package.Signature)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Package is corrupted ({calculatedMD5} != {package.Signature})! Deleting and re-downloading...");
                    file.Delete();
                }
                else
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Package is already downloaded, skipping...");

                    _totalDownloadedBytes += package.PackedSize;
                    UpdateProgressBar();

                    return;
                }
            }
            else if (File.Exists(robloxPackageLocation))
            {

                App.Logger.WriteLine(LOG_IDENT, $"Found existing copy at '{robloxPackageLocation}'! Copying to Downloads folder...");
                File.Copy(robloxPackageLocation, package.DownloadPath);

                _totalDownloadedBytes += package.PackedSize;
                UpdateProgressBar();

                return;
            }

            if (File.Exists(package.DownloadPath))
                return;

            const int maxTries = 5;

            App.Logger.WriteLine(LOG_IDENT, "Downloading...");

            var buffer = new byte[4096];

            for (int i = 1; i <= maxTries; i++)
            {
                if (_cancelTokenSource.IsCancellationRequested)
                    return;

                int totalBytesRead = 0;

                try
                {
                    var response = await App.HttpClient.GetAsync(packageUrl, HttpCompletionOption.ResponseHeadersRead, _cancelTokenSource.Token);
                    await using var stream = await response.Content.ReadAsStreamAsync(_cancelTokenSource.Token);
                    await using var fileStream = new FileStream(package.DownloadPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Delete);

                    while (true)
                    {
                        if (_cancelTokenSource.IsCancellationRequested)
                        {
                            stream.Close();
                            fileStream.Close();
                            return;
                        }

                        int bytesRead = await stream.ReadAsync(buffer, _cancelTokenSource.Token);

                        if (bytesRead == 0)
                            break;

                        totalBytesRead += bytesRead;

                        await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), _cancelTokenSource.Token);

                        _totalDownloadedBytes += bytesRead;
                        UpdateProgressBar();
                    }

                    string hash = MD5Hash.FromStream(fileStream);

                    if (hash != package.Signature)
                        throw new ChecksumFailedException($"Failed to verify download of {packageUrl}\n\nExpected hash: {package.Signature}\nGot hash: {hash}");

                    App.Logger.WriteLine(LOG_IDENT, $"Finished downloading! ({totalBytesRead} bytes total)");
                    break;
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"An exception occurred after downloading {totalBytesRead} bytes. ({i}/{maxTries})");
                    App.Logger.WriteException(LOG_IDENT, ex);

                    if (ex.GetType() == typeof(ChecksumFailedException))
                    {
                        App.SendStat("packageDownloadState", "httpFail");

                        Frontend.ShowConnectivityDialog(
                            Strings.Dialog_Connectivity_UnableToDownload,
                            String.Format(Strings.Dialog_Connectivity_UnableToDownloadReason, "[https://riftstrap.com/wiki/help/riftstrap-cannot-download-roblox/](https://riftstrap.com/wiki/help/riftstrap-cannot-download-roblox/)"),
                            MessageBoxImage.Error,
                            ex
                        );

                        App.Terminate(ErrorCode.ERROR_CANCELLED);
                    }
                    else if (i >= maxTries)
                        throw;

                    if (File.Exists(package.DownloadPath))
                        File.Delete(package.DownloadPath);

                    _totalDownloadedBytes -= totalBytesRead;
                    UpdateProgressBar();

                    if (ex.GetType() == typeof(IOException) && !packageUrl.StartsWith("http://"))
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Retrying download over HTTP...");
                        packageUrl = packageUrl.Replace("https://", "http://");
                    }
                }
            }
        }

        private void ExtractPackage(Package package, List<string>? files = null)
        {
            const string LOG_IDENT = "Bootstrapper::ExtractPackage";

            string? packageDir = AppData.PackageDirectoryMap.GetValueOrDefault(package.Name);

            if (packageDir is null)
            {
                App.Logger.WriteLine(LOG_IDENT, $"WARNING: {package.Name} was not found in the package map!");
                return;
            }

            string packageFolder = Path.Combine(_latestVersionDirectory, packageDir);
            string? fileFilter = null;

            if (files is not null)
            {
                var regexList = new List<string>();

                foreach (string file in files)
                    regexList.Add("^" + file.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)") + "$");

                fileFilter = String.Join(';', regexList);
            }

            App.Logger.WriteLine(LOG_IDENT, $"Extracting {package.Name}...");

            var fastZip = new FastZip(_fastZipEvents);
            fastZip.RestoreDateTimeOnExtract = false;
            fastZip.RestoreAttributesOnExtract = false;

            fastZip.ExtractZip(package.DownloadPath, packageFolder, fileFilter);

            App.Logger.WriteLine(LOG_IDENT, $"Finished extracting {package.Name}");
        }
    }
}
