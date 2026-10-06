using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using ParrotPatcher;

internal static class Program
{
    private static int assertions;

    private static void Require(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new Exception(message);
    }

    [STAThread]
    private static int Main()
    {
        string originalDirectory = Environment.CurrentDirectory;
        string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fixtures", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Environment.CurrentDirectory = root;
        try
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            File.WriteAllText("user-profile.txt", "preserve");
            using (var form = new Form1())
            {
                // Create a handle for production Invoke calls without showing the patcher window.
                IntPtr unused = form.Handle;
                var install = typeof(Form1).GetMethod("ProcessSingleZip", BindingFlags.Instance | BindingFlags.NonPublic);
                Require(install != null, "Production ZIP installer not found");
                foreach (string component in new[] { "TeknoMVS", "TeknoCPS", "TeknoSS32" })
                {
                    string installedExe = Path.Combine(component, component + ".exe");
                    Require(!File.Exists(installedExe), "Fixture must start with no installed executable");
                    Install(form, install, component, "1.0.0.100", "first install");
                    Require(File.Exists(installedExe), component + " executable missing from TPUI component folder");
                    Require(File.ReadAllText(installedExe) == "first install", component + " executable missing from TPUI component folder");
                    Require(File.ReadAllText(Path.Combine(component, "SDL3.dll")) == "runtime first install", component + " runtime dependency missing");
                    Require(File.ReadAllText(Path.Combine(component, "data", "catalog.txt")) == "catalog first install", component + " nested support file missing");
                    Require(!File.Exists(component + ".exe"), component + " executable incorrectly installed in TPUI root");
                    Require(!Directory.Exists(Path.Combine(component, component)), component + " package installed into a duplicate component folder");
                    File.WriteAllText(Path.Combine(component, "user-settings.txt"), "keep settings");
                    Install(form, install, component, "1.0.0.101", "updated install");
                    Require(File.ReadAllText(installedExe) == "updated install", component + " executable not replaced during update");
                    Require(File.ReadAllText(Path.Combine(component, "SDL3.dll")) == "runtime updated install", component + " runtime dependency not updated");
                    Require(File.ReadAllText(Path.Combine(component, "data", "catalog.txt")) == "catalog updated install", component + " support file not updated");
                    Require(File.ReadAllText(Path.Combine(component, "user-settings.txt")) == "keep settings", component + " unrelated settings changed");
                    Require(Directory.GetFiles(component, component + ".exe", SearchOption.AllDirectories).Length == 1, component + " installed executable duplicated");
                }
            }
            Require(File.ReadAllText("user-profile.txt") == "preserve", "Unrelated TPUI root file changed");
            Console.WriteLine("PASS: " + assertions + " assertions; all three real patcher fresh-install and update paths preserve executables, dependencies, subfolders and user files. No downloads or games started.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
        finally
        {
            Environment.CurrentDirectory = originalDirectory;
        }
    }

    private static void Install(Form1 form, MethodInfo install, string component, string version, string payload)
    {
        Directory.CreateDirectory("cache");
        string archive = Path.Combine("cache", component + version + ".zip");
        using (var stream = File.Create(archive))
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            Add(zip, component + ".exe", payload);
            Add(zip, "SDL3.dll", "runtime " + payload);
            zip.CreateEntry("data/");
            Add(zip, "data/catalog.txt", "catalog " + payload);
        }
        // Use the same ZIP recognition and extraction entry point as a downloaded update.
        install.Invoke(form, new object[] { archive });
    }

    private static void Add(ZipArchive zip, string name, string payload)
    {
        using (var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false)))
            writer.Write(payload);
    }
}
