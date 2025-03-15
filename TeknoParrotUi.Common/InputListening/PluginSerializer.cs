using System;
using System.Text.Json;

namespace TeknoParrotUi.Common
{
    // Helper class to serialize plugin-specific data
    public static class PluginSerializer
    {
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false,
            IncludeFields = true,
            MaxDepth = 64
        };

        // Type information wrapper for serialization
        private class TypedData
        {
            public string TypeName { get; set; }
            public string AssemblyName { get; set; }
            public string Data { get; set; }
        }

        public static byte[] SerializeToBytes(object data)
        {
            if (data == null) return Array.Empty<byte>();

            try
            {
                // Wrap the data with type information
                var typedData = new TypedData
                {
                    TypeName = data.GetType().FullName,
                    AssemblyName = data.GetType().Assembly.GetName().Name,
                    Data = JsonSerializer.Serialize(data, data.GetType(), _jsonOptions)
                };

                // Serialize the wrapper
                string json = JsonSerializer.Serialize(typedData, _jsonOptions);
                return System.Text.Encoding.UTF8.GetBytes(json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Serialization error: {ex.Message}");
                return Array.Empty<byte>();
            }
        }

        public static object DeserializeFromBase64(string base64, string pluginId)
        {
            if (string.IsNullOrEmpty(base64)) return null;

            try
            {
                byte[] bytes = Convert.FromBase64String(base64);
                string json = System.Text.Encoding.UTF8.GetString(bytes);

                // Deserialize the wrapper first
                var typedData = JsonSerializer.Deserialize<TypedData>(json, _jsonOptions);

                if (typedData == null) return null;

                // Find the type
                Type dataType = null;

                // Try to find type in the specified assembly
                if (!string.IsNullOrEmpty(typedData.AssemblyName))
                {
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        if (assembly.GetName().Name == typedData.AssemblyName)
                        {
                            dataType = assembly.GetType(typedData.TypeName);
                            if (dataType != null) break;
                        }
                    }
                }

                // If not found, search in all loaded assemblies
                if (dataType == null)
                {
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        dataType = assembly.GetType(typedData.TypeName);
                        if (dataType != null) break;
                    }
                }

                // If type is found, deserialize the actual data
                if (dataType != null)
                {
                    return JsonSerializer.Deserialize(typedData.Data, dataType, _jsonOptions);
                }

                // Special handler for legacy types or when type not found
                System.Diagnostics.Debug.WriteLine($"Type {typedData.TypeName} not found for plugin {pluginId}");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Deserialization error for plugin {pluginId}: {ex.Message}");
                return null;
            }
        }
    }
}