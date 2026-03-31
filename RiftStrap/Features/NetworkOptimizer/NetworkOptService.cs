using System.Net.NetworkInformation;

namespace RiftStrap.Features.NetworkOptimizer
{

    public class NetworkOptService
    {
        public static readonly DnsServer[] DnsServers =
        {
            new("Google", "8.8.8.8", "8.8.4.4"),
            new("Cloudflare", "1.1.1.1", "1.0.0.1"),
            new("Quad9", "9.9.9.9", "149.112.112.112"),
            new("OpenDNS", "208.67.222.222", "208.67.220.220"),
            new("AdGuard", "94.140.14.14", "94.140.15.15"),
            new("System Default", "", ""),
        };

        public async Task<List<DnsBenchmarkResult>> BenchmarkDnsAsync()
        {
            var results = new List<DnsBenchmarkResult>();

            foreach (var server in DnsServers)
            {
                if (string.IsNullOrEmpty(server.Primary))
                {
                    results.Add(new DnsBenchmarkResult { Server = server, AvgMs = -1, Status = "Skip" });
                    continue;
                }

                var times = new List<long>();

                for (int i = 0; i < 3; i++)
                {
                    try
                    {
                        using var ping = new Ping();
                        var sw = Stopwatch.StartNew();
                        var reply = await ping.SendPingAsync(server.Primary, 2000);
                        sw.Stop();

                        if (reply.Status == IPStatus.Success)
                            times.Add(sw.ElapsedMilliseconds);
                    }
                    catch { }
                }

                results.Add(new DnsBenchmarkResult
                {
                    Server = server,
                    AvgMs = times.Count > 0 ? (int)times.Average() : -1,
                    MinMs = times.Count > 0 ? (int)times.Min() : -1,
                    MaxMs = times.Count > 0 ? (int)times.Max() : -1,
                    Status = times.Count > 0 ? "OK" : "Timeout",
                });
            }

            return results.OrderBy(r => r.AvgMs == -1 ? int.MaxValue : r.AvgMs).ToList();
        }

        public async Task<RobloxPingResult> PingRobloxAsync()
        {
            var endpoints = new[] { "roblox.com", "apis.roblox.com", "games.roblox.com" };
            var results = new List<(string Host, long Ms)>();

            foreach (var host in endpoints)
            {
                try
                {
                    using var ping = new Ping();
                    var reply = await ping.SendPingAsync(host, 3000);
                    if (reply.Status == IPStatus.Success)
                        results.Add((host, reply.RoundtripTime));
                }
                catch { }
            }

            return new RobloxPingResult
            {
                Endpoints = results,
                AvgPing = results.Count > 0 ? (int)results.Average(r => r.Ms) : -1,
            };
        }

        public async Task<int> DiscoverMtuAsync()
        {
            int low = 500, high = 1500, best = 1400;

            while (low <= high)
            {
                int mid = (low + high) / 2;
                try
                {
                    using var ping = new Ping();
                    var options = new PingOptions { DontFragment = true };
                    var buffer = new byte[mid];
                    var reply = await ping.SendPingAsync("roblox.com", 2000, buffer, options);

                    if (reply.Status == IPStatus.Success)
                    {
                        best = mid;
                        low = mid + 1;
                    }
                    else
                    {
                        high = mid - 1;
                    }
                }
                catch
                {
                    high = mid - 1;
                }
            }

            return best + 28;
        }
    }

    public class DnsServer
    {
        public string Name { get; }
        public string Primary { get; }
        public string Secondary { get; }

        public DnsServer(string name, string primary, string secondary)
        {
            Name = name;
            Primary = primary;
            Secondary = secondary;
        }
    }

    public class DnsBenchmarkResult
    {
        public DnsServer Server { get; set; } = null!;
        public int AvgMs { get; set; }
        public int MinMs { get; set; }
        public int MaxMs { get; set; }
        public string Status { get; set; } = "";

        public string DisplayText => AvgMs > 0 ? $"{AvgMs} ms" : Status;
    }

    public class RobloxPingResult
    {
        public List<(string Host, long Ms)> Endpoints { get; set; } = new();
        public int AvgPing { get; set; }
    }
}
