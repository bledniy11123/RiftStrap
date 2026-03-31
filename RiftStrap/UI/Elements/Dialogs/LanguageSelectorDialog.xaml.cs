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

namespace RiftStrap.UI.Elements.Dialogs
{

    public partial class LanguageSelectorDialog
    {
        public LanguageSelectorDialog()
        {
            var viewModel = new LanguageSelectorViewModel();

            DataContext = viewModel;
            InitializeComponent();

            viewModel.CloseRequestEvent += (_, _) => Close();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();
    }
}
