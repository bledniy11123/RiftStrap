using System.Windows;
using System.Windows.Media.Animation;
using Wpf.Ui.Controls;
using RiftStrap.Features.Analytics;

namespace RiftStrap.UI.Elements.Settings.Pages
{
    public partial class AnalyticsPage : UiPage
    {
        private readonly AnalyticsService _analytics = new();
        public AnalyticsPage() => InitializeComponent();

        private void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            var sb = (Storyboard)FindResource("SectionEntrance");
            sb.Begin(this, true);

            TodayText.Text = $"{_analytics.TodayPlayTime.TotalHours:F1}h";
            WeekText.Text = $"{_analytics.WeekPlayTime.TotalHours:F1}h";
            TotalText.Text = $"{_analytics.TotalPlayTime.TotalHours:F1}h";
            GamesText.Text = _analytics.UniqueGamesCount.ToString();

            DailyChart.ItemsSource = _analytics.GetDailyBreakdown(14);
            TopGamesList.ItemsSource = _analytics.GetTopGames(10);
            SessionsList.ItemsSource = _analytics.Sessions.Take(20).ToList();
        }
    }
}
