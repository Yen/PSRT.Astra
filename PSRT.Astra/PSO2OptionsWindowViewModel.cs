using GalaSoft.MvvmLight.Command;
using Neo.IronLua;
using PropertyChanged;
using PSRT.Astra.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace PSRT.Astra
{
    static class LuaTableExtensions
    {
        public static void SetValueEx(this LuaTable table, IEnumerable<string> keys, object value)
        {
            if (keys.Count() == 0)
                return;

            if (keys.Count() == 1)
            {
                table[keys.First()] = value;
                return;
            }

            var nested = table[keys.First()] as LuaTable;
            if (nested == null)
                return;

            SetValueEx(nested, keys.Skip(1), value);
        }
    }

    [AddINotifyPropertyChangedInterface]
    public class PSO2OptionsWindowViewModel
    {
        public enum WindowMode
        {
            Windowed,
            ExclusiveFullscreen,
            BorderlessFullscreen
        }

        [AddINotifyPropertyChangedInterface]
        public class TypedItem<T>
            where T : Enum
        {
            public string LocaleKey { get; set; }
            public T Type { get; set; }
        }

        public RelayCommand SaveCommand => new RelayCommand(async () => await SaveAndCloseAsync());

        public Action CloseAction { get; set; }

        private int _ActivityCount { get; set; } = 0;
        public bool Ready => _ActivityCount == 0;

        private LuaTable _Table;

        public ObservableCollection<TypedItem<WindowMode>> WindowModeItems { get; set; } = new ObservableCollection<TypedItem<WindowMode>>()
        {
            new TypedItem<WindowMode>() { LocaleKey = "PSO2OptionsWindow_Windowed", Type = WindowMode.Windowed },
            new TypedItem<WindowMode>() { LocaleKey = "PSO2OptionsWindow_ExclusiveFullscreen", Type = WindowMode.ExclusiveFullscreen },
            new TypedItem<WindowMode>() { LocaleKey = "PSO2OptionsWindow_BorderlessFullscreen", Type = WindowMode.BorderlessFullscreen }
        };
        public TypedItem<WindowMode> WindowModeSelected { get; set; }

        public async Task InitializeAsync()
        {
            _ActivityCount += 1;

            _Table = await _LoadOptionsFileAsync();
            _RefreshFromTable(_Table);

            _ActivityCount -= 1;
        }

        public async Task SaveAndCloseAsync()
        {
            _ActivityCount += 1;

            switch (WindowModeSelected?.Type)
            {
                case WindowMode.Windowed:
                    _Table.SetValueEx(new[] { "Windows", "FullScreen" }, false);
                    _Table.SetValueEx(new[] { "Windows", "VirtualFullScreen" }, false);
                    break;
                case WindowMode.ExclusiveFullscreen:
                    _Table.SetValueEx(new[] { "Windows", "FullScreen" }, true);
                    _Table.SetValueEx(new[] { "Windows", "VirtualFullScreen" }, false);
                    break;
                case WindowMode.BorderlessFullscreen:
                    _Table.SetValueEx(new[] { "Windows", "FullScreen" }, false);
                    _Table.SetValueEx(new[] { "Windows", "VirtualFullScreen" }, true);
                    break;
            }

            _RefreshFromTable(_Table);
            await _SaveOptionsFileAsync(_Table);

            CloseAction();

            _ActivityCount -= 1;
        }

        private void _RefreshFromTable(LuaTable table)
        {
            // window mode
            if (table["Windows", "FullScreen"] as bool? ?? false)
                WindowModeSelected = WindowModeItems.FirstOrDefault(x => x.Type == WindowMode.ExclusiveFullscreen);
            else if (table["Windows", "VirtualFullScreen"] as bool? ?? false)
                WindowModeSelected = WindowModeItems.FirstOrDefault(x => x.Type == WindowMode.BorderlessFullscreen);
            else
                WindowModeSelected = WindowModeItems.FirstOrDefault(x => x.Type == WindowMode.Windowed);
        }

        private async Task<LuaTable> _LoadOptionsFileAsync()
        {
            _ActivityCount += 1;

            using (var fs = new FileStream(InstallConfiguration.PSO2DocumentsUserFile, FileMode.Open, FileAccess.Read))
            using (var reader = new StreamReader(fs))
            {
                var lua = await reader.ReadToEndAsync();
                var lson = Regex.Replace(lua, @"^\s*Ini\s*=\s*", string.Empty);
                var table = await Task.Run(() => LuaTable.FromLson(lson));

                _ActivityCount -= 1;
                return table;
            }
        }

        private async Task _SaveOptionsFileAsync(LuaTable table)
        {
            _ActivityCount += 1;

            var lson = await Task.Run(() => table.ToLson());
            using (var fs = new FileStream(InstallConfiguration.PSO2DocumentsUserFile, FileMode.Create))
            using (var writer = new StreamWriter(fs))
            {
                var data = $"Ini = {lson}";
                await writer.WriteAsync(data);
            }

            _ActivityCount -= 1;
        }
    }
}
