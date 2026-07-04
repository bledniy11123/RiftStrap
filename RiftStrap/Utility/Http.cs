namespace RiftStrap.Utility
{
    internal static class Http
    {

        public static async Task<T> GetJson<T>(string url)
        {
            using var response = await App.HttpClient.GetAsync(url);

            using var stream = await response.Content.ReadAsStreamAsync();

            return await JsonSerializer.DeserializeAsync<T>(stream) ?? throw new InvalidOperationException($"Null JSON response from {url}");
        }
    }
}
