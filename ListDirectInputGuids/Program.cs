using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpDX.DirectInput;

namespace ListDirectInputGuids
{
    class Program
    {
        private static readonly DirectInput DiInput = new DirectInput();
        static void Main(string[] args)
        {
            // TODO: OBSOLETE, NOT REALLY NEEDED AT ALL ANYMORE SINCE DINPUT WORKS FINE!
            var devices = DiInput.GetDevices().Where(x => x.Type != DeviceType.Mouse).ToList();
            Console.WriteLine("TeknoParrot GUID helper, use this if your joystick controls don't work.");
            Console.WriteLine("Create a new file called DirectInputOverride.txt");
            Console.WriteLine("Put one GUID per line to use the override, simply file content like this:");
            Console.WriteLine("87654321-1234-1234-4312-112233445566");
            Console.WriteLine("12345678-4321-4321-1234-112233445566");
            Console.WriteLine("Found DirectInput devices:");
            Console.WriteLine("----------------------------------------");
            foreach (var deviceInstance in devices)
            {
                Console.WriteLine("Product Name: " + deviceInstance.ProductName + " " + deviceInstance.InstanceName);
                Console.WriteLine("GUID: " + deviceInstance.InstanceGuid);
                Console.WriteLine("----------------------------------------");
            }

            Console.WriteLine("Press any key to continue");
            Console.ReadKey();
        }
    }
}
