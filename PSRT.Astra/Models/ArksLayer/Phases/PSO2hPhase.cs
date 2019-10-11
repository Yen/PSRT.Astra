using PSRT.Astra.Models.ArksLayer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PSRT.Astra.Models.ArksLayer.Phases
{
    public class PSO2hPhase
    {
        private InstallConfiguration _InstallConfiguration;
        private bool _Enabled;

        public PSO2hPhase(InstallConfiguration installConfiguration, bool enabled)
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
            App.Logger.Info(nameof(PSO2hPhase), "Validating plugin loader");

            await pluginInfo.PluginLoader.ValidateAsync(_InstallConfiguration, ct);

            App.Logger.Info(nameof(PSO2hPhase), "Writing tweaker.bin");
            var magic = await Task.Run(() => TweakerBin.GenerateFileContents(_InstallConfiguration.PSO2BinDirectory));

            using (var fs = File.Create(_InstallConfiguration.ArksLayer.TweakerBin, 4096, FileOptions.Asynchronous))
            using (var writer = new StreamWriter(fs))
                await writer.WriteLineAsync(magic);

            App.Logger.Info(nameof(PSO2hPhase), "Getting version.ver contents");
            using (var client = new ArksLayerHttpClient())
            using (var response = await client.GetAsync(DownloadConfiguration.VersionFile, ct))
            {
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();

                App.Logger.Info(nameof(PSO2hPhase), "Writing version.ver");
                using (var fs = File.Create(_InstallConfiguration.ArksLayer.VersionFile, 4096, FileOptions.Asynchronous))
                using (var writer = new StreamWriter(fs))
                    await writer.WriteAsync(content);
            }
        }

        private async Task _RemoveAsync(CancellationToken ct = default)
        {
            await Task.Run(() =>
            {
                App.Logger.Info(nameof(PSO2hPhase), "Removing pso2h dlls");
                File.Delete(_InstallConfiguration.ArksLayer.DDrawDll);
                File.Delete(_InstallConfiguration.ArksLayer.PSO2hDll);

                App.Logger.Info(nameof(PSO2hPhase), "Removing tweaker.bin");
                File.Delete(_InstallConfiguration.ArksLayer.TweakerBin);

                App.Logger.Info(nameof(PSO2hPhase), "Removing version.ver");
                File.Delete(_InstallConfiguration.ArksLayer.VersionFile);
            });
        }
    }
}
