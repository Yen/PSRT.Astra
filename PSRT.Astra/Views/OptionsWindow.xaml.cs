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
    public partial class OptionsWindow : Window
    {
        private OptionsWindowViewModel _ViewModel;

        public OptionsWindow()
        {
            InitializeComponent();

            _ViewModel = new OptionsWindowViewModel();
            DataContext = _ViewModel;
        }

        private void _SaveSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.TelepipeProxyUrl = _ViewModel.TelepipeProxyUrl;
            Properties.Settings.Default.LargeAddressAwareEnabled = _ViewModel.LargeAddressAwareEnabled;
            Properties.Settings.Default.CloseOnLaunchEnabled = _ViewModel.CloseOnLaunchEnabled;
            Properties.Settings.Default.Save();
            DialogResult = true;
            Close();
        }

        private void _ChangePSO2DirectoryButton_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.LastSelectedInstallLocation = null;
            Properties.Settings.Default.Save();

            Process.Start(Application.ResourceAssembly.Location);
            Application.Current.Shutdown();
        }
    }
}
