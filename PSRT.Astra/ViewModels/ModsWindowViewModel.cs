using PropertyChanged;
using PSRT.Astra.Models.Mods;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace PSRT.Astra.ViewModels
{
    [AddINotifyPropertyChangedInterface]
    public class ModsWindowViewModel
    {
        public bool ModsEnabled { get; set; }

        public Brush ModsEnabledBarBackground => ModsEnabled
            ? new SolidColorBrush(Color.FromRgb(170, 238, 178))
            : new SolidColorBrush(Color.FromRgb(238, 221, 170));
        public string ModsEnabledBarMessage => ModsEnabled ? "Mods Enabled" : "Mods Disabled";
        public string ModsEnabledBarToggleText => ModsEnabled ? "Disable Mods" : "Enable Mods";

        public ObservableCollection<ModEntry> ModEntries { get; set; } = new ObservableCollection<ModEntry>()
        {
            new ModEntry
            {
                Enabled = true,
                Type = ModEntryType.Directory,
                Path = "aaa"
            },
            new ModEntry
            {
                Enabled = false,
                Type = ModEntryType.File,
                Path = "bbb"
            },
            new ModEntry
            {
                Enabled = true,
                Type = ModEntryType.Directory,
                Path = "aaa"
            },
            new ModEntry
            {
                Enabled = false,
                Type = ModEntryType.File,
                Path = "bbb"
            },
            new ModEntry
            {
                Enabled = true,
                Type = ModEntryType.Directory,
                Path = "aaa"
            },
            new ModEntry
            {
                Enabled = false,
                Type = ModEntryType.File,
                Path = "bbb"
            },
            new ModEntry
            {
                Enabled = true,
                Type = ModEntryType.Directory,
                Path = "aaa"
            },
            new ModEntry
            {
                Enabled = false,
                Type = ModEntryType.File,
                Path = "bbb"
            },
            new ModEntry
            {
                Enabled = true,
                Type = ModEntryType.Directory,
                Path = "aaa"
            },
            new ModEntry
            {
                Enabled = false,
                Type = ModEntryType.File,
                Path = "bbb"
            },
            new ModEntry
            {
                Enabled = true,
                Type = ModEntryType.Directory,
                Path = "aaa"
            },
            new ModEntry
            {
                Enabled = false,
                Type = ModEntryType.File,
                Path = "bbb"
            },
            new ModEntry
            {
                Enabled = true,
                Type = ModEntryType.Directory,
                Path = "aaa"
            },
            new ModEntry
            {
                Enabled = false,
                Type = ModEntryType.File,
                Path = "bbb"
            },
            new ModEntry
            {
                Enabled = true,
                Type = ModEntryType.Directory,
                Path = "aaa"
            },
            new ModEntry
            {
                Enabled = false,
                Type = ModEntryType.File,
                Path = "bbb"
            },
        };
    }
}
