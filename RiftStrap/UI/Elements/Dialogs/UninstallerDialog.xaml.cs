using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using RiftStrap.UI.ViewModels.Dialogs;
using RiftStrap.UI.ViewModels.Installer;
using Wpf.Ui.Mvvm.Interfaces;

namespace RiftStrap.UI.Elements.Dialogs
{

    public partial class UninstallerDialog
    {
        public bool Confirmed { get; private set; } = false;

        public bool KeepData { get; private set; } = true;

        public UninstallerDialog()
        {
            var viewModel = new UninstallerViewModel();
            viewModel.ConfirmUninstallRequest += (_, _) =>
            {
                Confirmed = true;
                KeepData = viewModel.KeepData;
                Close();
            };

            DataContext = viewModel;

            InitializeComponent();
        }

        private void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left) DragMove();
        }

        private void CloseBtn_Click(object sender, System.Windows.RoutedEventArgs e) => Close();
    }
}
