using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace RiftStrap.Utility
{
    internal static class Filesystem
    {
        internal static long GetFreeDiskSpace(string path)
        {
            foreach (var drive in DriveInfo.GetDrives())
            {

                if (path.ToUpperInvariant().StartsWith(drive.Name))
                    return drive.AvailableFreeSpace;
            }

            return -1;
        }

        internal static void AssertReadOnly(string filePath)
        {
            var fileInfo = new FileInfo(filePath);

            if (!fileInfo.Exists || !fileInfo.IsReadOnly)
                return;

            fileInfo.IsReadOnly = false;
            App.Logger.WriteLine("Filesystem::AssertReadOnly", $"The following file was set as read-only: {filePath}");
        }

        internal static void AssertReadOnlyDirectory(string directoryPath)
        {
            var directory = new DirectoryInfo(directoryPath);

            if (!directory.Exists)   // setting Attributes on a missing directory throws
                return;

            // clear ONLY the read-only bit; setting FileAttributes.Normal clobbered Hidden/System/etc.
            try { directory.Attributes &= ~FileAttributes.ReadOnly; } catch { }

            foreach (var info in directory.GetFileSystemInfos("*", SearchOption.AllDirectories))
            {
                try { info.Attributes &= ~FileAttributes.ReadOnly; } catch { }
            }

            App.Logger.WriteLine("Filesystem::AssertReadOnlyDirectory", $"The following directory was cleared of read-only: {directoryPath}");
        }
    }
}
