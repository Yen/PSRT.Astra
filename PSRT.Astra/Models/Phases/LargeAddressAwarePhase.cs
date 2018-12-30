using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PSRT.Astra.Models.Phases
{
    public class LargeAddressAwarePhase
    {
        private InstallConfiguration _InstallConfiguration;

        public LargeAddressAwarePhase(InstallConfiguration installConfiguration)
        {
            _InstallConfiguration = installConfiguration;
        }

        public async Task RunAsync(CancellationToken ct = default)
        {
            await Task.Run(() => LargeAddressAware.ApplyLargeAddressAwarePatch(_InstallConfiguration));
        }
    }
}
