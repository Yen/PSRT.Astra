using GalaSoft.MvvmLight;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

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
                PropertyChangedHandler?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
            }
        }
        public string this[string key] => Properties.Localization.ResourceManager.GetString(key, CurrentCulture);
    }

    public class LocaleBindingExtension : Binding
    {
        public LocaleBindingExtension(string path) : base($"[{path}]")
        {
            Mode = BindingMode.OneWay;
            Source = LocaleManager.Instance;
        }
    }
}
