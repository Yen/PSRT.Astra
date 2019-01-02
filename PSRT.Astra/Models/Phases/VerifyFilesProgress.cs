using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSRT.Astra.Models.Phases
{
    [AddINotifyPropertyChangedInterface]
    public class VerifyFilesProgress
    {
        public bool IsIndeterminate { get; set; } = true;
        public double Progress { get; set; }

        public int TotalCount { get; set; }
        public int CompletedCount { get; set; }
    }
}
