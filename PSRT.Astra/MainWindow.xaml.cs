using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace PSRT.Astra
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private MainWindowViewModel _ViewModel;

        public MainWindow(string pso2BinPath)
        {
            InitializeComponent();

            _ViewModel = new MainWindowViewModel(pso2BinPath);
            DataContext = _ViewModel;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await _ViewModel.InitializeAsync();
            await _ViewModel.VerifyGameFilesAsync();
        }

        private void _Log_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (sender is ListView list)
            {
                if (list.Items.Count > 0)
                    list.ScrollIntoView(list.Items[list.Items.Count - 1]);
            }
        }

        private async void _SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            if (!await _ViewModel.CanOpenSettingsAsync())
                return;

            var window = new PSO2OptionsWindow();
            window.ShowDialog();
        }

        private async void _AstraSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            if (!await _ViewModel.CanOpenSettingsAsync())
                return;

            var window = new AstraOptionsWindow();
            window.ShowDialog();
        }
    }
}
