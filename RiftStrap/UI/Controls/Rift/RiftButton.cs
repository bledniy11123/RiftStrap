using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace RiftStrap.UI.Controls.Rift
{
    public enum RiftButtonVariant
    {
        Primary,
        Secondary,
        Ghost
    }

    public class RiftButton : Button
    {
        public static readonly DependencyProperty VariantProperty =
            DependencyProperty.Register(nameof(Variant), typeof(RiftButtonVariant), typeof(RiftButton),
                new PropertyMetadata(RiftButtonVariant.Primary));

        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(nameof(CornerRadius), typeof(CornerRadius), typeof(RiftButton),
                new PropertyMetadata(new CornerRadius(8)));

        public static readonly DependencyProperty IconProperty =
            DependencyProperty.Register(nameof(Icon), typeof(object), typeof(RiftButton),
                new PropertyMetadata(null));

        public RiftButtonVariant Variant
        {
            get => (RiftButtonVariant)GetValue(VariantProperty);
            set => SetValue(VariantProperty, value);
        }

        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        public object? Icon
        {
            get => GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        static RiftButton()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(RiftButton),
                new FrameworkPropertyMetadata(typeof(RiftButton)));
        }
    }
}
