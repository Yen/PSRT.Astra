using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSRT.Astra.Models.Mods
{
    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    public class ModEntryRecord
    {
        [JsonProperty(PropertyName = "name", Required = Required.Always)]
        public string Name;

        [JsonProperty(PropertyName = "enabled", Required = Required.Always)]
        public bool Enabled;

        [JsonProperty(PropertyName = "type", Required = Required.Always)]
        private string _Type
        {
            get => Type == ModEntryType.Directory ? "directory" : "file";
            set => Type = value == "directory" ? ModEntryType.Directory : ModEntryType.File;
        }

        public ModEntryType Type;
    }
}
