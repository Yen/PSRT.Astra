using SharpCompress.Archives.Rar;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PSRT.Astra.Models.ArksLayer.Phases
{
    public class EnglishPatchPhase
    {
        private InstallConfiguration _InstallConfiguration;
        private PatchCache _PatchCache;
        private PluginInfo _PluginInfo;
        private bool _Enabled;

        public EnglishPatchPhase(InstallConfiguration installConfiguration, PatchCache patchCache, PluginInfo pluginInfo, bool enabled)
        {
            _InstallConfiguration = installConfiguration;
            _PatchCache = patchCache;
            _PluginInfo = pluginInfo;
            _Enabled = enabled;
        }

        public async Task RunAsync(CancellationToken ct = default)
        {
            if (_Enabled)
                await _InstallAsync(ct);
            else
                await _RemoveAsync(ct);
        }

        private async Task _InstallAsync(CancellationToken ct = default)
        {
            App.Current.Logger.Info(nameof(EnglishPatchPhase), "Downloading english translation information");
            var translation = await TranslationInfo.FetchEnglishAsync(ct);

            App.Current.Logger.Info(nameof(EnglishPatchPhase), "Getting data from patch cache");
            var cacheData = await _PatchCache.SelectAllAsync();

            string CreateRelativePath(string path)
            {
                var root = new Uri(_InstallConfiguration.PSO2BinDirectory);
                var relative = root.MakeRelativeUri(new Uri(path));
                return relative.OriginalString;
            }

            bool Verify(string path, string hash)
            {
                var relative = CreateRelativePath(path);

                var info = new FileInfo(path);
                if (info.Exists == false)
                    return false;

                if (cacheData.ContainsKey(relative) == false)
                    return false;

                var data = cacheData[relative];

                if (data.Hash != hash)
                    return false;

                if (data.LastWriteTime != info.LastWriteTimeUtc.ToFileTimeUtc())
                    return false;

                return true;
            }

            using (var client = new ArksLayerHttpClient())
            {
                async Task VerifyAndDownlodRar(string path, string downloadHash, Uri downloadPath)
                {
                    if (Verify(path, downloadHash) == false)
                    {
                        App.Current.Logger.Info(nameof(EnglishPatchPhase), $"Downloading \"{Path.GetFileName(downloadPath.LocalPath)}\"");
                        using (var response = await client.GetAsync(downloadPath))
                        using (var stream = await response.Content.ReadAsStreamAsync())
                        using (var archive = RarArchive.Open(stream))
                        {
                            if (archive.Entries.Count > 0)
                            {
                                using (var fs = File.Create(path, 4096, FileOptions.Asynchronous))
                                    await archive.Entries.First().OpenEntryStream().CopyToAsync(fs);

                                await _PatchCache.InsertUnderTransactionAsync(new[]
                                {
                                    new PatchCacheEntry()
                                    {
                                        Name = CreateRelativePath(path),
                                        Hash = downloadHash,
                                        LastWriteTime = new FileInfo(path).LastWriteTimeUtc.ToFileTimeUtc()
                                    }
                                });
                            }
                        }
                    }
                }

                await VerifyAndDownlodRar(_InstallConfiguration.ArksLayer.EnglishBlockPatch, translation.BlockMD5, new Uri(translation.BlockPatch));
                await VerifyAndDownlodRar(_InstallConfiguration.ArksLayer.EnglishItemPatch, translation.ItemMD5, new Uri(translation.ItemPatch));
                await VerifyAndDownlodRar(_InstallConfiguration.ArksLayer.EnglishTextPatch, translation.TextMD5, new Uri(translation.TextPatch));
                await VerifyAndDownlodRar(_InstallConfiguration.ArksLayer.EnglishTitlePatch, translation.TitleMD5, new Uri(translation.TitlePatch));

                if (Verify(_InstallConfiguration.ArksLayer.EnglishRaiserPatch, translation.RaiserMD5) == false)
                {
                    App.Current.Logger.Info(nameof(EnglishPatchPhase), $"Downloading \"{Path.GetFileName(new Uri(translation.RaiserPatch).LocalPath)}\"");
                    using (var stream = await client.GetStreamAsync(translation.RaiserPatch))
                    using (var fs = File.Create(_InstallConfiguration.ArksLayer.EnglishRaiserPatch, 4096, FileOptions.Asynchronous))
                        await stream.CopyToAsync(fs);

                    await _PatchCache.InsertUnderTransactionAsync(new[]
                    {
                        new PatchCacheEntry()
                        {
                            Name = CreateRelativePath(_InstallConfiguration.ArksLayer.EnglishRaiserPatch),
                            Hash = translation.RaiserMD5,
                            LastWriteTime = new FileInfo(_InstallConfiguration.ArksLayer.EnglishRaiserPatch).LastWriteTimeUtc.ToFileTimeUtc()
                        }
                    });
                }
            }

            App.Current.Logger.Info(nameof(EnglishPatchPhase), "Validating plugin dlls");

            await _PluginInfo.PSO2BlockRenameDll.ValidateFileAsync(_InstallConfiguration.ArksLayer.PluginPSO2BlockRenameDll, ct);
            await _PluginInfo.PSO2ItemTranslatorDll.ValidateFileAsync(_InstallConfiguration.ArksLayer.PluginPSO2ItemTranslatorDll, ct);
            await _PluginInfo.PSO2TitleTranslatorDll.ValidateFileAsync(_InstallConfiguration.ArksLayer.PluginPSO2TitleTranslatorDll, ct);
            await _PluginInfo.PSO2RAISERSystemDll.ValidateFileAsync(_InstallConfiguration.ArksLayer.PluginPSO2RAISERSystemDll, ct);
        }

        private async Task _RemoveAsync(CancellationToken ct)
        {
            await Task.Run(() =>
            {
                App.Current.Logger.Info(nameof(EnglishPatchPhase), "Deleting plugin dlls");

                File.Delete(_InstallConfiguration.ArksLayer.PluginPSO2BlockRenameDll);
                File.Delete(_InstallConfiguration.ArksLayer.PluginPSO2ItemTranslatorDll);
                File.Delete(_InstallConfiguration.ArksLayer.PluginPSO2TitleTranslatorDll);
                File.Delete(_InstallConfiguration.ArksLayer.PluginPSO2RAISERSystemDll);
            });
        }
    }
}
