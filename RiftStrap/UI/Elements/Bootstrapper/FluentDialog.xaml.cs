using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using RiftStrap.UI.ViewModels.Bootstrapper;

namespace RiftStrap.UI.Elements.Bootstrapper
{
    public partial class FluentDialog
    {
        private DispatcherTimer? _progressTimer;

        public FluentDialog(bool aero) : base()
        {
            InitializeComponent();
            _viewModel = new FluentDialogViewModel(this, aero);
            DataContext = _viewModel;
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {

            ((Storyboard)FindResource("Entrance")).Begin(this, true);

            ((Storyboard)FindResource("PortalPulse")).Begin(this, true);
            ((Storyboard)FindResource("PortalPulse2")).Begin(this, true);
            ((Storyboard)FindResource("PortalPulse3")).Begin(this, true);

            ((Storyboard)FindResource("LogoFloat")).Begin(this, true);

            ((Storyboard)FindResource("Shimmer")).Begin(this, true);
            ((Storyboard)FindResource("DotBlink")).Begin(this, true);

            _progressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _progressTimer.Tick += (_, _) => UpdatePercent();
            _progressTimer.Start();
        }

        private void UpdatePercent()
        {
            if (_viewModel.ProgressMaximum > 0 && !_viewModel.ProgressIndeterminate)
            {
                var percent = (int)(_viewModel.ProgressValue / (double)_viewModel.ProgressMaximum * 100);
                PercentText.Text = $"{Math.Min(percent, 100)}%";
            }
            else
            {
                PercentText.Text = "";
            }
        }

        private void OnDrag(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }
    }
}
