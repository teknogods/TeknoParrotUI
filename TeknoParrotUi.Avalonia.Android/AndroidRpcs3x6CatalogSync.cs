using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Android.Content;
using Android.Content.PM;

namespace TeknoParrotUi.Avalonia.Android;

internal sealed class AndroidRpcs3x6CatalogSync
{
    private const string Package = "com.teknogods.rpcs3x6";
    private const string Receiver = "net.rpcs3.TeknoParrotSessionControlReceiver";
    private const string Prefix = "com.teknoparrot.rpcs3x6";
    private static readonly object Sync = new();
    private static Dictionary<string, string> _paths = new(StringComparer.OrdinalIgnoreCase);
    private readonly Context _context;

    internal AndroidRpcs3x6CatalogSync(Context context) =>
        _context = context.ApplicationContext ?? context;

    internal static bool TryGetGamePath(string? profileName, out string path)
    {
        lock (Sync)
            return _paths.TryGetValue(profileName ?? string.Empty, out path!);
    }

    internal async Task<IReadOnlyDictionary<string, string>> QueryAsync()
    {
        try { _ = _context.PackageManager?.GetPackageInfo(Package, PackageInfoFlags.Activities); }
        catch (PackageManager.NameNotFoundException) { return Publish(new Dictionary<string, string>()); }

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var completion = new TaskCompletionSource<IReadOnlyDictionary<string, string>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var receiver = new CatalogReceiver(token, completion);
        var filter = new IntentFilter(Prefix + ".action.CATALOG_STATUS");
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
            _context.RegisterReceiver(receiver, filter, ReceiverFlags.Exported);
        else
#pragma warning disable CA1422
            _context.RegisterReceiver(receiver, filter);
#pragma warning restore CA1422
        try
        {
            var request = new Intent(Prefix + ".action.QUERY_CATALOG");
            request.SetComponent(new ComponentName(Package, Receiver));
            request.PutExtra(Prefix + ".extra.CALLBACK_PACKAGE", _context.PackageName);
            request.PutExtra(Prefix + ".extra.SESSION_TOKEN", token);
            _context.SendBroadcast(request);
            return Publish(await completion.Task.WaitAsync(TimeSpan.FromSeconds(8)).ConfigureAwait(false));
        }
        finally
        {
            try { _context.UnregisterReceiver(receiver); }
            catch (Java.Lang.IllegalArgumentException) { }
            receiver.Dispose();
        }
    }

    private static IReadOnlyDictionary<string, string> Publish(IReadOnlyDictionary<string, string> paths)
    {
        lock (Sync)
            _paths = paths.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);
        return paths;
    }

    private sealed class CatalogReceiver(
        string token,
        TaskCompletionSource<IReadOnlyDictionary<string, string>> completion) : BroadcastReceiver
    {
        private readonly byte[] _token = System.Text.Encoding.ASCII.GetBytes(token);
        public override void OnReceive(Context? context, Intent? intent)
        {
            if (intent?.Action != Prefix + ".action.CATALOG_STATUS") return;
            var received = System.Text.Encoding.ASCII.GetBytes(
                intent.GetStringExtra(Prefix + ".extra.SESSION_TOKEN") ?? "");
            if (!CryptographicOperations.FixedTimeEquals(_token, received)) return;
            var profiles = intent.GetStringArrayListExtra(Prefix + ".extra.PROFILE_NAMES") ?? [];
            var paths = intent.GetStringArrayListExtra(Prefix + ".extra.GAME_PATHS") ?? [];
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < Math.Min(profiles.Count, paths.Count); index++)
            {
                var profile = profiles[index];
                var path = paths[index];
                if (SupportedProfiles.Contains(profile) &&
                    path.StartsWith("/storage/emulated/0/Android/data/com.teknogods.rpcs3x6/files/TeknoParrot/arcade/", StringComparison.Ordinal) &&
                    path.EndsWith("/dev_hdd0/game/SCEEXE000/USRDIR/EBOOT.BIN", StringComparison.Ordinal))
                    result[profile] = path;
            }
            completion.TrySetResult(result);
        }
    }

    private static readonly HashSet<string> SupportedProfiles = new(StringComparer.OrdinalIgnoreCase)
    { "DarkEscape4D", "DSPS", "dbzenkai", "RazingStorm", "AKB48", "taikogreen", "taikoyellow", "Tekken6", "Tekken6BR", "ttt2", "ttt2u" };
}
