using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Android.Content;
using Android.Content.PM;

namespace TeknoParrotUi.Avalonia.Android;

internal sealed class AndroidDolphinCatalogSync
{
    private const string Package = "com.teknogods.teknodolphin";
    private const string Receiver =
        "org.dolphinemu.dolphinemu.teknoparrot.TeknoParrotSessionControlReceiver";
    private const string Prefix = "com.teknoparrot.dolphin";
    private const string QueryAction = Prefix + ".action.QUERY_CATALOG";
    private const string StatusAction = Prefix + ".action.CATALOG_STATUS";
    private const string CallbackExtra = Prefix + ".extra.CALLBACK_PACKAGE";
    private const string TokenExtra = Prefix + ".extra.SESSION_TOKEN";
    private const string GamesExtra = Prefix + ".extra.GAME_IDS";
    private readonly Context _context;

    public AndroidDolphinCatalogSync(Context context) =>
        _context = context.ApplicationContext ?? context;

    public async Task<IReadOnlyCollection<string>> QueryAsync()
    {
        try
        {
            _ = _context.PackageManager?.GetPackageInfo(
                Package,
                PackageInfoFlags.Activities);
        }
        catch (PackageManager.NameNotFoundException)
        {
            return Array.Empty<string>();
        }

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var completion = new TaskCompletionSource<IReadOnlyCollection<string>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var receiver = new CatalogReceiver(token, completion);
        var filter = new IntentFilter(StatusAction);
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
            _context.RegisterReceiver(receiver, filter, ReceiverFlags.Exported);
        else
#pragma warning disable CA1422
            _context.RegisterReceiver(receiver, filter);
#pragma warning restore CA1422
        try
        {
            var request = new Intent(QueryAction);
            request.SetComponent(new ComponentName(Package, Receiver));
            request.PutExtra(CallbackExtra, _context.PackageName);
            request.PutExtra(TokenExtra, token);
            _context.SendBroadcast(request);
            return await completion.Task.WaitAsync(TimeSpan.FromSeconds(5))
                .ConfigureAwait(false);
        }
        finally
        {
            try { _context.UnregisterReceiver(receiver); }
            catch (Java.Lang.IllegalArgumentException) { }
            receiver.Dispose();
        }
    }

    private sealed class CatalogReceiver(
        string token,
        TaskCompletionSource<IReadOnlyCollection<string>> completion)
        : BroadcastReceiver
    {
        private readonly byte[] _token = System.Text.Encoding.ASCII.GetBytes(token);

        public override void OnReceive(Context? context, Intent? intent)
        {
            if (intent?.Action != StatusAction)
                return;
            var received = System.Text.Encoding.ASCII.GetBytes(
                intent.GetStringExtra(TokenExtra) ?? "");
            if (!CryptographicOperations.FixedTimeEquals(_token, received))
                return;
            completion.TrySetResult(
                intent.GetStringArrayListExtra(GamesExtra)
                    ?.Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray() ?? []);
        }
    }
}
