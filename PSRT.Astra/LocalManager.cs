using GalaSoft.MvvmLight;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using PropertyChanged;

namespace PSRT.Astra
{
    public class LocaleManager : ObservableObject
    {
        public static LocaleManager Instance { get; } = new LocaleManager();

        private CultureInfo _CurrentCulture = CultureInfo.CurrentCulture;
        public CultureInfo CurrentCulture
        {
            get => _CurrentCulture;
            set
            {
                _CurrentCulture = value;
                PropertyChangedHandler?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentCulture)));
            }
        }
        public string this[string key] => Properties.Localization.ResourceManager.GetString(key, CurrentCulture) ?? key;
    }

    [AddINotifyPropertyChangedInterface]
    public class LocaleBindingExtension : Binding
    {
        public static readonly DependencyProperty LocaleKeyProperty =
            DependencyProperty.Register(nameof(LocaleKey), typeof(string), typeof(LocaleBindingExtension));

        public string LocaleKey
        {
            set => Path = new PropertyPath($"[{value}]");
        }

        public LocaleBindingExtension()
        {
            Mode = BindingMode.OneWay;
            Source = LocaleManager.Instance;
        }

        public LocaleBindingExtension(string path) : base($"[{path}]")
        {
            Mode = BindingMode.OneWay;
            Source = LocaleManager.Instance;
        }
    }

    public class LocaleDynamicBindingExtension : MultiBinding
    {
        private class LocalConverter : IMultiValueConverter
        {
            public object Convert(object[] values, Type targetType, object parameter, CultureInfo _culture)
            {
                if (values.Length != 2)
                    throw new Exception();


                var culture = values[0] as CultureInfo;
                var key = values[1] as string;

                return Properties.Localization.ResourceManager.GetString(key, culture) ?? key;
            }

            public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            {
                throw new NotImplementedException();
            }
        }

        public LocaleDynamicBindingExtension(BindingBase binding)
        {
            Mode = BindingMode.OneWay;
            Converter = new LocalConverter();

            (this as IAddChild).AddChild(new Binding("CurrentCulture") { Source = LocaleManager.Instance });
            (this as IAddChild).AddChild(binding);
        }
    }
}
