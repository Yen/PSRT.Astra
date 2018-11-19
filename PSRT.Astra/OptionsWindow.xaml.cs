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

namespace PSRT.Astra
{
    /// <summary>
    /// Interaction logic for AstraOptionsWindow.xaml
    /// </summary>
    public partial class AstraOptionsWindow : Window
    {
        public AstraOptionsWindow()
        {
            InitializeComponent();
        }

        private void _SaveSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.Save();
            Close();
        }
    }
}
