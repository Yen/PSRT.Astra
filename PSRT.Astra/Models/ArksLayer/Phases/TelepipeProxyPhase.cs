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
        private bool _Enabled;

        public TelepipeProxyPhase(InstallConfiguration installConfiguration, bool enabled)
        {
            _InstallConfiguration = installConfiguration;
            _Enabled = enabled;
        }

        public async Task RunAsync(PluginInfo pluginInfo, CancellationToken ct = default)
        {
            if (_Enabled)
                await _InstallAsync(pluginInfo, ct);
            else
                await _RemoveAsync(ct);
        }

        private async Task _InstallAsync(PluginInfo pluginInfo, CancellationToken ct = default)
        {
            var proxyUrl = Properties.Settings.Default.TelepipeProxyUrl;
            bool isCustomProxy = !string.IsNullOrWhiteSpace(proxyUrl);
            string url = isCustomProxy ? proxyUrl : "http://telepipe.io/config.json";

            App.Logger.Info(nameof(TelepipeProxyPhase), $"Downloading config from {proxyUrl}");

            using (var client = new HttpClient())
            {
                var configString = await client.GetStringAsync(url);
                var config = JsonConvert.DeserializeObject<ProxyInfo>(configString);
                var publicKey = await client.GetByteArrayAsync(config.PublicKeyUrl);

                App.Logger.Info(nameof(TelepipeProxyPhase), "Writing config file");
                await Task.Run(() =>
                    File.WriteAllText(_InstallConfiguration.ArksLayer.TelepipeProxyConfig, config.Host));

                App.Logger.Info(nameof(TelepipeProxyPhase), "Writing public key");
                await Task.Run(() =>
                    File.WriteAllBytes(_InstallConfiguration.ArksLayer.TelepipeProxyPublicKey, publicKey));
            }

            App.Logger.Info(nameof(TelepipeProxyPhase), "Validating telepipe plugin dll");

            await pluginInfo.TelepipeProxyDll.ValidateFileAsync(_InstallConfiguration.ArksLayer.PluginTelepipeProxyDll, ct);
        }

        private async Task _RemoveAsync(CancellationToken ct = default)
        {
            await Task.Run(() =>
            {
                App.Logger.Info(nameof(TelepipeProxyPhase), "Deleting config file");
                File.Delete(_InstallConfiguration.ArksLayer.TelepipeProxyConfig);

                App.Logger.Info(nameof(TelepipeProxyPhase), "Deleting public key");
                File.Delete(_InstallConfiguration.ArksLayer.TelepipeProxyPublicKey);

                App.Logger.Info(nameof(TelepipeProxyPhase), "Deleting telepipe proxy dll");
                File.Delete(_InstallConfiguration.ArksLayer.PluginTelepipeProxyDll);
            });
        }
    }
}
