using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace RiftStrap.UI.Controls.Rift
{
    public class RiftToggle : ToggleButton
    {
        public static readonly DependencyProperty ThumbPositionProperty =
            DependencyProperty.Register(nameof(ThumbPosition), typeof(double), typeof(RiftToggle),
                new PropertyMetadata(2.0));

        public double ThumbPosition
        {
            get => (double)GetValue(ThumbPositionProperty);
            set => SetValue(ThumbPositionProperty, value);
        }

        static RiftToggle()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(RiftToggle),
                new FrameworkPropertyMetadata(typeof(RiftToggle)));
        }

        protected override void OnChecked(RoutedEventArgs e)
        {
            base.OnChecked(e);
            AnimateThumb(20.0);
        }

        protected override void OnUnchecked(RoutedEventArgs e)
        {
            base.OnUnchecked(e);
            AnimateThumb(2.0);
        }

        private void AnimateThumb(double target)
        {
            var animation = new DoubleAnimation
            {
                To = target,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            BeginAnimation(ThumbPositionProperty, animation);
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            ThumbPosition = IsChecked == true ? 20.0 : 2.0;
        }
    }
}
