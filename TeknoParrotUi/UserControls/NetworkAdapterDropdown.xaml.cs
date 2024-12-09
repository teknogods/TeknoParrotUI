using System.Net.NetworkInformation;
using System.Windows.Controls;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Sockets;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.UserControls
{
    /// <summary>
    /// Interaction logic for NetworkAdapterDropdown.xaml
    /// </summary>
    public partial class NetworkAdapterDropdown : UserControl
    {
        public ObservableCollection<NetworkAdapterItem> foundAdapters { get; set; } = new ObservableCollection<NetworkAdapterItem>();

        public class NetworkAdapterItem
        {
            public string AdapterName { get; set; }
            public string IpAddress { get; set; }
            public string DisplayName { get; set; }
        }

        public NetworkAdapterDropdown()
        {
            InitializeComponent();
            DataContext = this;
            PopulateItemsSource();
            try
            {
                comboBox.SelectedIndex = GetSavedAdapterIndex();
            } catch
            {
                // do nothing, this is mostly so that this ui element loads in the xaml preview in vs :)
            }
            
        }

        private void PopulateItemsSource()
        {
            var networkAdapters = NetworkInterface.GetAllNetworkInterfaces();
            var items = new ObservableCollection<NetworkAdapterItem>();

            foreach (var adapter in networkAdapters)
            {
                var ipAddress = "";
                var unicastAddresses = adapter.GetIPProperties().UnicastAddresses;
                foreach (var uniCastAddress in unicastAddresses)
                {
                    // only accept IPV4
                    if (uniCastAddress.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    ipAddress = uniCastAddress.Address.ToString();
                    break;

                }

                var adapterName = adapter.Name;
                var displayName = $"{adapterName} ({ipAddress})";
                items.Add(new NetworkAdapterItem { AdapterName = adapterName, IpAddress = ipAddress, DisplayName = displayName });
            }

            foundAdapters = items;
        }

        public string SelectedAdapterName => comboBox.SelectedValue as string;

        private int GetSavedAdapterIndex()
        {
            if (Lazydata.ParrotData.Elfldr2NetworkAdapterName != "")
            {
                NetworkAdapterItem foundAdapter = foundAdapters.FirstOrDefault(adapter => adapter.AdapterName == Lazydata.ParrotData.Elfldr2NetworkAdapterName);
                if (foundAdapter == null) return 0;
                return foundAdapters.IndexOf(foundAdapter);
            }

            return 0;
        }

    }
}
