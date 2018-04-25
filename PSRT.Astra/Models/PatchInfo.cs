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
    public static class PatchInfo
    {
        public static async Task<Dictionary<string, (string Hash, Uri DownloadPath)>> FetchPatchInfosAsync(InstallConfiguration installConfiguration, DownloadConfiguration downloadConfiguration, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            var infos = new Dictionary<string, (string Hash, Uri DownloadPath)>();

            // patchlist
            using (var client = new AquaHttpClient())
            using (var response = await client.GetAsync(downloadConfiguration.PatchesPatchList, ct))
            using (var stream = await response.Content.ReadAsStreamAsync())
            using (var reader = new StreamReader(stream))
            {
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
                            throw new Exception("Patch list line contained unknown root type \"{type}\"");
                    }

                    infos[name] = (hash, new Uri(root, name));
                }
            }

            // launcherlist
            using (var client = new AquaHttpClient())
            using (var response = await client.GetAsync(downloadConfiguration.PatchesLauncherList, ct))
            using (var stream = await response.Content.ReadAsStreamAsync())
            using (var reader = new StreamReader(stream))
            {
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

                    infos[name] = (hash, new Uri(downloadConfiguration.RootPatches, name));
                }
            }

            return infos;
        }
    }
}
