using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PSRT.Astra.Models.Phases
{
    public class PSO2DirectoriesPhase
    {
        private InstallConfiguration _InstallConfiguration;

        public PSO2DirectoriesPhase(InstallConfiguration installConfiguration)
        {
            _InstallConfiguration = installConfiguration;
        }

        public async Task RunAsync(CancellationToken ct = default)
        {
            App.Current.Logger.Info(nameof(PSO2DirectoriesPhase), "Creating directories");

            await Task.Run(() =>
            {
                Directory.CreateDirectory(_InstallConfiguration.PSO2BinDirectory);
                Directory.CreateDirectory(_InstallConfiguration.ModsDirectory);
                Directory.CreateDirectory(_InstallConfiguration.DataDirectory);
                Directory.CreateDirectory(_InstallConfiguration.DataLicenseDirectory);
                Directory.CreateDirectory(_InstallConfiguration.DataWin32Directory);
                Directory.CreateDirectory(_InstallConfiguration.DataWin32ScriptDirectory);
            });
        }
    }
}
