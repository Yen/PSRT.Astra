using PSRT.Astra.Models;
using PSRT.Astra.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

namespace PSRT.Astra.Views
{
    /// <summary>
    /// Interaction logic for ModsWindow.xaml
    /// </summary>
    public partial class ModsWindow : Window
    {
        private ModsWindowViewModel _ViewModel;

        public ModsWindow(InstallConfiguration installConfiguration)
        {
            InitializeComponent();

            _ViewModel = new ModsWindowViewModel(installConfiguration);
            DataContext = _ViewModel;
        }

        private async void Window_Closed(object sender, EventArgs e)
        {
            Properties.Settings.Default.Save();
            await _ViewModel.DisposeAsync();
        }

        private void OpenModsDirectoryButton_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(_ViewModel.InstallConfiguration.ModsDirectory);
        }

        private void MoveUpButton_Click(object sender, RoutedEventArgs e) => _ViewModel.MoveUpEntry();
        private void MoveDownButton_Click(object sender, RoutedEventArgs e) => _ViewModel.MoveDownEntry();

    }
}
