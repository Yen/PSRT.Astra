using PSRT.Astra.ViewModels;
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

namespace PSRT.Astra.Views
{
    /// <summary>
    /// Interaction logic for ModsWindow.xaml
    /// </summary>
    public partial class ModsWindow : Window
    {
        private ModsWindowViewModel _ViewModel;

        public ModsWindow()
        {
            InitializeComponent();

            _ViewModel = new ModsWindowViewModel();
            DataContext = _ViewModel;
        }
    }
}
