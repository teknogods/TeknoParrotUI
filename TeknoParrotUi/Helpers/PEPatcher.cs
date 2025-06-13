using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using TeknoParrotUi.Properties;

namespace TeknoParrotUi.Helpers
{
    public class PEPatcher
    {
        [DllImport("imagehlp.dll", SetLastError = true)]
        private static extern uint MapFileAndCheckSum(string filename, out uint headerSum, out uint checkSum);
        private const ushort IMAGE_FILE_LARGE_ADDRESS_AWARE = 0x0020;
        private const ushort IMAGE_FILE_32BIT_MACHINE = 0x0100;

        public static bool IsLargeAddressAware(string filePath)
        {
            try
            {
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    byte[] buffer = new byte[Math.Min(fs.Length, 0x8000)];
                    fs.Read(buffer, 0, buffer.Length);

                    if (buffer.Length < 64)
                        return false;

                    uint peOffset = BitConverter.ToUInt32(buffer, 60);
                    if (peOffset >= buffer.Length - 24)
                        return false;

                    int characteristicsOffset = (int)peOffset + 22;
                    ushort characteristics = BitConverter.ToUInt16(buffer, characteristicsOffset);

                    return (characteristics & IMAGE_FILE_LARGE_ADDRESS_AWARE) != 0;
                }
            }
            catch
            {
                return false;
            }
        }
        
        // Thanks and shout out to https://ntcore.com/4gb-patch/ for the original 4gb patcher <3
        public static bool ApplyLargeAddressAwarePatch(string filePath)
        {
            string backupPath = filePath + ".back";

            // If there's already a .back file, let's assume the backup was done by us and skip backing up again
            if (!File.Exists(backupPath))
            {
                try
                {
                    File.Copy(filePath, backupPath, false);
                }
                catch
                {
                    MessageBox.Show(Resources.PEPatcherBackupError, Resources.PEPatcher4GBPatchTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }
            }

            try
            {
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite))
                {
                    byte[] buffer = new byte[Math.Min(fs.Length, 0x8000)];
                    fs.Read(buffer, 0, buffer.Length);
                    if (buffer.Length < 64)
                    {
                        MessageBox.Show(Resources.PEPatcherInvalidExecutableFormat, Resources.PEPatcher4GBPatchTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return false;
                    }

                    uint peOffset = BitConverter.ToUInt32(buffer, 60);
                    if (peOffset >= buffer.Length - 24)
                    {
                        MessageBox.Show(Resources.PEPatcherInvalidPEFormat, Resources.PEPatcher4GBPatchTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return false;
                    }

                    int characteristicsOffset = (int)peOffset + 22;
                    ushort characteristics = BitConverter.ToUInt16(buffer, characteristicsOffset);

                    // So, technically this helper should only ever get brought up via manual profile flag so this check might be
                    // a bit redundant, but you know, I don't trust like that, so... sanity check time.                    
                    if ((characteristics & IMAGE_FILE_32BIT_MACHINE) == 0)
                    {
                        MessageBox.Show(Resources.PEPatcherCannotPatch64Bit, Resources.PEPatcher4GBPatchTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return false;
                    }

                    characteristics |= IMAGE_FILE_LARGE_ADDRESS_AWARE;

                    byte[] newCharacteristics = BitConverter.GetBytes(characteristics);
                    buffer[characteristicsOffset] = newCharacteristics[0];
                    buffer[characteristicsOffset + 1] = newCharacteristics[1];

                    fs.Seek(0, SeekOrigin.Begin);
                    fs.Write(buffer, 0, buffer.Length);
                    fs.Flush();
                }

                // Recalculate the PE header checksum like the original 4GB patcher does, as otherwise
                // the checksum we get via TP's calculation will differ compared to if someone used the external patcher
                // I thought about just making TP's checksum stuff a bit less static but ... this feels a bit safer at the moment. 
                if (MapFileAndCheckSum(filePath, out uint headerSum, out uint checkSum) == 0)
                {
                    using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite))
                    {
                        byte[] buffer = new byte[Math.Min(fs.Length, 0x8000)];
                        fs.Read(buffer, 0, buffer.Length);

                        uint peOffset = BitConverter.ToUInt32(buffer, 60);
                        int checksumOffset = (int)peOffset + 88;

                        if (checksumOffset + 4 <= buffer.Length)
                        {
                            byte[] newChecksum = BitConverter.GetBytes(checkSum);
                            Array.Copy(newChecksum, 0, buffer, checksumOffset, 4);

                            fs.Seek(0, SeekOrigin.Begin);
                            fs.Write(buffer, 0, buffer.Length);
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(Resources.PEPatcherErrorPatchingFile, ex.Message), Resources.PEPatcher4GBPatchTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }
    }
}