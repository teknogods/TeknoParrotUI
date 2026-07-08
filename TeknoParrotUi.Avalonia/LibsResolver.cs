using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace TeknoParrotUi.Avalonia;

/// <summary>
/// Resolves managed and native dependencies from the "libs" subfolder so the
/// application root stays clean (only the exe + host config files). Falls back
/// to default resolution when the file is not there. Must be registered before
/// any type from a dependency assembly is touched.
/// </summary>
internal static class LibsResolver
{
    private static string _libsDir = "";

    public static void Register()
    {
        _libsDir = Path.Combine(AppContext.BaseDirectory, "libs");
        if (!Directory.Exists(_libsDir))
            return;

        AssemblyLoadContext.Default.Resolving += ResolveManaged;
        AssemblyLoadContext.Default.ResolvingUnmanagedDll += ResolveNative;
    }

    private static Assembly? ResolveManaged(AssemblyLoadContext context, AssemblyName name)
    {
        // Satellite (translation) assemblies live in libs\{culture}\
        if (name.Name != null && name.Name.EndsWith(".resources") && !string.IsNullOrEmpty(name.CultureName))
        {
            var satellite = Path.Combine(_libsDir, name.CultureName, name.Name + ".dll");
            if (File.Exists(satellite))
                return context.LoadFromAssemblyPath(satellite);
        }

        var candidate = Path.Combine(_libsDir, name.Name + ".dll");
        return File.Exists(candidate) ? context.LoadFromAssemblyPath(candidate) : null;
    }

    private static IntPtr ResolveNative(Assembly assembly, string libraryName)
    {
        foreach (var candidate in new[]
                 {
                     Path.Combine(_libsDir, libraryName),
                     Path.Combine(_libsDir, libraryName + ".dll")
                 })
        {
            if (File.Exists(candidate) &&
                System.Runtime.InteropServices.NativeLibrary.TryLoad(candidate, out var handle))
                return handle;
        }
        return IntPtr.Zero;
    }
}
