using System.Net.NetworkInformation;

namespace RiftStrap.Features.LaunchOptimizer
{

    public class LaunchOptService
    {
        private static readonly string[] PrefetchDomains =
        {
            "roblox.com",
            "setup.rbxcdn.com",
            "clientsettingscdn.roblox.com",
            "games.roblox.com",
            "apis.roblox.com",
            "auth.roblox.com",
            "thumbnails.roblox.com",
            "assetdelivery.roblox.com",
            "presence.roblox.com",
            "users.roblox.com",
            "economy.roblox.com",
            "friends.roblox.com",
        };

        private bool _warmed = false;

        public async Task WarmupAsync()
        {
            if (_warmed) return;

            var sw = Stopwatch.StartNew();
            var tasks = PrefetchDomains.Select(PrefetchDomainAsync).ToList();
            await Task.WhenAll(tasks);
            sw.Stop();

            var resolved = tasks.Count(t => t.Result);
            _warmed = true;

            App.Logger.WriteLine("LaunchOptimizer",
                $"DNS prefetch complete: {resolved}/{PrefetchDomains.Length} resolved in {sw.ElapsedMilliseconds}ms");
        }

        private async Task<bool> PrefetchDomainAsync(string domain)
        {
            try
            {
                await System.Net.Dns.GetHostAddressesAsync(domain);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<int> PingCdnAsync()
        {
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync("setup.rbxcdn.com", 3000);
                return reply.Status == IPStatus.Success ? (int)reply.RoundtripTime : -1;
            }
            catch
            {
                return -1;
            }
        }

        public async Task<string?> PreValidateAsync()
        {
            try
            {
                var json = await App.HttpClient.GetStringAsync("https://clientsettingscdn.roblox.com/v2/client-version/WindowsPlayer");
                var data = JsonSerializer.Deserialize<JsonElement>(json);
                return data.GetProperty("clientVersionUpload").GetString();
            }
            catch
            {
                return null;
            }
        }
    }
}
