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

        public enum ShaderQuality
        {
            Low = 0,
            Standard = 1,
            High = 2
        }

        public enum TextureQuality
        {
            Low = 0,
            Standard = 1,
            High = 2
        }

        public enum InterfaceScale
        {
            Scale1x = 0,
            Scale1_25x = 1,
            Scale1_5x = 2
        }

        [AddINotifyPropertyChangedInterface]
        public class TaggedItem<T>
        {
            public string LocaleKey { get; set; }
            public T Tag { get; set; }
        }

        public RelayCommand SaveCommand => new RelayCommand(async () => await SaveAndCloseAsync());

        public Action CloseAction { get; set; }

        private int _ActivityCount { get; set; } = 0;
        public bool Ready => _ActivityCount == 0;

        private LuaTable _Table;

        public ObservableCollection<TaggedItem<WindowMode>> WindowModeItems { get; set; } = new ObservableCollection<TaggedItem<WindowMode>>()
        {
            new TaggedItem<WindowMode>() { LocaleKey = "PSO2OptionsWindow_Windowed", Tag = WindowMode.Windowed },
            new TaggedItem<WindowMode>() { LocaleKey = "PSO2OptionsWindow_ExclusiveFullscreen", Tag = WindowMode.ExclusiveFullscreen },
            new TaggedItem<WindowMode>() { LocaleKey = "PSO2OptionsWindow_BorderlessFullscreen", Tag = WindowMode.BorderlessFullscreen }
        };
        public TaggedItem<WindowMode> WindowModeSelected { get; set; }

        public ObservableCollection<TaggedItem<ShaderQuality>> ShaderQualityItems { get; set; } = new ObservableCollection<TaggedItem<ShaderQuality>>()
        {
            new TaggedItem<ShaderQuality>() { LocaleKey = "PSO2OptionsWindow_QualityLow", Tag = ShaderQuality.Low },
            new TaggedItem<ShaderQuality>() { LocaleKey = "PSO2OptionsWindow_QualityStandard", Tag = ShaderQuality.Standard },
            new TaggedItem<ShaderQuality>() { LocaleKey = "PSO2OptionsWindow_QualityHigh", Tag = ShaderQuality.High }
        };
        public TaggedItem<ShaderQuality> ShaderQualitySelected { get; set; }

        public ObservableCollection<TaggedItem<TextureQuality>> TextureQualityItems { get; set; } = new ObservableCollection<TaggedItem<TextureQuality>>()
        {
            new TaggedItem<TextureQuality>() { LocaleKey = "PSO2OptionsWindow_QualityLow", Tag = TextureQuality.Low },
            new TaggedItem<TextureQuality>() { LocaleKey = "PSO2OptionsWindow_QualityStandard", Tag = TextureQuality.Standard },
            new TaggedItem<TextureQuality>() { LocaleKey = "PSO2OptionsWindow_QualityHigh", Tag = TextureQuality.High }
        };
        public TaggedItem<TextureQuality> TextureQualitySelected { get; set; }

        public ObservableCollection<TaggedItem<int>> FrameLimitItems { get; set; } = new ObservableCollection<TaggedItem<int>>()
        {
            new TaggedItem<int>() { LocaleKey = "PSO2OptionsWindow_FPSLimit30", Tag = 30 },
            new TaggedItem<int>() { LocaleKey = "PSO2OptionsWindow_FPSLimit60", Tag = 60 },
            new TaggedItem<int>() { LocaleKey = "PSO2OptionsWindow_FPSLimit120", Tag = 120 },
            new TaggedItem<int>() { LocaleKey = "PSO2OptionsWindow_FPSLimit240", Tag = 240 }
        };
        public TaggedItem<int> FrameLimitSelected { get; set; }

        public ObservableCollection<TaggedItem<InterfaceScale>> InterfaceScaleItems { get; set; } = new ObservableCollection<TaggedItem<InterfaceScale>>()
        {
            new TaggedItem<InterfaceScale>() { LocaleKey = "PSO2OptionsWindow_UIScale1x", Tag = InterfaceScale.Scale1x },
            new TaggedItem<InterfaceScale>() { LocaleKey = "PSO2OptionsWindow_UIScale1Point25x", Tag = InterfaceScale.Scale1_25x },
            new TaggedItem<InterfaceScale>() { LocaleKey = "PSO2OptionsWindow_UIScale1Point5x", Tag = InterfaceScale.Scale1_5x }
        };
        public TaggedItem<InterfaceScale> InterfaceScaleSelected { get; set; }

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

            // window mode
            switch (WindowModeSelected?.Tag)
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

            // shader quality
            if (ShaderQualitySelected != null)
                _Table.SetValueEx(new[] { "Config", "Draw", "ShaderLevel" }, (int)ShaderQualitySelected.Tag);

            // texture quality
            if (TextureQualitySelected != null)
                _Table.SetValueEx(new[] { "Config", "Draw", "TextureResolution" }, (int)TextureQualitySelected.Tag);

            // frame limit
            if (FrameLimitSelected != null)
                _Table.SetValueEx(new[] { "FrameKeep" }, FrameLimitSelected.Tag);

            // interface scale
            if (InterfaceScaleSelected != null)
                _Table.SetValueEx(new[] { "Config", "Screen", "InterfaceSize" }, (int)InterfaceScaleSelected.Tag);

            _RefreshFromTable(_Table);
            await _SaveOptionsFileAsync(_Table);

            CloseAction();

            _ActivityCount -= 1;
        }

        private void _RefreshFromTable(LuaTable table)
        {
            // window mode
            if (table["Windows", "FullScreen"] as bool? ?? false)
                WindowModeSelected = WindowModeItems.FirstOrDefault(x => x.Tag == WindowMode.ExclusiveFullscreen);
            else if (table["Windows", "VirtualFullScreen"] as bool? ?? false)
                WindowModeSelected = WindowModeItems.FirstOrDefault(x => x.Tag == WindowMode.BorderlessFullscreen);
            else
                WindowModeSelected = WindowModeItems.FirstOrDefault(x => x.Tag == WindowMode.Windowed);

            // shader quality
            if (table["Config", "Draw", "ShaderLevel"] is int shaderLevel)
                ShaderQualitySelected = ShaderQualityItems.FirstOrDefault(x => (int)x.Tag == shaderLevel);

            // texture quality
            if (table["Config", "Draw", "TextureResolution"] is int textureLevel)
                TextureQualitySelected = TextureQualityItems.FirstOrDefault(x => (int)x.Tag == textureLevel);

            // frame limit
            if (table["FrameKeep"] is int frameLimit)
                FrameLimitSelected = FrameLimitItems.FirstOrDefault(x => x.Tag == frameLimit);

            // interface scale
            if (table["Config", "Screen", "InterfaceSize"] is int interfaceScale)
                InterfaceScaleSelected = InterfaceScaleItems.FirstOrDefault(x => (int)x.Tag == interfaceScale);
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
