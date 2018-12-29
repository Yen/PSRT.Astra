using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PSRT.Astra.Models.Phases
{
    public class ModFilesPhase
    {
        private InstallConfiguration _InstallConfiguration;

        public ModFilesPhase(InstallConfiguration installConfiguration)
        {
            _InstallConfiguration = installConfiguration;
        }

        public async Task RunAsync(CancellationToken ct = default)
        {
            await Task.Run(() =>
            {
                var modFiles = Directory.GetFiles(_InstallConfiguration.ModsDirectory)
                    .Select(p => Path.GetFileName(p))
                    .ToArray();

                if (modFiles.Length > 0)
                {
                    App.Current.Logger.Info(nameof(ModFilesPhase), $"Copying {modFiles.Length} file{(modFiles.Length == 1 ? string.Empty : "s")}");

                    foreach (var file in modFiles)
                    {
                        App.Current.Logger.Info(nameof(ModFilesPhase), $"Copying {file}");

                        var dataPath = Path.Combine(_InstallConfiguration.DataWin32Directory, file);
                        File.Delete(dataPath);
                        File.Copy(Path.Combine(_InstallConfiguration.ModsDirectory, file), dataPath, true);
                    }
                }
            });
        }
    }
}
