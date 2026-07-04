using RiftStrap.AppData;
using System.ComponentModel;

namespace RiftStrap
{
    static class Utilities
    {
        public static void ShellExecute(string website)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = website,
                    UseShellExecute = true
                });
            }
            catch (Win32Exception ex)
            {

                if (ex.NativeErrorCode != (int)ErrorCode.CO_E_APPNOTFOUND)
                    throw;

                Process.Start(new ProcessStartInfo
                {
                    FileName = "rundll32.exe",
                    Arguments = $"shell32,OpenAs_RunDLL {website}"
                });
            }
        }

        public static Version GetVersionFromString(string version)
        {
            if (version.StartsWith('v'))
                version = version[1..];

            // Strip SemVer build metadata ('+build') and prerelease suffix ('-beta1') so that
            // e.g. '2.12.0-beta1' parses as '2.12.0'. Without this, System.Version.Parse throws on
            // the '-'/'+' and CompareVersions falls back to Equal, silently skipping the update.
            int idx = version.IndexOf('+');
            if (idx != -1)
                version = version[..idx];

            idx = version.IndexOf('-');
            if (idx != -1)
                version = version[..idx];

            return new Version(version);
        }

        public static VersionComparison CompareVersions(string versionStr1, string versionStr2)
        {
            try
            {
                var version1 = GetVersionFromString(versionStr1);
                var version2 = GetVersionFromString(versionStr2);

                return (VersionComparison)version1.CompareTo(version2);
            }
            catch (Exception)
            {
                // a malformed/non-numeric version string must NOT crash the upgrade flow; treat as Equal.
                App.Logger.WriteLine("Utilities::CompareVersions", $"Could not compare versions (versionStr1={versionStr1} versionStr2={versionStr2}); treating as Equal");
                return VersionComparison.Equal;
            }
        }

        public static Version? ParseVersionSafe(string versionStr)
        {
            const string LOG_IDENT = "Utilities::ParseVersionSafe";

            if (!Version.TryParse(versionStr, out Version? version))
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to convert {versionStr} to a valid Version type.");
                return version;
            }

            return version;
        }

        public static string GetRobloxVersionStr(IAppData data)
        {
            string playerLocation = data.ExecutablePath;

            if (!File.Exists(playerLocation))
                return "";

            FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(playerLocation);

            if (versionInfo.ProductVersion is null)
                return "";

            return versionInfo.ProductVersion.Replace(", ", ".");
        }

        public static string GetRobloxVersionStr(bool studio)
        {
            IAppData data = studio ? new RobloxStudioData() : new RobloxPlayerData();

            return GetRobloxVersionStr(data);
        }

        public static Version? GetRobloxVersion(IAppData data)
        {
            string str = GetRobloxVersionStr(data);
            return ParseVersionSafe(str);
        }

        public static Process[] GetProcessesSafe()
        {
            const string LOG_IDENT = "Utilities::GetProcessesSafe";

            try
            {
                return Process.GetProcesses();
            }
            catch (ArithmeticException ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Unable to fetch processes!");
                App.Logger.WriteException(LOG_IDENT, ex);
                return Array.Empty<Process>();
            }
        }

        // Lightweight, leak-free liveness check (disposes the Process handle). Prefer this over
        // GetProcessesSafe().Any(...) in poll loops, which leaks a handle for every process each tick.
        public static bool IsProcessRunning(int pid)
        {
            try
            {
                using var p = Process.GetProcessById(pid);
                return !p.HasExited;
            }
            catch (ArgumentException)
            {
                return false;   // no process with that id -> not running
            }
            catch
            {
                return true;    // indeterminate -> assume running so we don't break a wait loop early
            }
        }

        public static bool DoesMutexExist(string name)
        {
            try
            {
                Mutex.OpenExisting(name).Close();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void KillBackgroundUpdater()
        {
            using EventWaitHandle handle = new EventWaitHandle(false, EventResetMode.AutoReset, "RiftStrap-BackgroundUpdaterKillEvent");
            handle.Set();
        }
    }
}
