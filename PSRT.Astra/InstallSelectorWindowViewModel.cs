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
                    return "Please select your PSO2 installation directory.";

                if (SelectedPathValid == false)
                    return "Path is not a valid PSO2 installation directory";

                if (SelectedPathContainsPSO2Bin == false)
                    return "Couldn't find a PSO2 installation here. Astra will install PSO2 to this folder.";

                return "Found a PSO2 installation! Astra will update and launch the game from here.";
            }
        }

        public string ConfirmationMessage
        {
            get
            {
                if (SelectedPathValid && SelectedPathContainsPSO2Bin)
                    return "Update PSO2";

                return "Install PSO2";
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
