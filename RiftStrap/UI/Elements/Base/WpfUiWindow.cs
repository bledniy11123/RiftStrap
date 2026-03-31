using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using RiftStrap.Extensions;
using Wpf.Ui.Controls;

namespace RiftStrap.UI.Elements.Base
{
    public abstract class WpfUiWindow : UiWindow
    {
        protected IntPtr Handle
        {
            get
            {
                if (!Dispatcher.CheckAccess())
                    return (IntPtr)Dispatcher.Invoke(() => InteropHelper.Handle);
                return InteropHelper.Handle;
            }
        }

        public WpfUiWindow()
        {
            ApplyTheme();
            ApplyIcon();
        }

        private void ApplyIcon()
        {
            try
            {
                Icon = Properties.Resources.IconRiftStrap.GetImageSource();
            }
            catch { }
        }

        public void ApplyTheme()
        {

            ForceMonochromeAccent();

#if QA_BUILD
            this.BorderBrush = Brushes.Red;
            this.BorderThickness = new Thickness(4);
#endif
        }

        private static void ForceMonochromeAccent()
        {
            var res = Application.Current.Resources;

            var white = Color.FromRgb(0xFA, 0xFA, 0xFA);
            var white90 = Color.FromArgb(0xD9, 0xFF, 0xFF, 0xFF);
            var white80 = Color.FromArgb(0xB3, 0xFF, 0xFF, 0xFF);

            res["SystemAccentColor"] = white;
            res["SystemAccentColorPrimary"] = white;
            res["SystemAccentColorSecondary"] = white90;
            res["SystemAccentColorTertiary"] = white80;

            res["SystemAccentColorBrush"] = new SolidColorBrush(white);
            res["SystemAccentColorPrimaryBrush"] = new SolidColorBrush(white);
            res["SystemAccentColorSecondaryBrush"] = new SolidColorBrush(white90);
            res["SystemAccentColorTertiaryBrush"] = new SolidColorBrush(white80);

            res["AccentTextFillColorPrimaryBrush"] = new SolidColorBrush(white90);
            res["AccentTextFillColorSecondaryBrush"] = new SolidColorBrush(white80);
            res["AccentTextFillColorTertiaryBrush"] = new SolidColorBrush(white);

            res["AccentFillColorDefaultBrush"] = new SolidColorBrush(white);
            res["AccentFillColorSecondaryBrush"] = new SolidColorBrush(white90);
            res["AccentFillColorTertiaryBrush"] = new SolidColorBrush(white80);

            res["SystemFillColorAttentionBrush"] = new SolidColorBrush(white);
            res["AccentFillColorSelectedTextBackgroundBrush"] = new SolidColorBrush(white);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            if (App.Settings.Prop.WPFSoftwareRender || App.LaunchSettings.NoGPUFlag.Active)
            {
                if (PresentationSource.FromVisual(this) is HwndSource hwndSource)
                    hwndSource.CompositionTarget.RenderMode = RenderMode.SoftwareOnly;
            }

            base.OnSourceInitialized(e);
        }
    }
}
