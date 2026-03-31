using RiftStrap.UI.ViewModels.Bootstrapper;

namespace RiftStrap.UI.Elements.Bootstrapper
{

    public partial class ClassicFluentDialog
    {
        public ClassicFluentDialog()
            : base()
        {
            InitializeComponent();

            _viewModel = new ClassicFluentDialogViewModel(this);
            DataContext = _viewModel;
        }
    }
}
