using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace PSRT.Astra
{
    static class HardLink
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern bool CreateHardLink(
            string fileName,
            string existingFileName,
            IntPtr securityAttributes);

        public static bool CreateHardLink(
            string fileName,
            string existingFileName) => CreateHardLink(fileName, existingFileName, IntPtr.Zero);
    }
}
