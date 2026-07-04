namespace RiftStrap.RobloxInterfaces
{
    public static class Deployment
    {
        public const string DefaultChannel = "production";

        private const string VersionStudioHash = "version-012732894899482c";

        public static string Channel = DefaultChannel;

        public static string BinaryType = "WindowsPlayer";

        public static bool IsDefaultChannel => Channel.Equals(DefaultChannel, StringComparison.OrdinalIgnoreCase);

        public static string BaseUrl { get; private set; } = null!;

        public static readonly List<HttpStatusCode?> BadChannelCodes = new()
        {
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound
        };

        private static readonly Dictionary<string, ClientVersion> ClientVersionCache = new();

        private static readonly Dictionary<string, int> BaseUrls = new()
        {
            { "https://setup.rbxcdn.com", 0 },
            { "https://setup-aws.rbxcdn.com", 2 },
            { "https://setup-ak.rbxcdn.com", 2 },
            { "https://roblox-setup.cachefly.net", 2 },
            { "https://s3.amazonaws.com/setup.roblox.com", 4 }
        };

        private static async Task<string?> TestConnection(string url, int priority, CancellationToken token)
        {
            string LOG_IDENT = $"Deployment::TestConnection<{url}>";

            await Task.Delay(priority * 1000, token);

            App.Logger.WriteLine(LOG_IDENT, "Connecting...");

            try
            {
                using var response = await App.HttpClient.GetAsync($"{url}/versionStudio", token);

                response.EnsureSuccessStatusCode();

                string content = await response.Content.ReadAsStringAsync(token);

                if (content != VersionStudioHash)
                    throw new InvalidHTTPResponseException($"versionStudio response does not match (expected \"{VersionStudioHash}\", got \"{content}\")");
            }
            catch (TaskCanceledException)
            {
                App.Logger.WriteLine(LOG_IDENT, "Connectivity test cancelled.");
                throw;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
                throw;
            }

            return url;
        }

        public static async Task<Exception?> InitializeConnectivity()
        {
            const string LOG_IDENT = "Deployment::InitializeConnectivity";

            using var tokenSource = new CancellationTokenSource();

            var exceptions = new List<Exception>();
            var tasks = (from entry in BaseUrls select TestConnection(entry.Key, entry.Value, tokenSource.Token)).ToList();

            App.Logger.WriteLine(LOG_IDENT, "Testing connectivity...");

            while (tasks.Any() && String.IsNullOrEmpty(BaseUrl))
            {
                var finishedTask = await Task.WhenAny(tasks);

                tasks.Remove(finishedTask);

                if (finishedTask.IsFaulted)
                    exceptions.Add(finishedTask.Exception!.InnerException!);
                else if (!finishedTask.IsCanceled)
                    BaseUrl = finishedTask.Result;
            }

            tokenSource.Cancel();

            // Observe any remaining tasks so their exceptions don't go unobserved.
            if (tasks.Any())
                _ = Task.WhenAll(tasks).ContinueWith(t => { _ = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);

            if (string.IsNullOrEmpty(BaseUrl))
            {
                if (exceptions.Any())
                    return exceptions[0];

                return new TaskCanceledException("All connection attempts timed out.");
            }

            App.Logger.WriteLine(LOG_IDENT, $"Got {BaseUrl} as the optimal base URL");

            return null;
        }

        public static string GetLocation(string resource)
        {
            string location = BaseUrl;

            if (!IsDefaultChannel)
                location += "/channel/common";

            location += resource;

            return location;
        }

        public static async Task<ClientVersion> GetInfo(string? channel = null)
        {
            const string LOG_IDENT = "Deployment::GetInfo";

            if (String.IsNullOrEmpty(channel))
                channel = Channel;

            bool isDefaultChannel = String.Compare(channel, DefaultChannel, StringComparison.OrdinalIgnoreCase) == 0;

            App.Logger.WriteLine(LOG_IDENT, $"Getting deploy info for channel {channel}");

            string cacheKey = $"{channel}-{BinaryType}";

            ClientVersion clientVersion;

            if (ClientVersionCache.ContainsKey(cacheKey))
            {
                App.Logger.WriteLine(LOG_IDENT, "Deploy information is cached");
                clientVersion = ClientVersionCache[cacheKey];
            }
            else
            {
                string path = $"/v2/client-version/{BinaryType}";

                if (!isDefaultChannel)
                    path = $"/v2/client-version/{BinaryType}/channel/{channel}";

                try
                {
                    clientVersion = await Http.GetJson<ClientVersion>("https://clientsettingscdn.roblox.com" + path);
                }
                catch (HttpRequestException httpEx)
                when (!isDefaultChannel && BadChannelCodes.Contains(httpEx.StatusCode))
                {
                    throw new InvalidChannelException(httpEx.StatusCode);
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, "Failed to contact clientsettingscdn! Falling back to clientsettings...");
                    App.Logger.WriteException(LOG_IDENT, ex);

                    try
                    {
                        clientVersion = await Http.GetJson<ClientVersion>("https://clientsettings.roblox.com" + path);
                    }
                    catch (HttpRequestException httpEx)
                    when (!isDefaultChannel && BadChannelCodes.Contains(httpEx.StatusCode))
                    {
                        throw new InvalidChannelException(httpEx.StatusCode);
                    }
                }

                ClientVersionCache[cacheKey] = clientVersion;
            }

            return clientVersion;
        }

        public static async Task<List<VersionHistoryEntry>> FetchChannelVersionsAsync(string? channel = null, string? binaryFilter = null)
        {
            const string LOG_IDENT = "Deployment::FetchChannelVersionsAsync";

            if (string.IsNullOrEmpty(channel))
                channel = Channel;

            var results = new List<VersionHistoryEntry>();
            var seen = new HashSet<string>();

            foreach (var binary in new[] { "WindowsPlayer", "WindowsStudio64" })
            {
                try
                {
                    bool isDefault = string.Compare(channel, DefaultChannel, StringComparison.OrdinalIgnoreCase) == 0;
                    string path = isDefault
                        ? $"/v2/client-version/{binary}"
                        : $"/v2/client-version/{binary}/channel/{channel}";

                    var cv = await Http.GetJson<ClientVersion>("https://clientsettingscdn.roblox.com" + path);

                    if (seen.Add(cv.VersionGuid))
                    {
                        results.Add(new VersionHistoryEntry
                        {
                            VersionGuid = cv.VersionGuid,
                            Version = $"{cv.Version} ({binary}) — LATEST",
                            Channel = channel,
                            Timestamp = DateTime.UtcNow
                        });
                        RecordVersionHistory(cv.VersionGuid, $"{cv.Version} ({binary})", channel);
                    }
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Failed to fetch {binary} for {channel}: {ex.Message}");
                }
            }

            try
            {
                await FetchPreviousVersion(results, seen);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Previous version fetch failed: {ex.Message}");
            }

            try
            {
                await FetchDeployHistory(results, seen);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"DeployHistory fetch failed: {ex.Message}");
            }

            var history = App.VersionHistoryManager.Prop.Entries
                .Where(e => string.IsNullOrEmpty(e.Channel) || e.Channel.Equals(channel, StringComparison.OrdinalIgnoreCase))
                .Where(e => !seen.Contains(e.VersionGuid));

            results.AddRange(history);

            return results.Take(30).ToList();
        }

        private static async Task FetchPreviousVersion(List<VersionHistoryEntry> results, HashSet<string> seen)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://weao.xyz/api/versions/past");
            request.Headers.Add("User-Agent", "WEAO-3PService");
            var response = await App.HttpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (data == null) return;

            foreach (var (platform, guid) in data)
            {
                if (string.IsNullOrEmpty(guid) || guid == "version-hidden" || !guid.StartsWith("version-")) continue;
                if (!seen.Add(guid)) continue;

                string binary = platform switch
                {
                    "Windows" => "WindowsPlayer",
                    "WindowsStudio" or "WindowsStudio64" => "WindowsStudio64",
                    _ => platform
                };

                results.Add(new VersionHistoryEntry
                {
                    VersionGuid = guid,
                    Version = $"Previous ({binary})",
                    Channel = "production",
                    Timestamp = DateTime.UtcNow.AddHours(-1)
                });

                RecordVersionHistory(guid, $"Previous ({binary})", "production");
            }
        }

        private static async Task FetchDeployHistory(List<VersionHistoryEntry> results, HashSet<string> seen)
        {
            var response = await App.HttpClient.GetStringAsync($"{BaseUrl}/DeployHistory.txt");
            var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines.Reverse())
            {
                if (results.Count >= 20) break;
                if (!line.StartsWith("New ") || line.Contains("version-hidden")) continue;

                var match = Regex.Match(line,
                    @"New (\w+) (version-[a-f0-9]+) at (.+?), file version: (\d+), (\d+), (\d+), (\d+)");
                if (!match.Success) continue;

                var binary = match.Groups[1].Value;
                var guid = match.Groups[2].Value;
                if (!seen.Add(guid)) continue;

                var ver = $"0.{match.Groups[5].Value}.{match.Groups[6].Value}.{match.Groups[7].Value}";
                DateTime.TryParse(match.Groups[3].Value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var ts);

                results.Add(new VersionHistoryEntry
                {
                    VersionGuid = guid,
                    Version = $"{ver} ({binary})",
                    Channel = "production",
                    Timestamp = ts == default ? DateTime.UtcNow : ts
                });

                RecordVersionHistory(guid, $"{ver} ({binary})", "production");
            }
        }

        public static async Task<bool> ValidateVersionGuid(string versionGuid)
        {
            try
            {
                string url = GetLocation($"/{versionGuid}-rbxPkgManifest.txt");
                using var request = new HttpRequestMessage(HttpMethod.Head, url);
                using var response = await App.HttpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public static void RecordVersionHistory(string versionGuid, string version, string channel)
        {
            var history = App.VersionHistoryManager.Prop;

            if (history.Entries.Any(e => e.VersionGuid == versionGuid))
                return;

            history.Entries.Insert(0, new VersionHistoryEntry
            {
                VersionGuid = versionGuid,
                Version = version,
                Channel = channel,
                Timestamp = DateTime.UtcNow
            });

            while (history.Entries.Count > 50)
                history.Entries.RemoveAt(history.Entries.Count - 1);

            App.VersionHistoryManager.Save();
        }
    }
}
