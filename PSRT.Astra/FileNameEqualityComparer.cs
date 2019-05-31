using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSRT.Astra
{
    public class FileNameEqualityComparer : IEqualityComparer<string>
    {
        public bool Equals(string x, string y) => x.Equals(y, StringComparison.InvariantCultureIgnoreCase);
        public int GetHashCode(string obj) => obj.GetHashCode();
    }
}
