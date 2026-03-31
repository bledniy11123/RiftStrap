using System.Runtime.Loader;

namespace RiftStrap.Features.Plugins
{

    public class PluginHost : IDisposable
    {
        private static readonly string PluginsDir = Path.Combine(Paths.Base, "Plugins");
        private readonly List<LoadedPlugin> _plugins = new();
        private readonly PluginContext _sharedContext = new();

        public IReadOnlyList<LoadedPlugin> Plugins => _plugins;

        public PluginHost()
        {
            Directory.CreateDirectory(PluginsDir);
        }

        public async Task LoadAllAsync()
        {
            foreach (var dir in Directory.GetDirectories(PluginsDir))
            {
                try
                {
                    await LoadPluginAsync(dir);
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine("PluginHost", $"Failed to load plugin from {dir}: {ex.Message}");
                }
            }

            App.Logger.WriteLine("PluginHost", $"Loaded {_plugins.Count} plugins");
        }

        public async Task<LoadedPlugin?> LoadPluginAsync(string pluginDir)
        {
            var manifestPath = Path.Combine(pluginDir, "plugin.json");
            if (!File.Exists(manifestPath))
                return null;

            var json = await File.ReadAllTextAsync(manifestPath);
            var manifest = JsonSerializer.Deserialize<PluginManifest>(json);
            if (manifest == null || string.IsNullOrEmpty(manifest.EntryDll))
                return null;

            if (!IsVersionCompatible(manifest.MinLauncherVersion))
            {
                App.Logger.WriteLine("PluginHost", $"Plugin {manifest.Name} requires v{manifest.MinLauncherVersion}+, skipping");
                return null;
            }

            var dllPath = Path.Combine(pluginDir, manifest.EntryDll);
            if (!File.Exists(dllPath))
                return null;

            var loadContext = new PluginLoadContext(dllPath);
            var assembly = loadContext.LoadFromAssemblyPath(Path.GetFullPath(dllPath));

            var pluginType = assembly.GetTypes()
                .FirstOrDefault(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

            if (pluginType == null)
            {
                loadContext.Unload();
                return null;
            }

            var plugin = (IPlugin?)Activator.CreateInstance(pluginType);
            if (plugin == null)
            {
                loadContext.Unload();
                return null;
            }

            var dataPath = Path.Combine(PluginsDir, manifest.Id, "data");
            Directory.CreateDirectory(dataPath);

            var context = new PluginContext { DataPath = dataPath };

            var loaded = new LoadedPlugin
            {
                Manifest = manifest,
                Instance = plugin,
                LoadContext = loadContext,
                Context = context,
                IsEnabled = true,
            };

            try
            {
                await plugin.OnActivateAsync(context);
                loaded.IsActive = true;
                App.Logger.WriteLine("PluginHost", $"Activated plugin: {manifest.Name} v{manifest.Version}");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("PluginHost", $"Plugin {manifest.Name} activation failed: {ex.Message}");
                loaded.IsActive = false;
            }

            _plugins.Add(loaded);
            return loaded;
        }

        public async Task UnloadPluginAsync(string pluginId)
        {
            var loaded = _plugins.FirstOrDefault(p => p.Manifest.Id == pluginId);
            if (loaded == null) return;

            try
            {
                await loaded.Instance.OnDeactivateAsync();
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("PluginHost", $"Plugin {pluginId} deactivation error: {ex.Message}");
            }

            loaded.LoadContext.Unload();
            _plugins.Remove(loaded);
        }

        public async Task UnloadAllAsync()
        {
            foreach (var plugin in _plugins.ToList())
            {
                await UnloadPluginAsync(plugin.Manifest.Id);
            }
        }

        public void NotifyGameJoin(long placeId, long universeId)
        {
            foreach (var p in _plugins.Where(p => p.IsActive))
                p.Context.RaiseGameJoin(placeId, universeId);
        }

        public void NotifyGameLeave()
        {
            foreach (var p in _plugins.Where(p => p.IsActive))
                p.Context.RaiseGameLeave();
        }

        public void Dispose()
        {
            Task.Run(UnloadAllAsync).Wait(TimeSpan.FromSeconds(5));
            GC.SuppressFinalize(this);
        }

        private static bool IsVersionCompatible(string minVersion)
        {
            if (string.IsNullOrEmpty(minVersion)) return true;
            return Utilities.CompareVersions(App.Version, minVersion) >= 0;
        }
    }

    public class LoadedPlugin
    {
        public PluginManifest Manifest { get; set; } = new();
        public IPlugin Instance { get; set; } = null!;
        public PluginLoadContext LoadContext { get; set; } = null!;
        public PluginContext Context { get; set; } = null!;
        public bool IsEnabled { get; set; }
        public bool IsActive { get; set; }
    }

    public class PluginLoadContext : AssemblyLoadContext
    {
        private readonly System.Runtime.Loader.AssemblyDependencyResolver _resolver;

        public PluginLoadContext(string pluginPath) : base(isCollectible: true)
        {
            _resolver = new AssemblyDependencyResolver(pluginPath);
        }

        protected override System.Reflection.Assembly? Load(System.Reflection.AssemblyName assemblyName)
        {
            var path = _resolver.ResolveAssemblyToPath(assemblyName);
            return path != null ? LoadFromAssemblyPath(path) : null;
        }
    }

    public class PluginContext : IPluginContext
    {
        public string DataPath { get; set; } = "";

        public event Action<long, long>? OnGameJoin;
        public event Action? OnGameLeave;

        public IReadOnlyDictionary<string, object> GetFastFlags()
            => new Dictionary<string, object>(App.FastFlags.Prop);

        public void SetFastFlag(string key, object? value)
            => App.FastFlags.SetValue(key, value);

        public void ShowToast(string title, string message)
        {

            App.Logger.WriteLine("Plugin", $"Toast: {title} — {message}");
        }

        public void Log(string message)
            => App.Logger.WriteLine("Plugin", message);

        internal void RaiseGameJoin(long placeId, long universeId) => OnGameJoin?.Invoke(placeId, universeId);
        internal void RaiseGameLeave() => OnGameLeave?.Invoke();
    }
}
