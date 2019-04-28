using PSRT.Astra.Models;
using PSRT.Astra.ViewModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PSRT.Astra.Views
{
    /// <summary>
    /// Interaction logic for InstallSelectorWindow.xaml
    /// </summary>
    public partial class InstallSelectorWindow : Window
    {
        private InstallSelectorWindowViewModel _ViewModel;
        private UpdateChecker.UpdateInformation _UpdateInformation;

        public InstallSelectorWindow(InstallSelectorWindowViewModel viewModel, UpdateChecker.UpdateInformation updateInformation)
        {
            _UpdateInformation = updateInformation;

            InitializeComponent();

            _ViewModel = viewModel;
            DataContext = _ViewModel;
        }

        private void _SelectDirectory_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = LocaleManager.Instance["InstallSelectorWindow"];
                var result = dialog.ShowDialog();

                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    var selectedPath = dialog.SelectedPath;
                    // if the user selects the pso2_bin directory rather than their actual
                    // installation directory with the folder browser, move their selection
                    // back down to the base installation directory
                    selectedPath = Regex.Replace(selectedPath, @"\\pso2_bin\\?$", string.Empty);
                    _ViewModel.SelectedPath = selectedPath;
                }
            }
        }

        private void _AcceptButton_Click(object sender, RoutedEventArgs e)
        {
            if (_ViewModel.SelectedPathContainsPSO2Bin)
            {
                var message = LocaleManager.Instance["InstallSelectorWindow_ExistingInstallationSelectMessage"];
                System.Windows.MessageBox.Show(message, "PSRT.Astra", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            _OpenMainWindow();
        }

        private void _OpenMainWindow()
        {
            Properties.Settings.Default.LastSelectedInstallLocation = _ViewModel.SelectedPath;
            Properties.Settings.Default.Save();

            var mainWindow = new MainWindow(Path.Combine(_ViewModel.SelectedPath, "pso2_bin"), _UpdateInformation);
            mainWindow.Show();
            Close();
        }
    }
}
