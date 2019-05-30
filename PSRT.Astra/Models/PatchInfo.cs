using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PSRT.Astra.Models
{
    public struct PatchInfo
    {
        public string Name;
        public string Hash;
        public Uri DownloadPath;

        public static async Task<List<PatchInfo>> FetchPatchInfosAsync(InstallConfiguration installConfiguration, DownloadConfiguration downloadConfiguration, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            var infos = new List<PatchInfo>();

            // patchlist
            App.Logger.Info(nameof(PatchInfo), "Downloading patch list");
            using (var client = new AquaHttpClient())
            using (var response = await client.GetAsync(downloadConfiguration.PatchesPatchList, ct))
            using (var reader = new StringReader(await response.Content.ReadAsStringAsync()))
            {
                App.Logger.Info(nameof(PatchInfo), "Parsing patch list");

                while (true)
                {
                    ct.ThrowIfCancellationRequested();

                    var line = await reader.ReadLineAsync();
                    if (line == null)
                        break;

                    var parts = line.Split();
                    if (parts.Length < 4)
                        throw new Exception("Patch list line contained less than four parts");

                    var name = parts[0];
                    var hash = parts[1];
                    var type = parts[3];

                    if (Path.GetFileNameWithoutExtension(name) == Path.GetFileName(installConfiguration.CensorFile))
                        continue;

                    Uri root;
                    switch (type)
                    {
                        case "p":
                            root = downloadConfiguration.RootPatches;
                            break;
                        case "m":
                            root = downloadConfiguration.RootMaster;
                            break;
                        default:
                            throw new Exception($"Patch list line contained unknown root type \"{type}\"");
                    }

                    infos.Add(new PatchInfo
                    {
                        Name = name,
                        Hash = hash,
                        DownloadPath = new Uri(root, name)
                    });
                }
            }

            // launcherlist
            App.Logger.Info(nameof(PatchInfo), "Downloading launcher list");
            using (var client = new AquaHttpClient())
            using (var response = await client.GetAsync(downloadConfiguration.PatchesLauncherList, ct))
            using (var reader = new StringReader(await response.Content.ReadAsStringAsync()))
            {
                App.Logger.Info(nameof(PatchInfo), "Parsing launcher list");

                while (true)
                {
                    ct.ThrowIfCancellationRequested();

                    var line = await reader.ReadLineAsync();
                    if (line == null)
                        break;

                    var parts = line.Split();
                    if (parts.Length < 3)
                        throw new Exception("Launcher list line contained less than three parts");

                    var name = parts[0];
                    // parts[1] is file size
                    var hash = parts[2];

                    infos.Add(new PatchInfo
                    {
                        Name = name,
                        Hash = hash,
                        DownloadPath = new Uri(downloadConfiguration.RootPatches, name)
                    });
                }
            }

            return infos;
        }
    }
}
