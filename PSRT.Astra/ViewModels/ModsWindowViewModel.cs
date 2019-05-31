using Newtonsoft.Json;
using PropertyChanged;
using PSRT.Astra.Models;
using PSRT.Astra.Models.Mods;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;

namespace PSRT.Astra.ViewModels
{
    [AddINotifyPropertyChangedInterface]
    public class ModsWindowViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private CancellationTokenSource _BackgroundTokenSource = new CancellationTokenSource();

        public InstallConfiguration InstallConfiguration;

        public bool ModFilesEnabled
        {
            get => Properties.Settings.Default.ModFilesEnabled;
            set => Properties.Settings.Default.ModFilesEnabled = value;
        }

        public Brush ModsEnabledBarBackground => ModFilesEnabled
            ? new SolidColorBrush(Color.FromRgb(170, 238, 178))
            : new SolidColorBrush(Color.FromRgb(238, 221, 170));
        public string ModsEnabledBarMessage => ModFilesEnabled ? "Mods Enabled" : "Mods Disabled";
        public string ModsEnabledBarToggleText => ModFilesEnabled ? "Disable Mods" : "Enable Mods";

        public ObservableCollection<ModEntry> ModEntries { get; private set; } = new ObservableCollection<ModEntry>();
        public ModEntry SelectedModEntry { get; set; }

        public bool MoveUpButtonEnabled => SelectedModEntry != null && ModEntries.IndexOf(SelectedModEntry) > 0;
        public bool MoveDownButtonEnabled => SelectedModEntry != null && ModEntries.IndexOf(SelectedModEntry) < ModEntries.Count - 1;

        private Task _FileWatcherLoopTask;

        public ModsWindowViewModel(InstallConfiguration installConfiguration)
        {
            InstallConfiguration = installConfiguration;

            _FileWatcherLoopTask = Task.Factory.StartNew(_FileWatcherLoopAsync, TaskCreationOptions.LongRunning);
        }

        private async Task _FileWatcherLoopAsync()
        {
            try
            {
                while (!_BackgroundTokenSource.IsCancellationRequested)
                {
                    await _FileWatcherCheckAsync();

                    try
                    {
                        await Task.Delay(1000, _BackgroundTokenSource.Token);
                    }
                    catch
                    {
                        // dont care
                    }
                }
                await _FileWatcherCheckAsync();
            }
            catch (Exception ex)
            {
                App.Logger.Error(nameof(MainWindowViewModel), "Error in file watcher loop", ex);
                throw;
            }
        }

        private async Task _FileWatcherCheckAsync()
        {
            if (File.Exists(InstallConfiguration.ModsConfigurationFile))
            {
                using (var fs = File.OpenRead(InstallConfiguration.ModsConfigurationFile))
                using (var reader = new StreamReader(fs))
                {
                    var json = await reader.ReadToEndAsync();
                    var entryRecords = JsonConvert.DeserializeObject<ModEntryRecord[]>(json);

                    await App.Current.Dispatcher.InvokeAsync(() =>
                    {
                        foreach (var record in entryRecords)
                        {
                            var existingEntry = ModEntries.FirstOrDefault(e => e.Name == record.Name);
                            if (existingEntry == null)
                                ModEntries.Add(new ModEntry()
                                {
                                    Name = record.Name,
                                    Type = record.Type,
                                    Enabled = record.Enabled
                                });
                        }
                    });
                }
            }

            var files = Directory.GetFiles(InstallConfiguration.ModsDirectory)
                .Select(f => Path.GetFileName(f))
                .Where(f => !f.Equals(Path.GetFileName(InstallConfiguration.ModsConfigurationFile), StringComparison.InvariantCultureIgnoreCase))
                .ToArray();
            var directories = Directory.GetDirectories(InstallConfiguration.ModsDirectory)
                .Select(d => Path.GetFileName(d))
                .ToArray();

            var combinedEntries = files
                .Concat(directories)
                .Distinct(new FileNameEqualityComparer())
                .ToArray();

            // have to update the mod entries from the UI thread
            await App.Current.Dispatcher.InvokeAsync(() =>
            {
                var addedEntries = combinedEntries
                    .Except(ModEntries.Select(e => e.Name), new FileNameEqualityComparer())
                    .ToArray();

                var deletedEntries = ModEntries
                    .Select(e => e.Name)
                    .Except(combinedEntries, new FileNameEqualityComparer())
                    .ToArray();

                var newFiles = files
                    .Where(f => addedEntries.Contains(f, new FileNameEqualityComparer()))
                    .ToArray();
                var newDirectories = directories
                    .Where(d => addedEntries.Contains(d, new FileNameEqualityComparer()))
                    .ToArray();

                foreach (var file in newFiles)
                    ModEntries.Add(new ModEntry
                    {
                        Enabled = true,
                        Type = ModEntryType.File,
                        Name = file
                    });

                foreach (var directory in newDirectories)
                    ModEntries.Add(new ModEntry
                    {
                        Enabled = true,
                        Type = ModEntryType.Directory,
                        Name = directory
                    });

                foreach (var entry in deletedEntries)
                {
                    var modEntry = ModEntries.FirstOrDefault(e => e.Name.Equals(entry, StringComparison.InvariantCultureIgnoreCase));
                    if (modEntry != null)
                        ModEntries.Remove(modEntry);
                }
            });

            {
                var entries = await App.Current.Dispatcher.InvokeAsync(() => ModEntries.ToArray());
                var json = JsonConvert.SerializeObject(entries.Select(e => new ModEntryRecord
                {
                    Name = e.Name,
                    Type = e.Type,
                    Enabled = e.Enabled
                }), Formatting.Indented);

                using (var fs = File.Create(InstallConfiguration.ModsConfigurationFile))
                using (var writer = new StreamWriter(fs))
                    await writer.WriteAsync(json);
            }
        }

        public void MoveUpEntry()
        {
            if (SelectedModEntry == null)
                return;

            var index = ModEntries.IndexOf(SelectedModEntry);
            if (index > 0)
            {
                ModEntries.Move(index, index - 1);

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MoveUpButtonEnabled)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MoveDownButtonEnabled)));
            }
        }

        public void MoveDownEntry()
        {
            if (SelectedModEntry == null)
                return;

            var index = ModEntries.IndexOf(SelectedModEntry);
            if (index < ModEntries.Count - 1)
            {
                ModEntries.Move(index, index + 1);

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MoveUpButtonEnabled)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MoveDownButtonEnabled)));
            }
        }

        public async Task DisposeAsync()
        {
            _BackgroundTokenSource.Cancel();

            await _FileWatcherLoopTask;
        }
    }
}
