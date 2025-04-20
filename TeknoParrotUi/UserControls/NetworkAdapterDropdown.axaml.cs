using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.UserControls
{
    public class NetworkAdapterItem
    {
        public string AdapterName { get; set; }
        public string IpAddress { get; set; }
        public string DisplayName { get; set; }
        public override string ToString()
        {
            return DisplayName;
        }
    }

    /// <summary>
    /// Interaction logic for NetworkAdapterDropdown.axaml
    /// </summary>
    public partial class NetworkAdapterDropdown : UserControl, INotifyPropertyChanged
    {
        private ObservableCollection<NetworkAdapterItem> _foundAdapters;

        public ObservableCollection<NetworkAdapterItem> foundAdapters
        {
            get => _foundAdapters;
            set
            {
                if (_foundAdapters != value)
                {
                    _foundAdapters = value;
                    OnPropertyChanged();
                }
            }
        }

        private NetworkAdapterItem _selectedAdapter;
        public NetworkAdapterItem SelectedAdapter
        {
            get => _selectedAdapter;
            set
            {
                if (_selectedAdapter != value)
                {
                    _selectedAdapter = value;
                    OnPropertyChanged();
                    // You can raise an event or execute code when selection changes
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public NetworkAdapterDropdown()
        {
            InitializeComponent();
            DataContext = this;
            foundAdapters = new ObservableCollection<NetworkAdapterItem>();
            PopulateItemsSource();
            try
            {
                comboBox.SelectedIndex = GetSavedAdapterIndex();
            }
            catch
            {
                // do nothing, this is mostly so that this ui element loads in the xaml preview in vs :)
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            // Get control references
            comboBox = this.FindControl<ComboBox>("comboBox");
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

                if (ipAddress != string.Empty)
                {
                    var adapterName = adapter.Name;
                    var displayName = $"{adapterName} ({ipAddress})";
                    items.Add(new NetworkAdapterItem { AdapterName = adapterName, IpAddress = ipAddress, DisplayName = displayName });
                }
            }

            foundAdapters = items;
        }

        public string SelectedAdapterName => comboBox.SelectedValue as string;

        private int GetSavedAdapterIndex()
        {
            if (!string.IsNullOrEmpty(Lazydata.ParrotData.Elfldr2NetworkAdapterName))
            {
                NetworkAdapterItem foundAdapter = foundAdapters.FirstOrDefault(adapter =>
                    adapter.AdapterName == Lazydata.ParrotData.Elfldr2NetworkAdapterName);

                if (foundAdapter != null)
                {
                    SelectedAdapter = foundAdapter; // Set the selected adapter
                    return foundAdapters.IndexOf(foundAdapter);
                }
            }

            return 0;
        }
    }
}