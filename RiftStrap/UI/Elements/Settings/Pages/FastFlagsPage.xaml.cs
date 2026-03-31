using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;

using RiftStrap.UI.ViewModels.Settings;
using RiftStrap.Features.FastFlagProfiles;
using Wpf.Ui.Mvvm.Contracts;

namespace RiftStrap.UI.Elements.Settings.Pages
{

    public partial class FastFlagsPage
    {
        private bool _initialLoad = false;

        private FastFlagsViewModel _viewModel = null!;

        public FastFlagsPage()
        {
            SetupViewModel();
            InitializeComponent();
        }

        private void SetupViewModel()
        {
            _viewModel = new FastFlagsViewModel();

            _viewModel.OpenFlagEditorEvent += OpenFlagEditor;
            _viewModel.RequestPageReloadEvent += (_, _) => SetupViewModel();

            DataContext = _viewModel;
        }

        private void OpenFlagEditor(object? sender, EventArgs e)
        {
            if (Window.GetWindow(this) is INavigationWindow window)
                    window.Navigate(typeof(FastFlagEditorPage));
        }

        private void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            var sb = (Storyboard)FindResource("SectionEntrance");
            sb.Begin(this, true);

            if (!_initialLoad)
            {
                _initialLoad = true;
                return;
            }

            SetupViewModel();
        }

        private void ValidateInt32(object sender, TextCompositionEventArgs e) => e.Handled = e.Text != "-" && !Int32.TryParse(e.Text, out int _);

        private void ValidateUInt32(object sender, TextCompositionEventArgs e) => e.Handled = !UInt32.TryParse(e.Text, out uint _);

        private readonly FlagProfileManager _profileManager = new();

        private void RefreshProfiles()
        {
            var profiles = _profileManager.GetProfiles();
            ProfileCombo.ItemsSource = profiles;
        }

        private void ProfileCombo_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e) { }

        private void SaveProfile_Click(object sender, RoutedEventArgs e)
        {
            var name = UI.Controls.Rift.RiftInputDialog.Show("Save FastFlag Profile", "Profile name:", "My Profile");
            if (string.IsNullOrEmpty(name)) return;

            _profileManager.SaveCurrentAsProfile(name);
            RefreshProfiles();
            Frontend.ShowMessageBox($"Profile \"{name}\" saved ({App.FastFlags.Prop.Count} flags).", MessageBoxImage.Information);
        }

        private void LoadProfile_Click(object sender, RoutedEventArgs e)
        {
            if (ProfileCombo.SelectedItem is not FlagProfile profile) return;

            var result = Frontend.ShowMessageBox(
                $"Load profile \"{profile.Name}\"?\nThis will replace your current FastFlags.",
                MessageBoxImage.Question, MessageBoxButton.YesNo);

            if (result == MessageBoxResult.Yes)
            {
                _profileManager.LoadProfile(profile.Id);
                SetupViewModel();
                Frontend.ShowMessageBox("Profile loaded!", MessageBoxImage.Information);
            }
        }

        private void DeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            if (ProfileCombo.SelectedItem is not FlagProfile profile) return;

            _profileManager.DeleteProfile(profile.Id);
            RefreshProfiles();
        }
    }
}
