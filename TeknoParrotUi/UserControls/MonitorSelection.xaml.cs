using System.Windows.Controls;
using System.Collections.ObjectModel;
using TeknoParrotUi.Common;
using System.Linq;
using System.Windows;
using System.Runtime.InteropServices;
using System.Text;

namespace TeknoParrotUi.UserControls
{
    public partial class MonitorSelection : System.Windows.Controls.UserControl
    {
        public ObservableCollection<MonitorItem> FoundMonitors { get; set; } = new ObservableCollection<MonitorItem>();

        public class MonitorItem
        {
            public int MonitorIndex { get; set; }
            public string MonitorName { get; set; }
            public string Resolution { get; set; }
            public string DisplayName { get; set; }
        }

        #region Windows API Declarations
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct DISPLAY_DEVICE
        {
            public int cb;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceString;
            public int StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceKey;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
            public int dmICMMethod;
            public int dmICMIntent;
            public int dmMediaType;
            public int dmDitherType;
            public int dmReserved1;
            public int dmReserved2;
            public int dmPanningWidth;
            public int dmPanningHeight;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool EnumDisplayDevices(string lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool EnumDisplaySettings(string lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);

        private const int ENUM_CURRENT_SETTINGS = -1;
        private const int DISPLAY_DEVICE_ATTACHED_TO_DESKTOP = 0x1;
        private const int DISPLAY_DEVICE_PRIMARY_DEVICE = 0x4;
        #endregion

        public static readonly DependencyProperty SelectedMonitorIndexProperty =
            DependencyProperty.Register("SelectedMonitorIndex", typeof(string), typeof(MonitorSelection),
                new FrameworkPropertyMetadata("0", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedMonitorIndexChanged));

        public static readonly DependencyProperty UseUnitySortingProperty =
            DependencyProperty.Register("UseUnitySorting", typeof(bool), typeof(MonitorSelection),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.None, OnUseUnitySortingChanged));

        public string SelectedMonitorIndex
        {
            get { return (string)GetValue(SelectedMonitorIndexProperty); }
            set { SetValue(SelectedMonitorIndexProperty, value); }
        }

        public bool UseUnitySorting
        {
            get { return (bool)GetValue(UseUnitySortingProperty); }
            set { SetValue(UseUnitySortingProperty, value); }
        }

        private static void OnSelectedMonitorIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as MonitorSelection;
            if (control != null && int.TryParse(e.NewValue as string, out int index))
            {
                if (index >= 0 && index < control.FoundMonitors.Count)
                {
                    control.comboBox.SelectedIndex = index;
                }
            }
        }

        private static void OnUseUnitySortingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as MonitorSelection;
            if (control != null)
            {
                control.PopulateItemsSource();
            }
        }

        public MonitorSelection()
        {
            InitializeComponent();
            DataContext = this;
            PopulateItemsSource();
            comboBox.SelectionChanged += ComboBox_SelectionChanged;
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (comboBox.SelectedValue != null)
            {
                SelectedMonitorIndex = comboBox.SelectedValue.ToString();
            }
        }

        private void PopulateItemsSource()
        {
            var allDisplays = new System.Collections.Generic.List<(DISPLAY_DEVICE device, DEVMODE mode)>();
            uint deviceIndex = 0;

            // First, enumerate all attached displays
            while (true)
            {
                DISPLAY_DEVICE displayDevice = new DISPLAY_DEVICE();
                displayDevice.cb = Marshal.SizeOf(displayDevice);

                if (!EnumDisplayDevices(null, deviceIndex, ref displayDevice, 0))
                    break;

                // Only include attached displays
                if ((displayDevice.StateFlags & DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) != 0)
                {
                    DEVMODE devMode = new DEVMODE();
                    devMode.dmSize = (short)Marshal.SizeOf(devMode);

                    if (EnumDisplaySettings(displayDevice.DeviceName, ENUM_CURRENT_SETTINGS, ref devMode))
                    {
                        allDisplays.Add((displayDevice, devMode));
                    }
                }

                deviceIndex++;
            }

            System.Collections.Generic.List<(DISPLAY_DEVICE device, DEVMODE mode)> sortedDisplays;

            if (UseUnitySorting)
            {
                // Unity sorting: Sort by X,Y coordinates (left-to-right, top-to-bottom)
                // Then move primary to index 0 and re-sort the rest
                var coordSorted = allDisplays
                    .OrderBy(d => d.mode.dmPositionX)
                    .ThenBy(d => d.mode.dmPositionY)
                    .ToList();

                // Find primary monitor
                int primaryIndex = coordSorted.FindIndex(d => (d.device.StateFlags & DISPLAY_DEVICE_PRIMARY_DEVICE) != 0);

                if (primaryIndex > 0 && coordSorted.Count > 1)
                {
                    // Move primary to front
                    var primary = coordSorted[primaryIndex];
                    coordSorted.RemoveAt(primaryIndex);
                    coordSorted.Insert(0, primary);

                    // Re-sort remaining monitors (excluding primary at index 0) by coordinates
                    if (coordSorted.Count > 2)
                    {
                        var remaining = coordSorted.Skip(1)
                            .OrderBy(d => d.mode.dmPositionX)
                            .ThenBy(d => d.mode.dmPositionY)
                            .ToList();
                        coordSorted = new System.Collections.Generic.List<(DISPLAY_DEVICE device, DEVMODE mode)> { coordSorted[0] };
                        coordSorted.AddRange(remaining);
                    }
                }

                sortedDisplays = coordSorted;
            }
            else
            {
                // Default sorting: Primary first, then by device name
                sortedDisplays = allDisplays
                    .OrderByDescending(d => (d.device.StateFlags & DISPLAY_DEVICE_PRIMARY_DEVICE) != 0)
                    .ThenBy(d => d.device.DeviceName)
                    .ToList();
            }

            var items = new ObservableCollection<MonitorItem>();
            for (int i = 0; i < sortedDisplays.Count; i++)
            {
                var display = sortedDisplays[i];
                bool isPrimary = (display.device.StateFlags & DISPLAY_DEVICE_PRIMARY_DEVICE) != 0;
                string resolution = $"{display.mode.dmPelsWidth}x{display.mode.dmPelsHeight}";
                string position = UseUnitySorting ? $" @({display.mode.dmPositionX},{display.mode.dmPositionY})" : "";
                string primaryTag = isPrimary ? " (Primary)" : "";
                string displayName = $"Monitor {i}: {display.device.DeviceName} {resolution}{position}{primaryTag}";

                items.Add(new MonitorItem
                {
                    MonitorIndex = i,
                    MonitorName = display.device.DeviceName,
                    Resolution = resolution,
                    DisplayName = displayName
                });
            }

            FoundMonitors = items;
            
            // Set initial selection
            if (int.TryParse(SelectedMonitorIndex, out int initialIndex) && initialIndex >= 0 && initialIndex < FoundMonitors.Count)
            {
                comboBox.SelectedIndex = initialIndex;
            }
            else
            {
                comboBox.SelectedIndex = 0;
            }
        }
    }
}
