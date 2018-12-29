using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSRT.Astra.Models.Phases
{
    public class DeleteCensorFilePhase
    {
        private InstallConfiguration _InstallConfiguration;

        public DeleteCensorFilePhase(InstallConfiguration installConfiguration)
        {
            _InstallConfiguration = installConfiguration;
        }

        public async Task RunAsync()
        {
            await Task.Run(() => File.Delete(_InstallConfiguration.CensorFile));
        }
    }
}
