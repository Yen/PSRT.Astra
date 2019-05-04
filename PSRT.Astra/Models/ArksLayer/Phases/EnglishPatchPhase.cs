using SharpCompress.Archives.Rar;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PSRT.Astra.Models.ArksLayer.Phases
{
    public class EnglishPatchPhase
    {
        private InstallConfiguration _InstallConfiguration;
        private bool _Enabled;

        public EnglishPatchPhase(InstallConfiguration installConfiguration, bool enabled)
        {
            _InstallConfiguration = installConfiguration;
            _Enabled = enabled;
        }

        public async Task RunAsync(PatchCache patchCache, PluginInfo pluginInfo, CancellationToken ct = default)
        {
            if (_Enabled)
                await _InstallAsync(patchCache, pluginInfo, ct);
            else
                await _RemoveAsync(ct);
        }

        private async Task _InstallAsync(PatchCache patchCache, PluginInfo pluginInfo, CancellationToken ct = default)
        {
            App.Logger.Info(nameof(EnglishPatchPhase), "Verifying translation plugins and data");

            await pluginInfo.BlockTranslation.ValidateAsync(_InstallConfiguration, ct);
            await pluginInfo.ItemTranslation.ValidateAsync(_InstallConfiguration, ct);
            await pluginInfo.TitleTranslation.ValidateAsync(_InstallConfiguration, ct);
            await pluginInfo.TextTranslation.ValidateAsync(_InstallConfiguration, ct);
        }

        private async Task _RemoveAsync(CancellationToken ct)
        {
            await Task.Run(() =>
            {
                App.Logger.Info(nameof(EnglishPatchPhase), "Deleting plugin dlls");

                File.Delete(_InstallConfiguration.ArksLayer.PluginPSO2BlockRenameDll);
                File.Delete(_InstallConfiguration.ArksLayer.PluginPSO2ItemTranslatorDll);
                File.Delete(_InstallConfiguration.ArksLayer.PluginPSO2TitleTranslatorDll);
                File.Delete(_InstallConfiguration.ArksLayer.PluginPSO2RAISERSystemDll);
            });
        }
    }
}
