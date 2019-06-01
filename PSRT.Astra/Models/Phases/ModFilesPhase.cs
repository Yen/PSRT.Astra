using Newtonsoft.Json;
using PSRT.Astra.Models.Mods;
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

        public async Task<string[]> RunAsync(CancellationToken ct = default)
        {
            var configuredEntries = new ModEntryRecord[0];
            if (File.Exists(_InstallConfiguration.ModsConfigurationFile))
            {
                App.Logger.Info(nameof(ModFilesPhase), "Mod configuration file found, reading entries");
                using (var fs = File.OpenRead(_InstallConfiguration.ModsConfigurationFile))
                using (var reader = new StreamReader(fs))
                {
                    var json = await reader.ReadToEndAsync();
                    configuredEntries = JsonConvert.DeserializeObject<ModEntryRecord[]>(json);
                }
            }

            var unconfiguredFiles = Directory.GetFiles(_InstallConfiguration.ModsDirectory)
                .Select(p => Path.GetFileName(p))
                .Where(p => !p.Equals(Path.GetFileName(_InstallConfiguration.ModsConfigurationFile), StringComparison.CurrentCultureIgnoreCase))
                .Where(p => !configuredEntries.Any(e => e.Name.Equals(p, StringComparison.InvariantCultureIgnoreCase)))
                .ToArray();

            var unconfiguredDirectories = Directory.GetDirectories(_InstallConfiguration.ModsDirectory)
                .Select(p => Path.GetFileName(p))
                .Where(p => !configuredEntries.Any(e => e.Name.Equals(p, StringComparison.InvariantCultureIgnoreCase)))
                .ToArray();

            var unconfiguredFileEntries = unconfiguredFiles.Select(p => new ModEntryRecord
            {
                Name = p,
                Type = ModEntryType.File,
                Enabled = true
            });

            var unconfiguredDirectoryEntries = unconfiguredDirectories.Select(p => new ModEntryRecord
            {
                Name = p,
                Type = ModEntryType.Directory,
                Enabled = true
            });

            var unconfiguredEntries = unconfiguredFileEntries
                .Concat(unconfiguredDirectoryEntries)
                .OrderBy(e => e.Name)
                .ToArray();

            var modEntries = configuredEntries
                .Where(e => e.Enabled)
                .Concat(unconfiguredEntries)
                .ToArray();

            var modFiles = new List<string>();
            await Task.Run(() =>
            {
                foreach (var entry in modEntries)
                {
                    if (entry.Type == ModEntryType.File)
                    {
                        if (!modFiles.Any(f => Path.GetFileName(f).Equals(entry.Name, StringComparison.InvariantCultureIgnoreCase)))
                            modFiles.Add(entry.Name);

                        continue;
                    }

                    if (entry.Type == ModEntryType.Directory)
                    {
                        var entryFiles = Directory.GetFiles(Path.Combine(_InstallConfiguration.ModsDirectory, entry.Name))
                            .Select(f => Path.Combine(entry.Name, Path.GetFileName(f)))
                            .ToArray();

                        foreach (var file in entryFiles)
                            if (!modFiles.Any(f => Path.GetFileName(f).Equals(Path.GetFileName(file), StringComparison.InvariantCultureIgnoreCase)))
                                modFiles.Add(file);

                        continue;
                    }

                    throw new NotImplementedException();
                }
            });

            if (modFiles.Count > 0)
            {
                App.Logger.Info(nameof(ModFilesPhase), $"Copying {modFiles.Count} file{(modFiles.Count == 1 ? string.Empty : "s")}");

                await Task.Run(() =>
                {
                    foreach (var file in modFiles)
                    {
                        App.Logger.Info(nameof(ModFilesPhase), $"Copying {file}");

                        var dataPath = Path.Combine(_InstallConfiguration.DataWin32Directory, Path.GetFileName(file));
                        File.Delete(dataPath);
                        File.Copy(Path.Combine(_InstallConfiguration.ModsDirectory, file), dataPath, true);
                    }
                });
            }

            return modFiles.Select(f => Path.GetFileName(f)).ToArray();
        }
    }
}
