namespace RiftStrap.Features.StatusMonitor
{

    public class RobloxStatusService
    {
        private static readonly ServiceEndpoint[] Endpoints =
        {
            new("Website", "https://www.roblox.com"),
            new("Auth API", "https://auth.roblox.com"),
            new("Games API", "https://games.roblox.com"),
            new("Users API", "https://users.roblox.com"),
            new("Economy API", "https://economy.roblox.com"),
            new("Thumbnails", "https://thumbnails.roblox.com"),
            new("Presence", "https://presence.roblox.com"),
            new("Friends", "https://friends.roblox.com"),
            new("Assets CDN", "https://assetdelivery.roblox.com"),
        };

        public async Task<List<ServiceStatus>> CheckAllAsync()
        {
            var tasks = Endpoints.Select(ep => CheckEndpointAsync(ep)).ToList();
            return (await Task.WhenAll(tasks)).ToList();
        }

        public async Task<OverallStatus> GetOverallStatusAsync()
        {
            var results = await CheckAllAsync();
            var healthy = results.Count(r => r.IsHealthy);
            var total = results.Count;

            return new OverallStatus
            {
                Healthy = healthy,
                Total = total,
                Results = results,
                Status = healthy == total ? "All Systems Operational"
                    : healthy > total / 2 ? "Partial Outage"
                    : "Major Outage",
                IsFullyOperational = healthy == total,
            };
        }

        private async Task<ServiceStatus> CheckEndpointAsync(ServiceEndpoint endpoint)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Head, endpoint.Url);
                request.Headers.Add("User-Agent", "RiftStrap/StatusMonitor");

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var response = await App.HttpClient.SendAsync(request, cts.Token);
                sw.Stop();

                return new ServiceStatus
                {
                    Endpoint = endpoint,
                    IsHealthy = response.IsSuccessStatusCode || (int)response.StatusCode < 500,
                    StatusCode = (int)response.StatusCode,
                    ResponseMs = (int)sw.ElapsedMilliseconds,
                };
            }
            catch (TaskCanceledException)
            {
                return new ServiceStatus
                {
                    Endpoint = endpoint,
                    IsHealthy = false,
                    StatusCode = 0,
                    ResponseMs = 5000,
                    Error = "Timeout",
                };
            }
            catch (Exception ex)
            {
                return new ServiceStatus
                {
                    Endpoint = endpoint,
                    IsHealthy = false,
                    StatusCode = 0,
                    ResponseMs = (int)sw.ElapsedMilliseconds,
                    Error = ex.Message,
                };
            }
        }
    }

    public class ServiceEndpoint
    {
        public string Name { get; }
        public string Url { get; }

        public ServiceEndpoint(string name, string url)
        {
            Name = name;
            Url = url;
        }
    }

    public class ServiceStatus
    {
        public ServiceEndpoint Endpoint { get; set; } = null!;
        public bool IsHealthy { get; set; }
        public int StatusCode { get; set; }
        public int ResponseMs { get; set; }
        public string? Error { get; set; }

        public string StatusText => IsHealthy ? "Operational" : (Error ?? $"HTTP {StatusCode}");
        public string LatencyText => ResponseMs > 0 ? $"{ResponseMs}ms" : "--";
    }

    public class OverallStatus
    {
        public int Healthy { get; set; }
        public int Total { get; set; }
        public string Status { get; set; } = "";
        public bool IsFullyOperational { get; set; }
        public List<ServiceStatus> Results { get; set; } = new();
    }
}
