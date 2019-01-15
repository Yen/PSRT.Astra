using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSRT.Astra.ViewModels
{
    [AddINotifyPropertyChangedInterface]
    class OptionsWindowViewModel
    {
        public string TelepipeProxyUrl { get; set; } = Properties.Settings.Default.TelepipeProxyUrl;

        public bool TelepipeProxyUrlValid
            => string.IsNullOrWhiteSpace(TelepipeProxyUrl)
            || (Uri.TryCreate(TelepipeProxyUrl, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps));

        public bool LargeAddressAwareEnabled { get; set; } = Properties.Settings.Default.LargeAddressAwareEnabled;

        public bool CloseOnLaunchEnabled { get; set; } = Properties.Settings.Default.CloseOnLaunchEnabled;

        public bool SettingsValid => TelepipeProxyUrlValid;
    }
}
