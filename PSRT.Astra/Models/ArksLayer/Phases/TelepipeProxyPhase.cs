using Newtonsoft.Json;
using PSRT.Astra.Models.ArksLayer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PSRT.Astra.Models.ArksLayer.Phases
{
    public class TelepipeProxyPhase
    {
        private InstallConfiguration _InstallConfiguration;
        private PluginInfo _PluginInfo;
        private bool _Enabled;

        public TelepipeProxyPhase(InstallConfiguration installConfiguration, PluginInfo pluginInfo, bool enabled)
        {
            _InstallConfiguration = installConfiguration;
            _PluginInfo = pluginInfo;
            _Enabled = enabled;
        }

        public async Task RunAsync(CancellationToken ct = default)
        {
            if (_Enabled)
                await _InstallAsync(ct);
            else
                await _RemoveAsync(ct);
        }

        private async Task _InstallAsync(CancellationToken ct = default)
        {
            var proxyUrl = Properties.Settings.Default.TelepipeProxyUrl;
            bool isCustomProxy = !string.IsNullOrWhiteSpace(proxyUrl);
            string url = isCustomProxy ? proxyUrl : "http://telepipe.io/config.json";

            App.Current.Logger.Info(nameof(TelepipeProxyPhase), $"Downloading config from {proxyUrl}");

            using (var client = new HttpClient())
            {
                var configString = await client.GetStringAsync(url);
                var config = JsonConvert.DeserializeObject<ProxyInfo>(configString);
                var publicKey = await client.GetByteArrayAsync(config.PublicKeyUrl);

                App.Current.Logger.Info(nameof(TelepipeProxyPhase), "Writing config file");
                await Task.Run(() =>
                    File.WriteAllText(_InstallConfiguration.ArksLayer.TelepipeProxyConfig, config.Host));

                App.Current.Logger.Info(nameof(TelepipeProxyPhase), "Writing public key");
                await Task.Run(() =>
                    File.WriteAllBytes(_InstallConfiguration.ArksLayer.TelepipeProxyPublicKey, publicKey));
            }

            App.Current.Logger.Info(nameof(TelepipeProxyPhase), "Validating telepipe plugin dll");

            await _PluginInfo.TelepipeProxyDll.ValidateFileAsync(_InstallConfiguration.ArksLayer.PluginTelepipeProxyDll, ct);
        }

        private async Task _RemoveAsync(CancellationToken ct = default)
        {
            await Task.Run(() =>
            {
                App.Current.Logger.Info(nameof(TelepipeProxyPhase), "Deleting config file");
                File.Delete(_InstallConfiguration.ArksLayer.TelepipeProxyConfig);

                App.Current.Logger.Info(nameof(TelepipeProxyPhase), "Deleting public key");
                File.Delete(_InstallConfiguration.ArksLayer.TelepipeProxyPublicKey);

                App.Current.Logger.Info(nameof(TelepipeProxyPhase), "Deleting telepipe proxy dll");
                File.Delete(_InstallConfiguration.ArksLayer.PluginTelepipeProxyDll);
            });
        }
    }
}
