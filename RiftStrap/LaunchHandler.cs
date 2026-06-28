using System.Windows;

using Windows.Win32;
using Windows.Win32.Foundation;

using RiftStrap.UI.Elements.Dialogs;
using RiftStrap.Enums;

namespace RiftStrap
{
    public static class LaunchHandler
    {
        public static void ProcessNextAction(NextAction action, bool isUnfinishedInstall = false)
        {
            const string LOG_IDENT = "LaunchHandler::ProcessNextAction";

            switch (action)
            {
                case NextAction.LaunchSettings:
                    App.Logger.WriteLine(LOG_IDENT, "Opening settings");
                    LaunchSettings();
                    break;

                case NextAction.LaunchRoblox:
                    App.Logger.WriteLine(LOG_IDENT, "Opening Roblox");
                    LaunchRoblox(LaunchMode.Player);
                    break;

                case NextAction.LaunchRobloxStudio:
                    App.Logger.WriteLine(LOG_IDENT, "Opening Roblox Studio");
                    LaunchRoblox(LaunchMode.Studio);
                    break;

                default:
                    App.Logger.WriteLine(LOG_IDENT, "Closing");
                    App.Terminate(isUnfinishedInstall ? ErrorCode.ERROR_INSTALL_USEREXIT : ErrorCode.ERROR_SUCCESS);
                    break;
            }
        }

        public static void ProcessLaunchArgs()
        {
            const string LOG_IDENT = "LaunchHandler::ProcessLaunchArgs";

            if (App.LaunchSettings.UninstallFlag.Active)
            {
                App.Logger.WriteLine(LOG_IDENT, "Opening uninstaller");
                LaunchUninstaller();
            }
            else if (App.LaunchSettings.MenuFlag.Active)
            {
                App.Logger.WriteLine(LOG_IDENT, "Opening settings");
                LaunchSettings();
            }
            else if (App.LaunchSettings.WatcherFlag.Active)
            {
                App.Logger.WriteLine(LOG_IDENT, "Opening watcher");
                LaunchWatcher();
            }
            else if (App.LaunchSettings.MutexHolderFlag.Active)
            {
                App.Logger.WriteLine(LOG_IDENT, "Opening multi-instance mutex holder");
                LaunchMultiInstanceLock();
            }
            else if (App.LaunchSettings.BackgroundUpdaterFlag.Active)
            {
                App.Logger.WriteLine(LOG_IDENT, "Opening background updater");
                LaunchBackgroundUpdater();
            }
            else if (App.LaunchSettings.RobloxLaunchMode != LaunchMode.None)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Opening bootstrapper ({App.LaunchSettings.RobloxLaunchMode})");
                LaunchRoblox(App.LaunchSettings.RobloxLaunchMode);
            }
            else if (!App.LaunchSettings.QuietFlag.Active)
            {
                App.Logger.WriteLine(LOG_IDENT, "Opening menu");
                LaunchMenu();
            }
            else
            {
                App.Logger.WriteLine(LOG_IDENT, "Closing - quiet flag active");
                App.Terminate();
            }
        }

        public static void LaunchInstaller()
        {
            using var interlock = new InterProcessLock("Installer");

            if (!interlock.IsAcquired)
            {
                Frontend.ShowMessageBox(Strings.Dialog_AlreadyRunning_Installer, MessageBoxImage.Stop);
                App.Terminate();
                return;
            }

            if (App.LaunchSettings.UninstallFlag.Active)
            {
                Frontend.ShowMessageBox(Strings.Bootstrapper_FirstRunUninstall, MessageBoxImage.Error);
                App.Terminate(ErrorCode.ERROR_INVALID_FUNCTION);
                return;
            }

            if (App.LaunchSettings.QuietFlag.Active)
            {
                var installer = new Installer();

                if (!installer.CheckInstallLocation())
                    App.Terminate(ErrorCode.ERROR_INSTALL_FAILURE);

                installer.DoInstall();

                interlock.Dispose();

                ProcessLaunchArgs();
            }
            else
            {
#if QA_BUILD
                Frontend.ShowMessageBox("You are about to install a QA build of RiftStrap. The red window border indicates that this is a QA build.\n\nQA builds are handled completely separately of your standard installation, like a virtual environment.", MessageBoxImage.Information);
#endif

                new LanguageSelectorDialog().ShowDialog();

                var installer = new UI.Elements.Installer.MainWindow();
                installer.ShowDialog();

                interlock.Dispose();

                ProcessNextAction(installer.CloseAction, !installer.Finished);
            }

        }

        public static void LaunchUninstaller()
        {
            using var interlock = new InterProcessLock("Uninstaller");

            if (!interlock.IsAcquired)
            {
                Frontend.ShowMessageBox(Strings.Dialog_AlreadyRunning_Uninstaller, MessageBoxImage.Stop);
                App.Terminate();
                return;
            }

            bool confirmed = false;
            bool keepData = true;

            if (App.LaunchSettings.QuietFlag.Active)
            {
                confirmed = true;
            }
            else
            {
                var dialog = new UninstallerDialog();
                dialog.ShowDialog();

                confirmed = dialog.Confirmed;
                keepData = dialog.KeepData;
            }

            if (!confirmed)
            {
                App.Terminate();
                return;
            }

            Installer.DoUninstall(keepData);

            Frontend.ShowMessageBox(Strings.Bootstrapper_SuccessfullyUninstalled, MessageBoxImage.Information);

            App.Terminate();
        }

        public static void LaunchSettings()
        {
            const string LOG_IDENT = "LaunchHandler::LaunchSettings";

            using var interlock = new InterProcessLock("Settings");

            if (interlock.IsAcquired)
            {
                bool showAlreadyRunningWarning = Process.GetProcessesByName(App.ProjectName).Length > 1;

                var window = new UI.Elements.Settings.MainWindow(showAlreadyRunningWarning);

                window.ShowDialog();
            }
            else
            {
                App.Logger.WriteLine(LOG_IDENT, "Found an already existing menu window");

                var process = Utilities.GetProcessesSafe().Where(x => x.MainWindowTitle == Strings.Menu_Title).FirstOrDefault();

                if (process is not null)
                    PInvoke.SetForegroundWindow((HWND)process.MainWindowHandle);

                App.Terminate();
            }
        }

        public static void LaunchMenu()
        {
            var dialog = new LaunchMenuDialog();
            dialog.ShowDialog();

            ProcessNextAction(dialog.CloseAction);
        }

        public static void LaunchRoblox(LaunchMode launchMode)
        {
            const string LOG_IDENT = "LaunchHandler::LaunchRoblox";

            if (launchMode == LaunchMode.None)
                throw new InvalidOperationException("No Roblox launch mode set");

            if (!File.Exists(Path.Combine(Paths.System, "mfplat.dll")))
            {
                Frontend.ShowMessageBox(Strings.Bootstrapper_WMFNotFound, MessageBoxImage.Error);

                if (!App.LaunchSettings.QuietFlag.Active)
                    Utilities.ShellExecute("https://support.microsoft.com/en-us/topic/media-feature-pack-list-for-windows-n-editions-c1c6fffa-d052-8338-7a79-a4bb980a700a");

                App.Terminate(ErrorCode.ERROR_FILE_NOT_FOUND);
            }

            if (App.Settings.Prop.ConfirmLaunches && !App.Settings.Prop.MultiInstanceLaunching && Mutex.TryOpenExisting("ROBLOX_singletonMutex", out var _))
            {

                var result = Frontend.ShowMessageBox(Strings.Bootstrapper_ConfirmLaunch, MessageBoxImage.Warning, MessageBoxButton.YesNo);

                if (result != MessageBoxResult.Yes)
                {
                    App.Terminate();
                    return;
                }
            }

            App.Logger.WriteLine(LOG_IDENT, "Initializing bootstrapper");
            App.Bootstrapper = new Bootstrapper(launchMode);
            IBootstrapperDialog? dialog = null;

            if (!App.LaunchSettings.QuietFlag.Active)
            {
                App.Logger.WriteLine(LOG_IDENT, "Initializing bootstrapper dialog");
                dialog = App.Settings.Prop.BootstrapperStyle.GetNew();
                App.Bootstrapper.Dialog = dialog;
                dialog.Bootstrapper = App.Bootstrapper;
            }

            Task.Run(App.Bootstrapper.Run).ContinueWith(t =>
            {
                App.Logger.WriteLine(LOG_IDENT, "Bootstrapper task has finished");

                if (t.IsFaulted)
                {
                    App.Logger.WriteLine(LOG_IDENT, "An exception occurred when running the bootstrapper");

                    if (t.Exception is not null)
                        App.FinalizeExceptionHandling(t.Exception);
                }

                App.Terminate();
            });

            dialog?.ShowBootstrapper();

            App.Logger.WriteLine(LOG_IDENT, "Exiting");
        }

        public static void LaunchWatcher()
        {
            const string LOG_IDENT = "LaunchHandler::LaunchWatcher";

            var watcher = new Watcher();

            Task.Run(watcher.Run).ContinueWith(t =>
            {
                App.Logger.WriteLine(LOG_IDENT, "Watcher task has finished");

                watcher.Dispose();

                if (t.IsFaulted)
                {
                    App.Logger.WriteLine(LOG_IDENT, "An exception occurred when running the watcher");

                    if (t.Exception is not null)
                        App.FinalizeExceptionHandling(t.Exception);
                }

                App.Terminate();
            });
        }

        public static void LaunchMultiInstanceLock()
        {
            // Dedicated diagnostic file (the shared logger goes into NoWriteMode when several RiftStrap
            // processes start at once, so the holder's own log lines get dropped — this never does).
            string diagPath;
            try { diagPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RiftStrap", "Logs", "mutexholder-diag.log"); }
            catch { diagPath = Path.Combine(Path.GetTempPath(), "riftstrap-mutexholder.log"); }
            void Diag(string m) { try { File.AppendAllText(diagPath, $"{Environment.TickCount} | {m}\r\n"); } catch { } }

            Diag("=== holder process started ===");

            // Singleton: only one holder should exist at a time (across concurrent launches).
            using var interlock = new InterProcessLock("MultiInstanceMutex");
            if (!interlock.IsAcquired)
            {
                // Another holder already owns ROBLOX_singletonMutex — just unblock the bootstrapper.
                try { using var ev = new EventWaitHandle(false, EventResetMode.ManualReset, "RiftStrap-MultiInstanceReady"); ev.Set(); } catch { }
                Diag("another holder already owns the IPL -> signalled ready and exit (normal for 2nd+ launch)");
                App.Terminate();
                return;
            }

            Mutex? mutex = null;
            EventWaitHandle? ready = null;
            try
            {
                // Own ROBLOX_singletonMutex from a NON-Roblox process BEFORE any client starts, so no
                // Roblox client owns it and they all coexist (same mechanism as ROBLOX_MULTI/fishstrap).
                mutex = new Mutex(true, "ROBLOX_singletonMutex", out bool created);
                bool owned = created;
                if (!created)
                {
                    // initiallyOwned only takes effect when WE create the object; if it already
                    // exists (a leftover/abandoned handle) we must explicitly ACQUIRE ownership,
                    // otherwise Roblox can still claim it and kill the other client.
                    try { owned = mutex.WaitOne(TimeSpan.FromSeconds(5)); }
                    catch (AbandonedMutexException) { owned = true; Diag("acquired abandoned mutex"); }
                }
                Diag($"ROBLOX_singletonMutex created={created} owned={owned}");

                // Tell the launching bootstrapper the mutex is owned; keep the handle open so the
                // event stays set for later launches while this holder lives.
                ready = new EventWaitHandle(false, EventResetMode.ManualReset, "RiftStrap-MultiInstanceReady");
                ready.Set();
                Diag("signalled ready");

                // Wait up to 60s for the first client.
                for (int i = 0; i < 60 && !IsAnyRobloxRunning(); i++)
                    Thread.Sleep(1000);
                Diag($"first-client grace done, robloxRunning={IsAnyRobloxRunning()}");

                // Hold the mutex while ANY client runs. Only release after Roblox has been gone for
                // several consecutive checks (~12s) so a brief process gap during Roblox's own
                // restart/bootstrap never releases the mutex and lets a client claim it.
                int goneStreak = 0;
                while (goneStreak < 6)
                {
                    goneStreak = IsAnyRobloxRunning() ? 0 : goneStreak + 1;
                    Thread.Sleep(2000);
                }
                Diag("no clients for ~12s -> releasing singleton mutex");
            }
            catch (Exception ex)
            {
                Diag("error: " + ex.Message);
            }
            finally
            {
                try { mutex?.ReleaseMutex(); } catch { }
                mutex?.Dispose();
                ready?.Dispose();
            }

            Diag("=== holder process exiting ===");
            App.Terminate();
        }

        private static bool IsAnyRobloxRunning()
        {
            foreach (var name in new[] { "RobloxPlayerBeta", "RobloxStudioBeta" })
            {
                var procs = Process.GetProcessesByName(name);
                try { if (procs.Length > 0) return true; }
                finally { foreach (var p in procs) p.Dispose(); }   // GetProcessesByName leaks handles otherwise
            }
            return false;
        }

        public static void LaunchBackgroundUpdater()
        {
            const string LOG_IDENT = "LaunchHandler::LaunchBackgroundUpdater";

            App.LaunchSettings.QuietFlag.Active = true;
            App.LaunchSettings.NoLaunchFlag.Active = true;

            if (!Enum.TryParse(App.LaunchSettings.BackgroundUpdaterFlag.Data, out LaunchMode launchMode))
                throw new ApplicationException($"Invalid launch mode arg ({App.LaunchSettings.BackgroundUpdaterFlag.Data})");

            if (launchMode != LaunchMode.Player && launchMode != LaunchMode.Studio)
                throw new ApplicationException($"Unsupported launch mode {launchMode} provided");

            App.Logger.WriteLine(LOG_IDENT, "Initializing bootstrapper");
            App.Bootstrapper = new Bootstrapper(launchMode)
            {
                MutexNamePrefix = "RiftStrap-BackgroundUpdater",
                QuitIfMutexExists = true
            };

            CancellationTokenSource cts = new CancellationTokenSource();

            Task.Run(() =>
            {
                App.Logger.WriteLine(LOG_IDENT, "Started event waiter");
                using (EventWaitHandle handle = new EventWaitHandle(false, EventResetMode.AutoReset, "RiftStrap-BackgroundUpdaterKillEvent"))
                    handle.WaitOne();

                App.Logger.WriteLine(LOG_IDENT, "Received close event, killing it all!");
                App.Bootstrapper.Cancel();
            }, cts.Token);

            Task.Run(App.Bootstrapper.Run).ContinueWith(t =>
            {
                App.Logger.WriteLine(LOG_IDENT, "Bootstrapper task has finished");
                cts.Cancel();

                if (t.IsFaulted)
                {
                    App.Logger.WriteLine(LOG_IDENT, "An exception occurred when running the bootstrapper");

                    if (t.Exception is not null)
                        App.FinalizeExceptionHandling(t.Exception);
                }

                App.Terminate();
            });

            App.Logger.WriteLine(LOG_IDENT, "Exiting");
        }
    }
}
