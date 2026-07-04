using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Windows;
using System.Windows.Shell;
using System.Windows.Threading;

using Microsoft.Win32;

namespace RiftStrap
{

    public partial class App : Application
    {
#if QA_BUILD
        public const string ProjectName = "RiftStrap-QA";
#else
        public const string ProjectName = "RiftStrap";
#endif
        public const string ProjectOwner = "n3xt3r";
        public const string ProjectRepository = "N3XT3R1337/RiftStrap";
        public const string ProjectDownloadLink = "https://github.com/N3XT3R1337/RiftStrap";
        public const string ProjectHelpLink = "https://github.com/N3XT3R1337/RiftStrap/wiki";
        public const string ProjectSupportLink = "https://github.com/N3XT3R1337/RiftStrap/issues/new";

        public const string RobloxPlayerAppName = "RobloxPlayerBeta";
        public const string RobloxStudioAppName = "RobloxStudioBeta";

        public const string UninstallKey = $@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{ProjectName}";

        public const int TaskbarProgressMaximum = 100;

        public static LaunchSettings LaunchSettings { get; private set; } = null!;

        public static BuildMetadataAttribute BuildMetadata = Assembly.GetExecutingAssembly().GetCustomAttribute<BuildMetadataAttribute>()!;

        public static string Version = Assembly.GetExecutingAssembly().GetName().Version!.ToString()[..^2];

        public static Bootstrapper? Bootstrapper { get; set; } = null!;

        public static bool IsActionBuild => !String.IsNullOrEmpty(BuildMetadata.CommitRef);

        public static bool IsProductionBuild => IsActionBuild && BuildMetadata.CommitRef.StartsWith("tag", StringComparison.Ordinal);

        public static bool IsPlayerInstalled => App.PlayerState.IsSaved && !String.IsNullOrEmpty(App.PlayerState.Prop.VersionGuid);

        public static bool IsStudioInstalled => App.StudioState.IsSaved && !String.IsNullOrEmpty(App.StudioState.Prop.VersionGuid);

        public static readonly MD5 MD5Provider = MD5.Create();

        public static readonly Logger Logger = new();

        public static readonly Dictionary<string, BaseTask> PendingSettingTasks = new();

        public static readonly JsonManager<Settings> Settings = new();

        public static readonly JsonManager<State> State = new();

        public static readonly LazyJsonManager<DistributionState> PlayerState = new(nameof(PlayerState));

        public static readonly LazyJsonManager<DistributionState> StudioState = new(nameof(StudioState));

        public static readonly JsonManager<VersionHistory> VersionHistoryManager = new();

        public static readonly FastFlagManager FastFlags = new();

        public static readonly HttpClient HttpClient = new(
            new HttpClientLoggingHandler(
                new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All }
            )
        );

        private static int _showingExceptionDialog = 0;

        private static string? _webUrl = null;
        public static string WebUrl
        {
            get {
                if (_webUrl != null)
                    return _webUrl;

                string url = ConstructRiftStrapWebUrl();
                if (Settings.Loaded)
                    _webUrl = url;
                return url;
            }
        }

        public static void Terminate(ErrorCode exitCode = ErrorCode.ERROR_SUCCESS)
        {
            int exitCodeNum = (int)exitCode;

            Logger.WriteLine("App::Terminate", $"Terminating with exit code {exitCodeNum} ({exitCode})");

            Environment.Exit(exitCodeNum);
        }

        public static void SoftTerminate(ErrorCode exitCode = ErrorCode.ERROR_SUCCESS)
        {
            int exitCodeNum = (int)exitCode;

            Logger.WriteLine("App::SoftTerminate", $"Terminating with exit code {exitCodeNum} ({exitCode})");

            Current.Dispatcher.Invoke(() => Current.Shutdown(exitCodeNum));
        }

        void GlobalExceptionHandler(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            e.Handled = true;

            Logger.WriteLine("App::GlobalExceptionHandler", "An exception occurred");

            FinalizeExceptionHandling(e.Exception);
        }

        public static void FinalizeExceptionHandling(AggregateException ex)
        {
            foreach (var innerEx in ex.InnerExceptions)
                Logger.WriteException("App::FinalizeExceptionHandling", innerEx);

            FinalizeExceptionHandling(ex.GetBaseException(), false);
        }

        public static void FinalizeExceptionHandling(Exception ex, bool log = true)
        {
            if (log)
                Logger.WriteException("App::FinalizeExceptionHandling", ex);

            if (Interlocked.Exchange(ref _showingExceptionDialog, 1) != 0)
                return;

            SendLog();

            if (Bootstrapper?.Dialog != null)
            {
                if (Bootstrapper.Dialog.TaskbarProgressValue == 0)
                    Bootstrapper.Dialog.TaskbarProgressValue = 1;

                Bootstrapper.Dialog.TaskbarProgressState = TaskbarItemProgressState.Error;
            }

            Frontend.ShowExceptionDialog(ex);

            Terminate(ErrorCode.ERROR_INSTALL_FAILURE);
        }

        public static string ConstructRiftStrapWebUrl()
        {

            if (Settings.Prop.WebEnvironment == WebEnvironment.Production || !Settings.Prop.DeveloperMode)
                return "services.riftstrap.com";

            string? sub = Settings.Prop.WebEnvironment.GetDescription();
            return $"services-{sub}.riftstrap.com";
        }

        public static bool CanSendLogs()
        {

            if (!Settings.Prop.DeveloperMode || Settings.Prop.WebEnvironment == WebEnvironment.Production)
                return IsProductionBuild;

            return true;
        }

        public static async Task<GithubRelease?> GetLatestRelease()
        {
            const string LOG_IDENT = "App::GetLatestRelease";

            try
            {
                var releaseInfo = await Http.GetJson<GithubRelease>($"https://api.github.com/repos/{ProjectRepository}/releases/latest");

                if (releaseInfo is null || releaseInfo.Assets is null)
                {
                    Logger.WriteLine(LOG_IDENT, "Encountered invalid data");
                    return null;
                }

                return releaseInfo;
            }
            catch (Exception ex)
            {
                Logger.WriteException(LOG_IDENT, ex);
            }

            return null;
        }

        public static void SendStat(string key, string value)
        {
            // Telemetry host (services.riftstrap.com) is not provisioned — no-op to avoid per-call
            // DNS failures spamming the log. Restore the HttpClient call here when a real host exists.
        }

        public static void SendLog()
        {
            // Telemetry host (services.riftstrap.com) is not provisioned — no-op (see SendStat).
        }

        public static void AssertWindowsOSVersion()
        {
            const string LOG_IDENT = "App::AssertWindowsOSVersion";

            int major = Environment.OSVersion.Version.Major;
            if (major < 10)
            {
                Logger.WriteLine(LOG_IDENT, $"Detected unsupported Windows version ({Environment.OSVersion.Version}).");

                if (!LaunchSettings.QuietFlag.Active)
                    Frontend.ShowMessageBox(Strings.App_OSDeprecation_Win7_81, MessageBoxImage.Error);

                Terminate(ErrorCode.ERROR_INVALID_FUNCTION);
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            const string LOG_IDENT = "App::OnStartup";

            Locale.Initialize();

            base.OnStartup(e);

            Logger.WriteLine(LOG_IDENT, $"Starting {ProjectName} v{Version}");

            string userAgent = $"{ProjectName}/{Version}";

            if (IsActionBuild)
            {
                Logger.WriteLine(LOG_IDENT, $"Compiled {BuildMetadata.Timestamp.ToFriendlyString()} from commit {BuildMetadata.CommitHash} ({BuildMetadata.CommitRef})");

                if (IsProductionBuild)
                    userAgent += $" (Production)";
                else
                    userAgent += $" (Artifact {BuildMetadata.CommitHash}, {BuildMetadata.CommitRef})";
            }
            else
            {
                Logger.WriteLine(LOG_IDENT, $"Compiled {BuildMetadata.Timestamp.ToFriendlyString()} from {BuildMetadata.Machine}");

#if QA_BUILD
                userAgent += " (QA)";
#else
                userAgent += $" (Build {Convert.ToBase64String(Encoding.UTF8.GetBytes(BuildMetadata.Machine))})";
#endif
            }

            Logger.WriteLine(LOG_IDENT, $"OSVersion: {Environment.OSVersion}");

            Logger.WriteLine(LOG_IDENT, $"Loaded from {Paths.Process}");
            Logger.WriteLine(LOG_IDENT, $"Temp path is {Paths.Temp}");
            Logger.WriteLine(LOG_IDENT, $"WindowsStartMenu path is {Paths.WindowsStartMenu}");

            ApplicationConfiguration.Initialize();

            HttpClient.Timeout = TimeSpan.FromSeconds(30);
            HttpClient.DefaultRequestHeaders.Add("User-Agent", userAgent);

            LaunchSettings = new LaunchSettings(e.Args);

            using var uninstallKey = Registry.CurrentUser.OpenSubKey(UninstallKey);
            string? installLocation = null;
            bool fixInstallLocation = false;

            if (uninstallKey?.GetValue("InstallLocation") is string value)
            {
                if (Directory.Exists(value))
                {
                    installLocation = value;
                }
                else
                {

                    var match = Regex.Match(value, @"^[a-zA-Z]:\\Users\\([^\\]+)", RegexOptions.IgnoreCase);

                    if (match.Success)
                    {
                        string newLocation = value.Replace(match.Value, Paths.UserProfile, StringComparison.InvariantCultureIgnoreCase);

                        if (Directory.Exists(newLocation))
                        {
                            installLocation = newLocation;
                            fixInstallLocation = true;
                        }
                    }
                }
            }

            if (installLocation is null && Directory.GetParent(Paths.Process)?.FullName is string processDir)
            {
                var files = Directory.GetFiles(processDir).Select(x => Path.GetFileName(x)).ToArray();

                if (files.Length <= 3 && files.Contains("Settings.json") && files.Contains("State.json"))
                {
                    installLocation = processDir;
                    fixInstallLocation = true;
                }
            }

            if (fixInstallLocation && installLocation is not null)
            {
                var installer = new Installer
                {
                    InstallLocation = installLocation,
                    IsImplicitInstall = true
                };

                if (installer.CheckInstallLocation())
                {
                    Logger.WriteLine(LOG_IDENT, $"Changing install location to '{installLocation}'");
                    installer.DoInstall();
                }
                else
                {

                    installLocation = null;
                }
            }

            if (installLocation is null)
            {
                Logger.Initialize(true);
                Logger.WriteLine(LOG_IDENT, "Not installed, launching the installer");
                AssertWindowsOSVersion();
                LaunchHandler.LaunchInstaller();
            }
            else
            {
                Paths.Initialize(installLocation);

                Logger.WriteLine(LOG_IDENT, "Entering main logic");

                if (Paths.Process != Paths.Application && !File.Exists(Paths.Application))
                {
                    Logger.WriteLine(LOG_IDENT, "Copying to install directory");
                    File.Copy(Paths.Process, Paths.Application);
                }

                Logger.Initialize(LaunchSettings.UninstallFlag.Active);

                if (!Logger.Initialized && !Logger.NoWriteMode)
                {
                    Logger.WriteLine(LOG_IDENT, "Possible duplicate launch detected, terminating.");
                    Terminate();
                }

                Settings.Load();
                State.Load();
                FastFlags.Load();
                VersionHistoryManager.Load();

                if (!Locale.SupportedLocales.ContainsKey(Settings.Prop.Locale))
                {
                    Settings.Prop.Locale = "nil";
                    Settings.Save();
                }

                Logger.WriteLine(LOG_IDENT, $"Developer mode: {Settings.Prop.DeveloperMode}");
                Logger.WriteLine(LOG_IDENT, $"Web environment: {Settings.Prop.WebEnvironment}");

                Locale.Set(Settings.Prop.Locale);

                ForceMonochromeAccent();

                _ = Task.Run(() => new Features.LaunchOptimizer.LaunchOptService().WarmupAsync());

                if (!LaunchSettings.BypassUpdateCheck)
                    Installer.HandleUpgrade();

                LaunchHandler.ProcessLaunchArgs();
            }

            Logger.WriteLine(LOG_IDENT, "Startup finished");
        }

        private static void ForceMonochromeAccent()
        {
            var res = Application.Current.Resources;
            var white = System.Windows.Media.Color.FromRgb(0xFA, 0xFA, 0xFA);
            var white90 = System.Windows.Media.Color.FromArgb(0xD9, 0xFF, 0xFF, 0xFF);
            var white80 = System.Windows.Media.Color.FromArgb(0xB3, 0xFF, 0xFF, 0xFF);

            res["SystemAccentColor"] = white;
            res["SystemAccentColorPrimary"] = white;
            res["SystemAccentColorSecondary"] = white90;
            res["SystemAccentColorTertiary"] = white80;

            var whiteBrush = new System.Windows.Media.SolidColorBrush(white);
            var white90Brush = new System.Windows.Media.SolidColorBrush(white90);
            var white80Brush = new System.Windows.Media.SolidColorBrush(white80);

            whiteBrush.Freeze();
            white90Brush.Freeze();
            white80Brush.Freeze();

            res["SystemAccentColorBrush"] = whiteBrush;
            res["SystemAccentColorPrimaryBrush"] = whiteBrush;
            res["SystemAccentColorSecondaryBrush"] = white90Brush;
            res["SystemAccentColorTertiaryBrush"] = white80Brush;
            res["AccentTextFillColorPrimaryBrush"] = white90Brush;
            res["AccentTextFillColorSecondaryBrush"] = white80Brush;
            res["AccentTextFillColorTertiaryBrush"] = whiteBrush;
            res["AccentFillColorDefaultBrush"] = whiteBrush;
            res["AccentFillColorSecondaryBrush"] = white90Brush;
            res["AccentFillColorTertiaryBrush"] = white80Brush;
            res["SystemFillColorAttentionBrush"] = whiteBrush;
            res["AccentFillColorSelectedTextBackgroundBrush"] = whiteBrush;
        }
    }
}
