using Neo.IronLua;
using PropertyChanged;
using PSRT.Astra.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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
        private int _ActivityCount { get; set; } = 0;
        public bool Ready => _ActivityCount == 0;

        private LuaTable _Table;

        public async Task InitializeAsync()
        {
            _ActivityCount += 1;

            _Table = await _LoadOptionsFileAsync();

            _ActivityCount -= 1;
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
