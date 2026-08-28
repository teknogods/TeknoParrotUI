using System;
using System.Diagnostics;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Helpers
{
    internal static class ParrotDataSerializer
    {
        public static void Save(ParrotData data, string fileName)
        {
            var path = Path.GetFullPath(fileName);
            var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                // Entitize CR characters so the cached feed text survives XML newline normalization.
                var settings = new XmlWriterSettings { NewLineHandling = NewLineHandling.Entitize };
                using (var writer = XmlWriter.Create(temporaryPath, settings))
                {
                    new XmlSerializer(typeof(ParrotData)).Serialize(writer, data);
                }

                // A failed save must not truncate the existing settings file.
                if (File.Exists(path))
                    File.Replace(temporaryPath, path, null);
                else
                    File.Move(temporaryPath, path);
            }
            finally
            {
                try
                {
                    if (File.Exists(temporaryPath))
                        File.Delete(temporaryPath);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to remove temporary settings file: {ex.Message}");
                }
            }
        }
    }
}
