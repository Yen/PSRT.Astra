using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSRT.Astra
{
    [AddINotifyPropertyChangedInterface]
    public class PSO2OptionsWindowViewModel
    {
        private int _ActivityCount { get; set; } = 0;
        public bool Ready => _ActivityCount == 0;

        public async Task InitializeAsync()
        {

        }
    }
}
