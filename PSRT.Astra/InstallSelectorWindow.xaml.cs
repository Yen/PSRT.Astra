using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PSRT.Astra
{
    /// <summary>
    /// Interaction logic for InstallSelectorWindow.xaml
    /// </summary>
    public partial class InstallSelectorWindow : Window
    {
        private InstallSelectorWindowViewModel _ViewModel;

        public InstallSelectorWindow()
        {
            InitializeComponent();

            _ViewModel = new InstallSelectorWindowViewModel();
            DataContext = _ViewModel;

            _ViewModel.SelectedPath = Properties.Settings.Default.LastSelectedInstallLocation;

            if (_ViewModel.SelectedPathContainsPSO2Bin)
                _OpenMainWindow();
        }

        private void _SelectDirectory_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = LocaleManager.Instance["InstallSelectorWindow"];
                var result = dialog.ShowDialog();
                
                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    _ViewModel.SelectedPath = dialog.SelectedPath;
                }
            }
        }

        private void _AcceptButton_Click(object sender, RoutedEventArgs e)
        {
            _OpenMainWindow();
        }

        private void _OpenMainWindow()
        {
            Properties.Settings.Default.LastSelectedInstallLocation = _ViewModel.SelectedPath;
            Properties.Settings.Default.Save();

            var mainWindow = new MainWindow(Path.Combine(_ViewModel.SelectedPath, "pso2_bin"));
            mainWindow.Show();
            Close();
        }
    }
}
