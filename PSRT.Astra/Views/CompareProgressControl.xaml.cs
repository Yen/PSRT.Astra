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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PSRT.Astra.Views
{
    /// <summary>
    /// Interaction logic for CompareProgressControl.xaml
    /// </summary>
    public partial class CompareProgressControl : UserControl
    {
        public double Progress
        {
            get => (double)GetValue(ProgressProperty);
            set => SetValue(ProgressProperty, value);
        }

        public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
            nameof(Progress),
            typeof(double),
            typeof(CompareProgressControl));

        public bool IsIndeterminate
        {
            get => (bool)GetValue(IsIndeterminateProperty);
            set => SetValue(IsIndeterminateProperty, value);
        }

        public static readonly DependencyProperty IsIndeterminateProperty = DependencyProperty.Register(
            nameof(IsIndeterminate),
            typeof(bool),
            typeof(CompareProgressControl));

        public CompareProgressControl()
        {
            InitializeComponent();
        }
    }
}
