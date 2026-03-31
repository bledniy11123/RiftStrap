namespace RiftStrap.Features.Plugins
{

    public interface IPlugin
    {
        string Id { get; }
        string Name { get; }
        string Version { get; }
        string Author { get; }
        string Description { get; }

        Task OnActivateAsync(IPluginContext context);
        Task OnDeactivateAsync();
    }

    public interface IPluginContext
    {

        IReadOnlyDictionary<string, object> GetFastFlags();
        void SetFastFlag(string key, object? value);

        event Action<long, long>? OnGameJoin;
        event Action? OnGameLeave;

        void ShowToast(string title, string message);

        string DataPath { get; }

        void Log(string message);
    }

    public class PluginManifest
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.0.0";

        [JsonPropertyName("author")]
        public string Author { get; set; } = "";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        [JsonPropertyName("entry_dll")]
        public string EntryDll { get; set; } = "";

        [JsonPropertyName("entry_class")]
        public string EntryClass { get; set; } = "";

        [JsonPropertyName("min_version")]
        public string MinLauncherVersion { get; set; } = "0.1.0";

        [JsonPropertyName("permissions")]
        public List<string> Permissions { get; set; } = new();

    }
}
