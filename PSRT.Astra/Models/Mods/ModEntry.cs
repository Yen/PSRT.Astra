using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSRT.Astra.Models.Mods
{
    [AddINotifyPropertyChangedInterface]
    public class ModEntry
    {
        public bool Enabled { get; set; }
        public ModEntryType Type { get; set; }
        public string Path { get; set; }
    }
}
