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
                    return "InstallSelectorWindow_PathEmptyHint";

                if (SelectedPathValid == false)
                    return "InstallSelectorWindow_InvalidDirectoryHint";

                if (SelectedPathContainsPSO2Bin == false)
                    return "InstallSelectorWindow_NoInstallFoundHint";

                return "InstallSelectorWindow_InstallFoundHint";
            }
        }

        public string ConfirmationMessage
        {
            get
            {
                if (SelectedPathValid && SelectedPathContainsPSO2Bin)
                    return "InstallSelectorWindow_UpdatePSO2";

                return "InstallSelectorWindow_InstallPSO2";
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
