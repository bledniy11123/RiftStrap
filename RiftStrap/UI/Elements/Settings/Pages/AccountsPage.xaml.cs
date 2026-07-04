using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Wpf.Ui.Controls;
using RiftStrap.Features.AccountSwitcher;
using RiftStrap.Features.AccountSwitcher.Models;

namespace RiftStrap.UI.Elements.Settings.Pages
{
    public partial class AccountsPage : UiPage
    {
        private readonly AccountManager _manager = new();
        private bool _isAdding;

        public AccountsPage() => InitializeComponent();

        private void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            ((Storyboard)FindResource("SectionEntrance")).Begin(this, true);
            RefreshList();
        }

        private void RefreshList()
        {
            var accounts = _manager.Accounts.OrderByDescending(a => a.LastUsed ?? a.AddedAt).ToList();
            foreach (var a in accounts)
                a.IsActive = a.UserId == _manager.ActiveUserId;
            AccountsList.ItemsSource = accounts;
            S3.Visibility = accounts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void AddAccount_Click(object sender, RoutedEventArgs e)
        {
            // Guard against re-entrant clicks while an add is in flight (async void +
            // awaited AddAccountAsync would otherwise allow duplicate accounts via a race).
            if (_isAdding) return;
            _isAdding = true;
            var addButton = sender as System.Windows.Controls.Control;
            if (addButton != null) addButton.IsEnabled = false;

            try
            {
                // Accept a single cookie OR a whole list (one cookie per line) — multiline input.
                var input = UI.Controls.Rift.RiftInputDialog.Show(
                    "Add Roblox Account(s)",
                    "Paste one .ROBLOSECURITY cookie — or a whole list, one cookie per line:\n\n(Cookies are encrypted and stored locally)",
                    "", true);

                if (string.IsNullOrWhiteSpace(input)) return;

                var cookies = input
                    .Replace("\r", "")
                    .Split('\n')
                    .Select(ParseCookie)
                    .Where(c => !string.IsNullOrEmpty(c))
                    .Distinct()
                    .ToList();

                if (cookies.Count == 0) return;

                int added = 0, failed = 0;
                var names = new List<string>();

                foreach (var cookie in cookies)
                {
                    var account = await _manager.AddAccountAsync(cookie);
                    if (account != null) { added++; names.Add("@" + account.Username); }
                    else failed++;
                }

                RefreshList();

                if (added == 0)
                    Frontend.ShowMessageBox("No accounts added — the cookie(s) were invalid or expired.", MessageBoxImage.Warning);
                else if (cookies.Count == 1)
                    Frontend.ShowMessageBox($"Added: {names[0]}", MessageBoxImage.Information);
                else
                    Frontend.ShowMessageBox(
                        $"Added {added} account(s)" + (failed > 0 ? $", {failed} failed" : "") + ".\n" + string.Join(", ", names),
                        MessageBoxImage.Information);
            }
            finally
            {
                _isAdding = false;
                if (addButton != null) addButton.IsEnabled = true;
            }
        }

        // Extract the bare .ROBLOSECURITY value from a raw cookie, a "...ROBLOSECURITY=xxx;..." string,
        // or a "_|WARNING..." token. Returns "" for empty/garbage lines.
        private static string ParseCookie(string raw)
        {
            raw = raw.Trim();
            if (string.IsNullOrEmpty(raw))
                return "";
            if (raw.StartsWith("_|"))
                return raw;
            if (raw.Contains("ROBLOSECURITY="))
                return raw.Split("ROBLOSECURITY=")[1].Split(';')[0].Trim();
            return raw;
        }

        private void AccountCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: RobloxAccount account })
            {
                var result = Frontend.ShowMessageBox(
                    $"Switch to {account.DisplayName} (@{account.Username})?",
                    MessageBoxImage.Question, MessageBoxButton.YesNo);

                if (result == MessageBoxResult.Yes)
                {
                    if (_manager.SwitchAccount(account.UserId))
                    {
                        RefreshList();
                        Frontend.ShowMessageBox($"Switched to @{account.Username}!", MessageBoxImage.Information);
                    }
                    else
                    {
                        Frontend.ShowMessageBox("Failed to switch. Cookie may be expired.", MessageBoxImage.Warning);
                    }
                }
            }
        }

        private void RemoveAccount_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { Tag: long userId })
            {
                _manager.RemoveAccount(userId);
                RefreshList();
            }
        }
    }
}
