using RiftStrap.UI.ViewModels.Bootstrapper;

namespace RiftStrap.UI.Elements.Bootstrapper
{

    public partial class CustomDialog
    {
        public CustomDialog()
            : base()
        {
            InitializeComponent();

            _viewModel = new BootstrapperDialogViewModel(this);
            DataContext = _viewModel;
        }
    }
}
