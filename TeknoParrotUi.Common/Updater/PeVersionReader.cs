using System;
using System.Collections.Generic;
using System.IO;

namespace TeknoParrotUi.Common.Updater
{
    /// <summary>
    /// Minimal PE (Windows executable/DLL) VS_VERSIONINFO resource reader.
    ///
    /// .NET's own <see cref="System.Diagnostics.FileVersionInfo"/> is supposed to be
    /// cross-platform, but on Linux it silently fails to parse the version resource of
    /// real-world Windows PE files (returns empty FileVersion/ProductVersion), which made
    /// every component the updater tracks show up as "unknown" and constantly nag for
    /// updates. This walks the PE resource directory by hand to pull the numeric
    /// VS_FIXEDFILEINFO version fields directly - no string/codepage parsing needed, and
    /// verified byte-for-byte against known-good version numbers (via Python's pefile).
    /// </summary>
    internal static class PeVersionReader
    {
        private const int ResourceTypeVersion = 16; // RT_VERSION

        /// <summary>
        /// Reads the product version (falling back to file version) as "major.minor.build.revision",
        /// or null if the file isn't a PE image or has no version resource.
        /// </summary>
        public static string ReadProductVersion(string path)
        {
            try
            {
                var bytes = File.ReadAllBytes(path);
                return ReadProductVersion(bytes);
            }
            catch
            {
                return null;
            }
        }

        internal static string ReadProductVersion(byte[] bytes)
        {
            if (!TryFindFixedFileInfo(bytes, out var fixedFileInfo))
                return null;

            uint productMs = BitConverter.ToUInt32(bytes, fixedFileInfo + 16);
            uint productLs = BitConverter.ToUInt32(bytes, fixedFileInfo + 20);
            if (productMs == 0 && productLs == 0)
            {
                // Some binaries only set FileVersion, not ProductVersion.
                productMs = BitConverter.ToUInt32(bytes, fixedFileInfo + 8);
                productLs = BitConverter.ToUInt32(bytes, fixedFileInfo + 12);
            }

            if (productMs == 0 && productLs == 0)
                return null;

            return $"{HighWord(productMs)}.{LowWord(productMs)}.{HighWord(productLs)}.{LowWord(productLs)}";
        }

        private static ushort HighWord(uint value) => (ushort)(value >> 16);
        private static ushort LowWord(uint value) => (ushort)(value & 0xFFFF);

        /// <summary>Locates the VS_FIXEDFILEINFO struct and returns its file offset.</summary>
        private static bool TryFindFixedFileInfo(byte[] bytes, out int fixedFileInfoOffset)
        {
            fixedFileInfoOffset = 0;

            if (bytes.Length < 0x40 || bytes[0] != 'M' || bytes[1] != 'Z')
                return false;

            int peOffset = (int)ReadU32(bytes, 0x3C);
            if (peOffset <= 0 || peOffset + 24 >= bytes.Length ||
                bytes[peOffset] != 'P' || bytes[peOffset + 1] != 'E' || bytes[peOffset + 2] != 0 || bytes[peOffset + 3] != 0)
                return false;

            int coffOffset = peOffset + 4;
            ushort numberOfSections = ReadU16(bytes, coffOffset + 2);
            ushort sizeOfOptionalHeader = ReadU16(bytes, coffOffset + 16);
            int optHeaderOffset = coffOffset + 20;
            if (sizeOfOptionalHeader == 0 || optHeaderOffset + 2 >= bytes.Length)
                return false;

            ushort magic = ReadU16(bytes, optHeaderOffset);
            bool isPe32Plus = magic == 0x20B;
            int dataDirOffset = optHeaderOffset + (isPe32Plus ? 112 : 96);
            const int resourceDirIndex = 2;
            int resourceEntryOffset = dataDirOffset + resourceDirIndex * 8;
            if (resourceEntryOffset + 8 > bytes.Length)
                return false;

            uint resourceRva = ReadU32(bytes, resourceEntryOffset);
            if (resourceRva == 0)
                return false;

            int sectionHeaderOffset = optHeaderOffset + sizeOfOptionalHeader;
            var sections = new List<(uint va, uint size, uint rawPtr)>();
            for (int i = 0; i < numberOfSections; i++)
            {
                int off = sectionHeaderOffset + i * 40;
                if (off + 40 > bytes.Length)
                    return false;
                uint vsize = ReadU32(bytes, off + 8);
                uint va = ReadU32(bytes, off + 12);
                uint rawPtr = ReadU32(bytes, off + 20);
                sections.Add((va, vsize, rawPtr));
            }

            int RvaToOffset(uint rva)
            {
                foreach (var s in sections)
                    if (rva >= s.va && rva < s.va + s.size)
                        return (int)(s.rawPtr + (rva - s.va));
                return -1;
            }

            int resourceSectionOffset = RvaToOffset(resourceRva);
            if (resourceSectionOffset < 0)
                return false;

            int dataEntryOffset = FindVersionDataEntry(bytes, resourceSectionOffset);
            if (dataEntryOffset < 0)
                return false;

            int absoluteDataEntry = resourceSectionOffset + dataEntryOffset;
            if (absoluteDataEntry + 8 > bytes.Length)
                return false;

            uint dataRva = ReadU32(bytes, absoluteDataEntry);
            int dataOffset = RvaToOffset(dataRva);
            if (dataOffset < 0)
                return false;

            // VS_VERSIONINFO: wLength(2) wValueLength(2) wType(2) + L"VS_VERSION_INFO\0" (32 bytes),
            // padded to a 4-byte boundary, then the VS_FIXEDFILEINFO struct itself.
            int p = dataOffset + 6 + 32;
            int relative = p - dataOffset;
            if (relative % 4 != 0)
                p += 2;

            if (p + 24 > bytes.Length)
                return false;

            uint signature = ReadU32(bytes, p);
            if (signature != 0xFEEF04BD)
                return false;

            fixedFileInfoOffset = p;
            return true;
        }

        /// <summary>
        /// Walks Type (RT_VERSION) -> Name -> Language resource directory levels and
        /// returns the offset (relative to the resource section start) of the first
        /// data entry found.
        /// </summary>
        private static int FindVersionDataEntry(byte[] bytes, int resourceSectionOffset)
        {
            int typeDir = resourceSectionOffset;
            int nameSubOffset = FindEntry(bytes, typeDir, (int id) => id == ResourceTypeVersion);
            if (nameSubOffset < 0)
                return -1;

            int nameDir = resourceSectionOffset + nameSubOffset;
            int langSubOffset = FindEntry(bytes, nameDir, null);
            if (langSubOffset < 0)
                return -1;

            int langDir = resourceSectionOffset + langSubOffset;
            return FindEntry(bytes, langDir, null, wantDataEntry: true);
        }

        /// <summary>
        /// Returns the first matching entry's target offset (subdirectory or data entry,
        /// both relative to the resource section start), or -1 if none matched.
        /// </summary>
        private static int FindEntry(byte[] bytes, int dirOffset, Func<int, bool> idPredicate, bool wantDataEntry = false)
        {
            if (dirOffset + 16 > bytes.Length)
                return -1;

            ushort numNamed = ReadU16(bytes, dirOffset + 12);
            ushort numId = ReadU16(bytes, dirOffset + 14);
            int entriesOffset = dirOffset + 16;
            int total = numNamed + numId;

            for (int i = 0; i < total; i++)
            {
                int eoff = entriesOffset + i * 8;
                if (eoff + 8 > bytes.Length)
                    return -1;

                uint idOrNameOffset = ReadU32(bytes, eoff);
                uint dataOrSubdirOffset = ReadU32(bytes, eoff + 4);
                bool isSubdir = (dataOrSubdirOffset & 0x80000000) != 0;
                int target = (int)(dataOrSubdirOffset & 0x7FFFFFFF);

                if (idPredicate != null)
                {
                    bool isNamedEntry = (idOrNameOffset & 0x80000000) != 0;
                    if (isNamedEntry || !idPredicate((int)idOrNameOffset))
                        continue;
                }

                if (wantDataEntry)
                {
                    if (!isSubdir)
                        return target;
                    continue;
                }

                return target;
            }

            return -1;
        }

        private static uint ReadU32(byte[] bytes, int offset) => BitConverter.ToUInt32(bytes, offset);
        private static ushort ReadU16(byte[] bytes, int offset) => BitConverter.ToUInt16(bytes, offset);
    }
}
