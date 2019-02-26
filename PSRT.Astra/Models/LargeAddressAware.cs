using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace PSRT.Astra.Models
{
    public static class LargeAddressAware
    {
        private class Config
        {
            [JsonProperty(PropertyName = "originalHash", Required = Required.Always)]
            public string OriginalHash { get; set; }

            [JsonProperty(PropertyName = "patchedHash", Required = Required.Always)]
            public string PatchedHash { get; set; }
        }

        public static bool IsLargeAddressAwarePactchApplied(InstallConfiguration installConfiguration, string serverHash)
        {
            var config = _TryReadConfig(installConfiguration);
            if (config == null)
                return false;

            if (serverHash != null && config.OriginalHash != serverHash)
                return false;

            try
            {
                return _GetPSO2Hash(installConfiguration) == config.PatchedHash;
            }
            catch
            {
                return false;
            }
        }

        private static Config _TryReadConfig(InstallConfiguration installConfiguration)
        {
            try
            {
                var file = File.ReadAllText(installConfiguration.LargeAddressAwareConfig);
                return JsonConvert.DeserializeObject<Config>(file);
            }
            catch (Exception ex)
            {
                App.Logger.Warning(nameof(LargeAddressAware), "Failed to read config", ex);
                return null;
            }
        }

        public static void ApplyLargeAddressAwarePatch(InstallConfiguration installConfiguration)
        {
            if (IsLargeAddressAwarePactchApplied(installConfiguration, null))
                return;

            var originalHash = _GetPSO2Hash(installConfiguration);

            _ApplyLargeAddressAwarePatch(installConfiguration.PSO2Executable);

            var patchedHash = _GetPSO2Hash(installConfiguration);

            var config = new Config
            {
                OriginalHash = originalHash,
                PatchedHash = patchedHash
            };
            var json = JsonConvert.SerializeObject(config);
            File.WriteAllText(installConfiguration.LargeAddressAwareConfig, json);
        }

        private static string _GetPSO2Hash(InstallConfiguration installConfiguration)
        {
            using (var stream = File.OpenRead(installConfiguration.PSO2Executable))
            using (var md5 = MD5.Create())
            {
                var hashBytes = md5.ComputeHash(stream);
                return string.Concat(hashBytes.Select(b => b.ToString("X2")));
            }
        }

        private static void _ApplyLargeAddressAwarePatch(string executablePath)
        {
            App.Logger.Info(nameof(LargeAddressAware), "Creating temporary directory");
            var tempDirectory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"PSRT.Astra-{Guid.NewGuid()}"));
            Directory.CreateDirectory(tempDirectory);

            var editbinExecutablePath = Path.Combine(tempDirectory, "EDITBIN.EXE");
            var linkExecutablePath = Path.Combine(tempDirectory, "LINK.EXE");
            var dllPath = Path.Combine(tempDirectory, "MSPDB60.DLL");

            App.Logger.Info(nameof(LargeAddressAware), "Writing patch files");
            File.WriteAllBytes(editbinExecutablePath, Properties.Resources.LargeAddressAware_EDITBIN);
            File.WriteAllBytes(linkExecutablePath, Properties.Resources.LargeAddressAware_LINK);
            File.WriteAllBytes(dllPath, Properties.Resources.LargeAddressAware_MSPDB60);

            var normalizedName = Path.GetFullPath(executablePath).Replace("/", "\\");

            var process = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = editbinExecutablePath,
                    Arguments = $"/NOLOGO /LARGEADDRESSAWARE \"{normalizedName}\""
                }
            };

            App.Logger.Info(nameof(LargeAddressAware), "Running editbin executable");
            process.Start();
            process.WaitForExit();

            // if this fails. it's not the end of the world, as windows will clear
            // up the temp directory anyway
            App.Logger.Info(nameof(LargeAddressAware), "Deleting temporary directory");
            try
            {
                Directory.Delete(tempDirectory, true);
            }
            catch (Exception ex)
            {
                App.Logger.Warning(nameof(LargeAddressAware), "Failed to delete temporary directory", ex);
            }

            if (process.ExitCode != 0)
                throw new Exception($"Large address patching returned error code: {process.ExitCode}");
        }
    }
}
