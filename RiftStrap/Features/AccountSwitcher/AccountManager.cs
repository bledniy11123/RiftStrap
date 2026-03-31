using System.Security.Cryptography;
using Microsoft.Win32;
using RiftStrap.Features.AccountSwitcher.Models;

namespace RiftStrap.Features.AccountSwitcher
{

    public class AccountManager
    {
        private static readonly string StoreFile = Path.Combine(Paths.Base, "Accounts.json");
        private static readonly string CookieDir = Path.Combine(Paths.Base, "AccountCookies");

        private AccountStore _store = new();

        public IReadOnlyList<RobloxAccount> Accounts => _store.Accounts;
        public long? ActiveUserId => _store.ActiveUserId;

        public AccountManager()
        {
            Directory.CreateDirectory(CookieDir);
            Load();
        }

        public async Task<RobloxAccount?> AddAccountAsync(string cookie)
        {
            try
            {

                var userInfo = await FetchUserInfoAsync(cookie);
                if (userInfo == null)
                    return null;

                var (userId, username, displayName) = userInfo.Value;

                var existing = _store.Accounts.FirstOrDefault(a => a.UserId == userId);
                if (existing != null)
                {
                    existing.Cookie = cookie;
                    SaveCookie(existing.UserId, cookie);
                    Save();
                    return existing;
                }

                var avatarUrl = await FetchAvatarUrlAsync(userId);

                var account = new RobloxAccount
                {
                    UserId = userId,
                    Username = username,
                    DisplayName = displayName,
                    AvatarUrl = avatarUrl,
                    Cookie = cookie,
                };

                _store.Accounts.Add(account);
                SaveCookie(account.UserId, cookie);
                Save();

                App.Logger.WriteLine("AccountManager", $"Added account: {account.Username} ({account.UserId})");
                return account;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("AccountManager", $"Failed to add account: {ex.Message}");
                return null;
            }
        }

        public bool SwitchAccount(long userId)
        {
            var account = _store.Accounts.FirstOrDefault(a => a.UserId == userId);
            if (account == null) return false;

            var cookie = LoadCookie(userId);
            if (string.IsNullOrEmpty(cookie)) return false;

            try
            {

                using var key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Roblox\RobloxStudioBrowser\roblox.com");
                key?.SetValue(".ROBLOSECURITY", $"COOK::<{cookie}>", RegistryValueKind.String);

                _store.ActiveUserId = userId;
                account.LastUsed = DateTime.UtcNow;
                Save();

                App.Logger.WriteLine("AccountManager", $"Switched to: {account.Username}");
                return true;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("AccountManager", $"Failed to switch account: {ex.Message}");
                return false;
            }
        }

        public void RemoveAccount(long userId)
        {
            _store.Accounts.RemoveAll(a => a.UserId == userId);
            if (_store.ActiveUserId == userId)
                _store.ActiveUserId = null;

            try
            {
                var cookiePath = GetCookiePath(userId);
                if (File.Exists(cookiePath))
                    File.Delete(cookiePath);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("AccountManager", $"Failed to delete cookie file: {ex.Message}");
            }

            Save();
        }

        public RobloxAccount? GetActiveAccount()
        {
            if (_store.ActiveUserId == null) return null;
            return _store.Accounts.FirstOrDefault(a => a.UserId == _store.ActiveUserId);
        }

        public async Task RefreshAllAsync()
        {
            foreach (var account in _store.Accounts)
            {
                try
                {
                    var cookie = LoadCookie(account.UserId);
                    if (string.IsNullOrEmpty(cookie)) continue;

                    var info = await FetchUserInfoAsync(cookie);
                    if (info != null)
                    {
                        account.Username = info.Value.Username;
                        account.DisplayName = info.Value.DisplayName;
                    }

                    var avatar = await FetchAvatarUrlAsync(account.UserId);
                    if (avatar != null)
                        account.AvatarUrl = avatar;
                }
                catch { }
            }

            Save();
        }

        private void SaveCookie(long userId, string cookie)
        {
            try
            {
                var data = Encoding.UTF8.GetBytes(cookie);
                var encrypted = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(GetCookiePath(userId), encrypted);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("AccountManager", $"Failed to save cookie for {userId}: {ex.Message}");
            }
        }

        private string? LoadCookie(long userId)
        {
            var path = GetCookiePath(userId);
            if (!File.Exists(path)) return null;

            try
            {
                var encrypted = File.ReadAllBytes(path);
                var data = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(data);
            }
            catch
            {
                return null;
            }
        }

        private string GetCookiePath(long userId) => Path.Combine(CookieDir, $"{userId}.enc");

        private async Task<(long UserId, string Username, string DisplayName)?> FetchUserInfoAsync(string cookie)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://users.roblox.com/v1/users/authenticated");
            request.Headers.Add("Cookie", $".ROBLOSECURITY={cookie}");

            var response = await App.HttpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<JsonElement>(json);

            return (
                data.GetProperty("id").GetInt64(),
                data.GetProperty("name").GetString() ?? "",
                data.GetProperty("displayName").GetString() ?? ""
            );
        }

        private async Task<string?> FetchAvatarUrlAsync(long userId)
        {
            try
            {
                var json = await App.HttpClient.GetStringAsync(
                    $"https://thumbnails.roblox.com/v1/users/avatar-headshot?userIds={userId}&size=150x150&format=Png");
                var data = JsonSerializer.Deserialize<JsonElement>(json);
                return data.GetProperty("data")[0].GetProperty("imageUrl").GetString();
            }
            catch
            {
                return null;
            }
        }

        private void Load()
        {
            if (!File.Exists(StoreFile)) return;

            try
            {
                var json = File.ReadAllText(StoreFile);
                _store = JsonSerializer.Deserialize<AccountStore>(json) ?? new();
            }
            catch
            {
                _store = new();
            }
        }

        private void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(_store, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(StoreFile, json);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("AccountManager", $"Failed to save accounts: {ex.Message}");
            }
        }
    }
}
