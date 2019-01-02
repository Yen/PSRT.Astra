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
            App.Current.Logger.Info(nameof(PSO2hPhase), "Validating pso2h dlls");

            await pluginInfo.DDrawDll.ValidateFileAsync(_InstallConfiguration.ArksLayer.DDrawDll, ct);
            await pluginInfo.PSO2hDll.ValidateFileAsync(_InstallConfiguration.ArksLayer.PSO2hDll, ct);

            App.Current.Logger.Info(nameof(PSO2hPhase), "Writing tweaker.bin");
            var magic = await Task.Run(() => TweakerBin.GenerateFileContents(_InstallConfiguration.PSO2BinDirectory));

            using (var fs = File.Create(_InstallConfiguration.ArksLayer.TweakerBin, 4096, FileOptions.Asynchronous))
            using (var writer = new StreamWriter(fs))
                await writer.WriteLineAsync(magic);
        }

        private async Task _RemoveAsync(CancellationToken ct = default)
        {
            await Task.Run(() =>
            {
                App.Current.Logger.Info(nameof(PSO2hPhase), "Removing pso2h dlls");

                File.Delete(_InstallConfiguration.ArksLayer.DDrawDll);
                File.Delete(_InstallConfiguration.ArksLayer.PSO2hDll);

                App.Current.Logger.Info(nameof(PSO2hPhase), "Removing tweaker.bin");

                File.Delete(_InstallConfiguration.ArksLayer.TweakerBin);
            });
        }
    }
}
