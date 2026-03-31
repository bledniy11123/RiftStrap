namespace RiftStrap.Features.ScreenshotManager
{

    public class ScreenshotService : IDisposable
    {
        private static readonly string RobloxScreenshotsDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Roblox");

        private static readonly string OrganizedDir = Path.Combine(Paths.Base, "Screenshots");

        private FileSystemWatcher? _watcher;

        public event Action<string>? OnNewScreenshot;

        public ScreenshotService()
        {
            Directory.CreateDirectory(OrganizedDir);
        }

        public List<ScreenshotInfo> GetAll(int limit = 50)
        {
            var screenshots = new List<ScreenshotInfo>();

            if (Directory.Exists(RobloxScreenshotsDir))
            {
                foreach (var file in Directory.GetFiles(RobloxScreenshotsDir, "*.png")
                    .Concat(Directory.GetFiles(RobloxScreenshotsDir, "*.jpg"))
                    .OrderByDescending(File.GetCreationTime)
                    .Take(limit))
                {
                    screenshots.Add(new ScreenshotInfo
                    {
                        Path = file,
                        FileName = Path.GetFileName(file),
                        CreatedAt = File.GetCreationTime(file),
                        SizeBytes = new FileInfo(file).Length,
                    });
                }
            }

            return screenshots;
        }

        public (int Count, long SizeBytes) GetStats()
        {
            if (!Directory.Exists(RobloxScreenshotsDir))
                return (0, 0);

            var files = Directory.GetFiles(RobloxScreenshotsDir, "*.*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".png") || f.EndsWith(".jpg"))
                .ToList();

            return (files.Count, files.Sum(f => new FileInfo(f).Length));
        }

        public void StartWatching()
        {
            if (!Directory.Exists(RobloxScreenshotsDir))
                return;

            _watcher = new FileSystemWatcher(RobloxScreenshotsDir)
            {
                Filter = "*.png",
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime,
                EnableRaisingEvents = true,
            };

            _watcher.Created += (_, e) =>
            {
                OnNewScreenshot?.Invoke(e.FullPath);
                App.Logger.WriteLine("Screenshots", $"New screenshot: {e.Name}");
            };
        }

        public static void OpenScreenshot(string path)
        {
            if (File.Exists(path))
                Utilities.ShellExecute(path);
        }

        public static void OpenFolder()
        {
            if (Directory.Exists(RobloxScreenshotsDir))
                Utilities.ShellExecute(RobloxScreenshotsDir);
        }

        public void Dispose()
        {
            _watcher?.Dispose();
            _watcher = null;
            GC.SuppressFinalize(this);
        }

        public int CleanOlderThan(int days)
        {
            if (!Directory.Exists(RobloxScreenshotsDir))
                return 0;

            var cutoff = DateTime.Now.AddDays(-days);
            int deleted = 0;

            foreach (var file in Directory.GetFiles(RobloxScreenshotsDir, "*.*", SearchOption.AllDirectories))
            {
                if (File.GetCreationTime(file) < cutoff)
                {
                    try { File.Delete(file); deleted++; }
                    catch { }
                }
            }

            return deleted;
        }
    }

    public class ScreenshotInfo
    {
        public string Path { get; set; } = "";
        public string FileName { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public long SizeBytes { get; set; }

        public string SizeText => SizeBytes > 1024 * 1024
            ? $"{SizeBytes / 1024.0 / 1024:F1} MB"
            : $"{SizeBytes / 1024.0:F0} KB";

        public string DateText => CreatedAt.ToString("dd MMM yyyy HH:mm");
    }
}
