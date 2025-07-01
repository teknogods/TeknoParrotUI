using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.UserControls
{
    public partial class GpuCompatibilityDisplay : UserControl
    {
        public GpuCompatibilityDisplay()
        {
            InitializeComponent();
        }

        public void SetGpuStatus(GPUSTATUS nvidia, GPUSTATUS amd, GPUSTATUS intel)
        {
            SetIconForStatus(NvidiaIcon, nvidia);
            SetIconForStatus(AmdIcon, amd);
            SetIconForStatus(IntelIcon, intel);
        }

        private void SetIconForStatus(PackIcon icon, GPUSTATUS status)
        {
            switch (status)
            {
                case GPUSTATUS.OK:
                    icon.Kind = PackIconKind.CheckCircle;
                    icon.Foreground = (SolidColorBrush)new BrushConverter().ConvertFrom("#21bf21");
                    break;
                case GPUSTATUS.WITH_FIX:
                    icon.Kind = PackIconKind.AlertCircle;
                    icon.Foreground = (SolidColorBrush)new BrushConverter().ConvertFrom("#f0a01d");
                    break;
                case GPUSTATUS.HAS_ISSUES:
                    icon.Kind = PackIconKind.AlertCircleCheck;
                    icon.Foreground = (SolidColorBrush)new BrushConverter().ConvertFrom("#f27209");
                    break;
                case GPUSTATUS.NO:
                    icon.Kind = PackIconKind.CloseCircle;
                    icon.Foreground = (SolidColorBrush)new BrushConverter().ConvertFrom("#f10000");
                    break;
                case GPUSTATUS.NO_INFO:
                default:
                    icon.Kind = PackIconKind.HelpCircle;
                    icon.Foreground = (SolidColorBrush)new BrushConverter().ConvertFrom("#737373");
                    break;
            }
        }
    }
}