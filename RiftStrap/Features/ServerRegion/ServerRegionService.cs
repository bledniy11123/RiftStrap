namespace RiftStrap.Features.ServerRegion
{

    public class ServerRegionService
    {
        private static readonly Dictionary<string, string> RegionFlags = new()
        {
            ["US"] = "🇺🇸", ["GB"] = "🇬🇧", ["DE"] = "🇩🇪", ["FR"] = "🇫🇷",
            ["JP"] = "🇯🇵", ["SG"] = "🇸🇬", ["AU"] = "🇦🇺", ["BR"] = "🇧🇷",
            ["IN"] = "🇮🇳", ["NL"] = "🇳🇱", ["CA"] = "🇨🇦", ["KR"] = "🇰🇷",
        };

        public async Task<ServerRegionInfo?> GetRegionAsync(string ipAddress)
        {
            if (string.IsNullOrEmpty(ipAddress) || ipAddress == "0.0.0.0")
                return null;

            try
            {
                var json = await App.HttpClient.GetStringAsync($"http://ip-api.com/json/{ipAddress}?fields=status,country,countryCode,regionName,city,isp,query");
                var data = JsonSerializer.Deserialize<JsonElement>(json);

                if (data.GetProperty("status").GetString() != "success")
                    return null;

                var countryCode = data.GetProperty("countryCode").GetString() ?? "";

                return new ServerRegionInfo
                {
                    IP = data.GetProperty("query").GetString() ?? ipAddress,
                    Country = data.GetProperty("country").GetString() ?? "Unknown",
                    CountryCode = countryCode,
                    Region = data.GetProperty("regionName").GetString() ?? "",
                    City = data.GetProperty("city").GetString() ?? "",
                    ISP = data.GetProperty("isp").GetString() ?? "",
                    Flag = RegionFlags.GetValueOrDefault(countryCode, "🌐"),
                };
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("ServerRegion", $"Failed to get region for {ipAddress}: {ex.Message}");
                return null;
            }
        }

        public async Task<ServerRegionInfo?> GetRegionFromAddressAsync(string? address)
        {
            if (string.IsNullOrEmpty(address)) return null;
            var ip = address.Contains(':') ? address.Split(':')[0] : address;
            return await GetRegionAsync(ip);
        }
    }

    public class ServerRegionInfo
    {
        public string IP { get; set; } = "";
        public string Country { get; set; } = "Unknown";
        public string CountryCode { get; set; } = "";
        public string Region { get; set; } = "";
        public string City { get; set; } = "";
        public string ISP { get; set; } = "";
        public string Flag { get; set; } = "🌐";

        public string DisplayName => string.IsNullOrEmpty(City)
            ? $"{Flag} {Country}"
            : $"{Flag} {City}, {Country}";

        public string ShortName => $"{Flag} {CountryCode}";
    }
}
