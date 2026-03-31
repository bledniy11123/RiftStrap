namespace RiftStrap.UI.ViewModels.Installer
{
    public class WelcomeViewModel : NotifyPropertyChangedViewModel
    {

        public string MainText => String.Format(
            Strings.Installer_Welcome_MainText,
            "[github.com/riftstrap/riftstrap](https://github.com/riftstrap/riftstrap)",
            "[riftstrap.com](https://riftstrap.com)"
        );

        public string VersionNotice { get; private set; } = "";

        public bool CanContinue { get; set; } = false;

        public event EventHandler? CanContinueEvent;

        public async void DoChecks()
        {
            var releaseInfo = await App.GetLatestRelease();

            if (releaseInfo is not null)
            {
                if (Utilities.CompareVersions(App.Version, releaseInfo.TagName) == VersionComparison.LessThan)
                {
                    VersionNotice = String.Format(Strings.Installer_Welcome_UpdateNotice, App.Version, releaseInfo.TagName.Replace("v", ""));
                    OnPropertyChanged(nameof(VersionNotice));
                }
            }

            CanContinue = true;
            OnPropertyChanged(nameof(CanContinue));

            CanContinueEvent?.Invoke(this, new EventArgs());
        }
    }
}
