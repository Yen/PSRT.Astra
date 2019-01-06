using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PSRT.Astra.Models.ArksLayer
{
    public static class TweakerBin
    {
        public static string GenerateFileContents(string pso2BinDirectory)
        {
            var key = "kW7eheKa7RMFXkbW7V5U";
            var hour = DateTime.Now.Hour.ToString(CultureInfo.InvariantCulture);
            var sanitizedDirectoryPath = Regex.Replace(
                pso2BinDirectory.Replace("://", ":/").Replace(@":\\", @":\"),
                @"(\\|\/)$",
                string.Empty);
            var directoryPathLength = sanitizedDirectoryPath.Length.ToString(CultureInfo.InvariantCulture);

            var combinedSeed = key + hour + directoryPathLength;
            using (var md5 = MD5.Create())
            {
                var hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(combinedSeed));
                var hexedStrings = hashBytes.Select(b => b.ToString("x2", CultureInfo.InvariantCulture));
                return string.Concat(hexedStrings);
            }
        }
    }
}
