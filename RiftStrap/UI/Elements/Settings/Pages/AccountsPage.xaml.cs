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

        public AccountsPage() => InitializeComponent();

        private void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            ((Storyboard)FindResource("SectionEntrance")).Begin(this, true);
            RefreshList();
        }

        private void RefreshList()
        {
            var accounts = _manager.Accounts.OrderByDescending(a => a.LastUsed ?? a.AddedAt).ToList();
            AccountsList.ItemsSource = accounts;
            S3.Visibility = accounts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void AddAccount_Click(object sender, RoutedEventArgs e)
        {
            var cookie = UI.Controls.Rift.RiftInputDialog.Show(
                "Add Roblox Account",
                "Paste your .ROBLOSECURITY cookie:\n\n(Your cookie is encrypted and stored locally)");

            if (string.IsNullOrEmpty(cookie)) return;

            cookie = cookie.Trim();
            if (cookie.StartsWith("_|"))
                {  }
            else if (cookie.Contains("ROBLOSECURITY="))
                cookie = cookie.Split("ROBLOSECURITY=")[1].Split(';')[0].Trim();

            var account = await _manager.AddAccountAsync(cookie);

            if (account != null)
            {
                RefreshList();
                Frontend.ShowMessageBox($"Added: {account.DisplayName} (@{account.Username})", MessageBoxImage.Information);
            }
            else
            {
                Frontend.ShowMessageBox("Invalid cookie or failed to authenticate.", MessageBoxImage.Warning);
            }
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
