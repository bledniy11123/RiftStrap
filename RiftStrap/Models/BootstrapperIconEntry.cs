using System.Windows.Media;

namespace RiftStrap.Models
{
    public class BootstrapperIconEntry
    {
        public BootstrapperIcon IconType { get; set; }
        public ImageSource ImageSource => IconType.GetIcon().GetImageSource();
    }
}
