using PropertyChanged;
using PSRT.Astra.Models;
using PSRT.Astra.Models.Mods;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;

namespace PSRT.Astra.ViewModels
{
    public class FileNameComparer : IEqualityComparer<string>
    {
        public bool Equals(string x, string y) => x.Equals(y, StringComparison.InvariantCultureIgnoreCase);
        public int GetHashCode(string obj) => obj.GetHashCode();
    }

    [AddINotifyPropertyChangedInterface]
    public class ModsWindowViewModel : IDisposable
    {
        private CancellationTokenSource _FileWatcherTokenSource = new CancellationTokenSource();

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

        public bool MoveButtonsEnabled => SelectedModEntry != null;

        public ModsWindowViewModel(InstallConfiguration installConfiguration)
        {
            InstallConfiguration = installConfiguration;

            Task.Factory.StartNew(_FileWatcherLoopAsync, TaskCreationOptions.LongRunning);
        }

        private async Task _FileWatcherLoopAsync()
        {
            try
            {
                while (!_FileWatcherTokenSource.IsCancellationRequested)
                {
                    await _FileWatcherCheckAsync();

                    await Task.Delay(1000);
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error(nameof(MainWindowViewModel), "Error in file watcher loop", ex);
            }
        }

        private async Task _FileWatcherCheckAsync()
        {
            var files = Directory.GetFiles(InstallConfiguration.ModsDirectory)
                .Select(f => Path.GetFileName(f))
                .ToArray();
            var directories = Directory.GetDirectories(InstallConfiguration.ModsDirectory)
                .Select(d => Path.GetFileName(d))
                .ToArray();

            var combinedEntries = files
                .Concat(directories)
                .Distinct(new FileNameComparer())
                .ToArray();

            // have to update the mod entries from the UI thread
            await App.Current.Dispatcher.InvokeAsync(() =>
            {
                var addedEntries = combinedEntries
                    .Except(ModEntries.Select(e => e.Path), new FileNameComparer())
                    .ToArray();

                var deletedEntries = ModEntries
                    .Select(e => e.Path)
                    .Except(combinedEntries, new FileNameComparer())
                    .ToArray();

                var newFiles = files
                    .Where(f => addedEntries.Contains(f, new FileNameComparer()))
                    .ToArray();
                var newDirectories = directories
                    .Where(d => addedEntries.Contains(d, new FileNameComparer()))
                    .ToArray();

                foreach (var file in newFiles)
                    ModEntries.Add(new ModEntry
                    {
                        Enabled = true,
                        Type = ModEntryType.File,
                        Path = file
                    });

                foreach (var directory in newDirectories)
                    ModEntries.Add(new ModEntry
                    {
                        Enabled = true,
                        Type = ModEntryType.Directory,
                        Path = directory
                    });

                foreach (var entry in deletedEntries)
                {
                    var modEntry = ModEntries.FirstOrDefault(e => e.Path.Equals(entry, StringComparison.InvariantCultureIgnoreCase));
                    if (modEntry != null)
                        ModEntries.Remove(modEntry);
                }
            });

            //var fileEntries = files.Select(f => new ModEntry
            //{
            //    Enabled = true,
            //    Type = ModEntryType.File,
            //    Path = Path.GetFileName(f)
            //});

            //var directoryEntries = directories.Select(d => new ModEntry
            //{
            //    Enabled = true,
            //    Type = ModEntryType.Directory,
            //    Path = Path.GetDirectoryName(d)
            //});

            //ModEntries = new ObservableCollection<ModEntry>(directoryEntries.Concat(fileEntries));
        }

        public void Dispose()
        {
            _FileWatcherTokenSource.Cancel();
        }
    }
}
