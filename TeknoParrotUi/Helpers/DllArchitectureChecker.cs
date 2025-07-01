using System;
using System.IO;
using System.Runtime.InteropServices;

namespace TeknoParrotUi.Helpers
{
    class DllArchitectureChecker
    {
        private const ushort IMAGE_FILE_MACHINE_I386 = 0x014c;  // 32-bit
        private const ushort IMAGE_FILE_MACHINE_AMD64 = 0x8664; // 64-bit

        public static bool IsDll64Bit(string dllPath, out bool is64Bit)
        {
            is64Bit = false;

            if (!File.Exists(dllPath))
            {
                Console.WriteLine("File not found.");
                return false;
            }

            try
            {
                using (var stream = new FileStream(dllPath, FileMode.Open, FileAccess.Read))
                using (var reader = new BinaryReader(stream))
                {
                    // Read the DOS header
                    stream.Seek(0x3C, SeekOrigin.Begin); // Offset of e_lfanew in IMAGE_DOS_HEADER
                    int peHeaderOffset = reader.ReadInt32();

                    // Read the PE header
                    stream.Seek(peHeaderOffset, SeekOrigin.Begin);
                    uint peSignature = reader.ReadUInt32();
                    
                    if (peSignature != 0x00004550) // "PE\0\0" signature
                    {
                        Console.WriteLine("Invalid PE signature.");
                        return false;
                    }

                    // Read the Machine field in the File Header
                    ushort machine = reader.ReadUInt16();
                    
                    switch (machine)
                    {
                        case IMAGE_FILE_MACHINE_AMD64:
                            is64Bit = true;
                            return true;
                        case IMAGE_FILE_MACHINE_I386:
                            is64Bit = false;
                            return true;
                        default:
                            Console.WriteLine($"Unknown architecture: 0x{machine:X4}");
                            return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading DLL: {ex.Message}");
                return false;
            }
        }
    }
}