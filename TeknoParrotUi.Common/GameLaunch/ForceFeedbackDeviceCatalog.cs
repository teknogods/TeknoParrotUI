using System;
using System.Collections.Generic;
using System.Linq;

namespace TeknoParrotUi.Common.GameLaunch
{
    public static class ForceFeedbackDeviceCatalog
    {
        public static List<DynamicDropdownOption> GetOptions(GameProfile profile, string selectedValue)
        {
            var devices = profile.EmulatorType switch
            {
                EmulatorType.TeknoModel1 => Model1FfbDeviceProbe.GetDevices(),
                EmulatorType.TeknoModel2 => Model2FfbDeviceProbe.GetDevices(),
                EmulatorType.TeknoCobra => CobraFfbDeviceProbe.GetDevices(),
                EmulatorType.TeknoHNG64 => Hng64FfbDeviceProbe.GetDevices(),
                EmulatorType.TeknoHornet => HornetFfbDeviceProbe.GetDevices(),
                EmulatorType.TeknoZeus => ZeusFfbDeviceProbe.GetDevices(),
                EmulatorType.TeknoViper => ViperFfbDeviceProbe.GetDevices(),
                EmulatorType.TeknoVegas => VegasFfbDeviceProbe.GetDevices(),
                EmulatorType.TeknoVUnit => VUnitFfbDeviceProbe.GetDevices(),
                EmulatorType.TeknoGClub => GClubFfbDeviceProbe.GetDevices(),
                _ => new List<DynamicDropdownOption>
                {
                    new DynamicDropdownOption { DisplayName = "Force feedback off", Value = "off" }
                }
            };

            // Keep an existing selection visible when its device is unplugged.
            if (!string.IsNullOrWhiteSpace(selectedValue) &&
                !devices.Any(device => string.Equals(device.Value, selectedValue, StringComparison.Ordinal)))
            {
                devices.Add(new DynamicDropdownOption
                {
                    DisplayName = $"Previously selected device (unavailable) - {selectedValue}",
                    Value = selectedValue
                });
            }
            return devices;
        }
    }
}
