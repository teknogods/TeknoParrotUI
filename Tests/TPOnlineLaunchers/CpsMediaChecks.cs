using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Xml.Serialization;
using TeknoParrotUi.Common;
using TeknoParrotUi.Views.GameRunningCode.ProcessManagement;

internal static class CpsMediaChecks
{
    private static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }

    internal static int Child(string[] args)
    {
        if (args[1] == "timeout")
        {
            File.WriteAllText(args[2], Process.GetCurrentProcess().Id.ToString());
            Thread.Sleep(30000);
        }
        Console.Out.Write(new string('o', 100000));
        Console.Error.Write(new string('e', 100000));
        return args[1] == "fail" ? 7 : 0;
    }

    internal static void Run(string root, string temporary)
    {
        GameProfile profile;
        using (var stream = File.OpenRead(Path.Combine(root, "TeknoParrotUi.Common/GameProfiles/cps_sfiii3n.xml")))
            profile = (GameProfile)new XmlSerializer(typeof(GameProfile)).Deserialize(stream);
        profile.ProfileName = "cps_sfiii3n";
        var zip = Path.Combine(temporary, "sfiii3n.zip");
        File.WriteAllBytes(zip, new byte[0]);
        Environment.SetEnvironmentVariable("TP_TPONLINE2", "should-not-connect|0|Player|2");
        profile.ConfigValues.Single(v => v.FieldName == "Window Scale").FieldValue = "invalid-local-scale";
        profile.ConfigValues.Single(v => v.FieldName == "Window Mode").FieldValue = "Fullscreen";
        profile.ConfigValues.Single(v => v.FieldName == "Use Bezel").FieldValue = "1";
        profile.ConfigValues.Single(v => v.FieldName == "CRT Shader").FieldValue = "lottes";
        var prepared = TeknoCPSLauncher.Build(profile, zip, null, isTest: true, prepareMedia: true);
        Program.NativeLaunch(prepared, renders: false);
        var arguments = Program.Arguments(prepared.Arguments);
        Require(arguments.Contains("--prepare-media") && arguments.Contains("--no-nvram") &&
            !arguments.Contains("--test-menu") && !arguments.Contains("--nvram-dir"), "Preparation must not play or mutate offline cabinet state");
        Require(!arguments.Contains("--fullscreen") && !arguments.Contains("--use-bezel") && !arguments.Contains("--crt"), "Presentation options leaked into headless media preparation");
        Require(!prepared.EnvironmentVariables.ContainsKey("TP_TPONLINE2") && prepared.CreateNoWindow &&
            prepared.RedirectStandardOutput && prepared.RedirectStandardError && !prepared.UseShellExecute, "Preparation process isolation");

        var exe = Assembly.GetExecutingAssembly().Location;
        ProcessStartInfo ChildInfo(string mode) => new ProcessStartInfo(exe, "--media-preparation-child " + mode)
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        var progressCount = 0;
        TeknoCPSLauncher.RunMediaPreparationAsync(ChildInfo("ok"), TimeSpan.FromSeconds(10), message =>
        { Require(message.Length <= 500, "Unbounded progress message"); ++progressCount; }).GetAwaiter().GetResult();
        Require(progressCount > 0, "Native progress was not delivered");
        var failed = false;
        try { TeknoCPSLauncher.RunMediaPreparationAsync(ChildInfo("fail"), TimeSpan.FromSeconds(10)).GetAwaiter().GetResult(); }
        catch (InvalidOperationException error) { failed = error.Message.Contains("Game media preparation failed") && error.Message.Length < 8300; }
        Require(failed, "Preparation failure did not preserve bounded diagnostics");

        var pidPath = Path.Combine(temporary, "preparation-child.pid");
        var timedOut = false;
        try { TeknoCPSLauncher.RunMediaPreparationAsync(ChildInfo("timeout \"" + pidPath + "\""), TimeSpan.FromSeconds(2)).GetAwaiter().GetResult(); }
        catch (TimeoutException) { timedOut = true; }
        Require(timedOut && File.Exists(pidPath), "Preparation timeout was not exercised");
        var alive = false;
        try { using (var child = Process.GetProcessById(int.Parse(File.ReadAllText(pidPath)))) alive = !child.HasExited; }
        catch (ArgumentException) { }
        Require(!alive, "Timed-out preparation process was left running");
        File.Delete(pidPath);
        using (var cancellation = new CancellationTokenSource())
        {
            var pending = TeknoCPSLauncher.RunMediaPreparationAsync(ChildInfo("timeout \"" + pidPath + "\""), TimeSpan.FromSeconds(10), cancellation: cancellation.Token);
            var until = DateTime.UtcNow.AddSeconds(3);
            while (!File.Exists(pidPath) && DateTime.UtcNow < until) Thread.Sleep(10);
            Require(File.Exists(pidPath), "Cancellable preparation did not start");
            cancellation.Cancel();
            var cancelled = false;
            try { pending.GetAwaiter().GetResult(); } catch (OperationCanceledException) { cancelled = true; }
            Require(cancelled, "Preparation cancellation did not complete");
            alive = false;
            try { using (var child = Process.GetProcessById(int.Parse(File.ReadAllText(pidPath)))) alive = !child.HasExited; }
            catch (ArgumentException) { }
            Require(!alive, "Cancelled preparation process was left running");
        }
        Console.WriteLine("CPS media preparation: isolation, progress, pipe drainage, bounded diagnostics, owned-process timeout and cancellation passed.");
    }
}
