using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace RiftStrap.UI.Controls.Rift
{
    public class RiftCard : ContentControl
    {
        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(nameof(CornerRadius), typeof(CornerRadius), typeof(RiftCard),
                new PropertyMetadata(new CornerRadius(12)));

        public static readonly DependencyProperty IsInteractiveProperty =
            DependencyProperty.Register(nameof(IsInteractive), typeof(bool), typeof(RiftCard),
                new PropertyMetadata(false));

        public static readonly DependencyProperty GlowOpacityProperty =
            DependencyProperty.Register(nameof(GlowOpacity), typeof(double), typeof(RiftCard),
                new PropertyMetadata(0.0));

        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        public bool IsInteractive
        {
            get => (bool)GetValue(IsInteractiveProperty);
            set => SetValue(IsInteractiveProperty, value);
        }

        public double GlowOpacity
        {
            get => (double)GetValue(GlowOpacityProperty);
            set => SetValue(GlowOpacityProperty, value);
        }

        static RiftCard()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(RiftCard),
                new FrameworkPropertyMetadata(typeof(RiftCard)));
        }

        public RiftCard()
        {
            MouseEnter += OnMouseEnter;
            MouseLeave += OnMouseLeave;
        }

        private void OnMouseEnter(object sender, MouseEventArgs e)
        {
            if (!IsInteractive) return;
            AnimateGlow(0.06);
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            if (!IsInteractive) return;
            AnimateGlow(0.0);
        }

        private void AnimateGlow(double targetOpacity)
        {
            var animation = new DoubleAnimation
            {
                To = targetOpacity,
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            BeginAnimation(GlowOpacityProperty, animation);
        }
    }
}
