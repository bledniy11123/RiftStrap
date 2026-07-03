using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

using RiftStrap.Resources;
using RiftStrap.UI.ViewModels;

namespace RiftStrap.UI.ViewModels.Dialogs
{
    public class UninstallerViewModel : NotifyPropertyChangedViewModel
    {
        public string Text => String.Format(
            Strings.Uninstaller_Text,
            null,
            Paths.Base
        );

        private bool _keepData = true;

        // Full property with change notification so the "your data will be kept" description
        // (driven by a OneWay DataTrigger on KeepData) collapses when the box is unchecked.
        public bool KeepData
        {
            get => _keepData;
            set
            {
                if (_keepData == value) return;
                _keepData = value;
                OnPropertyChanged(nameof(KeepData));
            }
        }

        public ICommand ConfirmUninstallCommand => new RelayCommand(ConfirmUninstall);

        public event EventHandler? ConfirmUninstallRequest;

        private void ConfirmUninstall() => ConfirmUninstallRequest?.Invoke(this, new EventArgs());
    }
}
