using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PSRT.Astra.Views
{
    /// <summary>
    /// Interaction logic for PhaseControl.xaml
    /// </summary>
    [ContentProperty(nameof(Child))]
    public partial class PhaseControl : UserControl
    {
        #region Converters

        public static readonly PhaseStateBrushConverter PhaseStateBrushConverterInstance = new PhaseStateBrushConverter();
        public class PhaseStateBrushConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                if (value is State state && targetType == typeof(Brush))
                {
                    switch (value)
                    {
                        case State.Queued:
                            return Brushes.LightGray;
                        case State.Running:
                            return Brushes.Yellow;
                        case State.Success:
                            return Brushes.Lime;
                        case State.Error:
                            return Brushes.Red;
                        case State.Canceled:
                            return Brushes.DeepSkyBlue;
                    }
                }

                return Binding.DoNothing;
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                throw new NotImplementedException();
            }
        }

        #endregion Converters

        public enum State
        {
            Queued,
            Running,
            Success,
            Error,
            Canceled
        }

        public State PhaseState
        {
            get => (State)GetValue(PhaseStateProperty);
            set => SetValue(PhaseStateProperty, value);
        }

        public static readonly DependencyProperty PhaseStateProperty
            = DependencyProperty.Register(
                nameof(PhaseState),
                typeof(State),
                typeof(PhaseControl),
                new FrameworkPropertyMetadata(State.Queued)
            );

        public UIElement Child
        {
            get => (UIElement)GetValue(ChildProperty);
            set => SetValue(ChildProperty, value);
        }

        public static readonly DependencyProperty ChildProperty
            = DependencyProperty.Register(
                nameof(Child),
                typeof(UIElement),
                typeof(PhaseControl),
                new FrameworkPropertyMetadata()
            );

        public string Title
        {
            get => (string)GetValue(ChildProperty);
            set => SetValue(ChildProperty, value);
        }

        public static readonly DependencyProperty TitleProperty
            = DependencyProperty.Register(
                nameof(Title),
                typeof(string),
                typeof(PhaseControl),
                new FrameworkPropertyMetadata()
            );

        public PhaseControl()
        {
            InitializeComponent();
        }
    }
}
