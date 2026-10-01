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

        public void SetGpuStatus(Metadata info)
        {
            SetGpuStatus(info.nvidia, info.amd, info.intel, info.nvidia_issues, info.amd_issues, info.intel_issues);
        }

        public void SetGpuStatus(GPUSTATUS nvidia, GPUSTATUS amd, GPUSTATUS intel,
            string nvidiaIssues = null, string amdIssues = null, string intelIssues = null)
        {
            bool untested = nvidia == GPUSTATUS.NO_INFO && amd == GPUSTATUS.NO_INFO && intel == GPUSTATUS.NO_INFO;
            VendorPanel.Visibility = untested ? Visibility.Collapsed : Visibility.Visible;
            UntestedText.Visibility = untested ? Visibility.Visible : Visibility.Collapsed;

            SetIconForStatus(NvidiaPanel, NvidiaIcon, nvidia, nvidiaIssues);
            SetIconForStatus(AmdPanel, AmdIcon, amd, amdIssues);
            SetIconForStatus(IntelPanel, IntelIcon, intel, intelIssues);
        }

        private static string StatusText(GPUSTATUS status)
        {
            switch (status)
            {
                case GPUSTATUS.OK: return "Works";
                case GPUSTATUS.WITH_FIX: return "Works with a fix";
                case GPUSTATUS.HAS_ISSUES: return "Runs with issues";
                case GPUSTATUS.NO: return "Not working";
                default: return "Untested";
            }
        }

        private void SetIconForStatus(FrameworkElement panel, PackIcon icon, GPUSTATUS status, string issues)
        {
            panel.ToolTip = string.IsNullOrWhiteSpace(issues) ? StatusText(status) : $"{StatusText(status)}: {issues.Trim()}";

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
                    icon.Kind = PackIconKind.HelpCircleOutline;
                    icon.Foreground = (SolidColorBrush)new BrushConverter().ConvertFrom("#737373");
                    break;
            }
        }
    }
}