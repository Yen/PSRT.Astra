using PropertyChanged;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace PSRT.Astra
{
    [AddINotifyPropertyChangedInterface]
    public class InstallSelectorWindowViewModel
    {
        public string SelectedPath { get; set; }

        public bool SelectedPathValid => string.IsNullOrEmpty(SelectedPath) ? false : Directory.Exists(SelectedPath);
        public bool SelectedPathContainsPSO2Bin => string.IsNullOrEmpty(SelectedPath) ? false : Directory.Exists(Path.Combine(SelectedPath, "pso2_bin"));

        public string SelectionMessage
        {
            get
            {
                if (string.IsNullOrEmpty(SelectedPath))
                    return "Please select a path with the button above or enter it into the field";

                if (SelectedPathValid == false)
                    return "Path is not a valid PSO2 installation directory";

                if (SelectedPathContainsPSO2Bin == false)
                    return "Path does not contain an existing PSO2 installation, PSO2Tweaker will create a new installation in this directory";

                return "PSO2 installation detected";
            }
        }

        public Brush SelectedPathColor => string.IsNullOrEmpty(SelectedPath)
            ? null
            : SelectedPathValid
                ? SelectedPathContainsPSO2Bin
                    ? new SolidColorBrush(Color.FromRgb(191, 255, 191))
                    : new SolidColorBrush(Color.FromRgb(191, 191, 255))
                : new SolidColorBrush(Color.FromRgb(255, 191, 191));
    }
}
