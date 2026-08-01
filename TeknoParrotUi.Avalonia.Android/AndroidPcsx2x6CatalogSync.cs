using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Android.Content;

namespace TeknoParrotUi.Avalonia.Android;

/// <summary>
/// Queries PCSX2X6 for complete manifests. The shared catalog exposes matching
/// stock profiles in the Android library without mutating user profiles.
/// </summary>
internal sealed class AndroidPcsx2x6CatalogSync
{
    private const string ArmsPackage = "com.teknogods.tekno2x6";
    private const string ArmsReceiver =
        "com.armsx2.TeknoParrotSessionControlReceiver";
    private const string QueryAction =
        "com.teknoparrot.pcsx2x6.action.QUERY_CATALOG";
    private const string StatusAction =
        "com.teknoparrot.pcsx2x6.action.CATALOG_STATUS";
    private const string CallbackPackageExtra =
        "com.teknoparrot.pcsx2x6.extra.CALLBACK_PACKAGE";
    private const string SessionTokenExtra =
        "com.teknoparrot.pcsx2x6.extra.SESSION_TOKEN";
    private const string GameIdsExtra =
        "com.teknoparrot.pcsx2x6.extra.GAME_IDS";

    private readonly Context _context;

    public AndroidPcsx2x6CatalogSync(Context context) =>
        _context = context.ApplicationContext ?? context;

    public async Task<IReadOnlyCollection<string>> QueryAsync()
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var completion =
            new TaskCompletionSource<IReadOnlyCollection<string>>(
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
            request.SetComponent(new ComponentName(ArmsPackage, ArmsReceiver));
            request.PutExtra(CallbackPackageExtra, _context.PackageName);
            request.PutExtra(SessionTokenExtra, token);
            _context.SendBroadcast(request);

            var gameIds = await completion.Task
                .WaitAsync(TimeSpan.FromSeconds(5))
                .ConfigureAwait(false);
            var ready = gameIds
                .Select(gameId => gameId + ".acgame")
                .ToArray();
            global::Android.Util.Log.Info(
                "TeknoParrotPCSX2X6",
                $"Catalog ready={ready.Length}");
            return ready;
        }
        finally
        {
            try
            {
                _context.UnregisterReceiver(receiver);
            }
            catch (Java.Lang.IllegalArgumentException)
            {
                // Android may tear down the receiver during a timeout.
            }
            receiver.Dispose();
        }
    }

    private sealed class CatalogReceiver : BroadcastReceiver
    {
        private readonly byte[] _expectedToken;
        private readonly TaskCompletionSource<IReadOnlyCollection<string>>
            _completion;

        public CatalogReceiver(
            string expectedToken,
            TaskCompletionSource<IReadOnlyCollection<string>> completion)
        {
            _expectedToken = System.Text.Encoding.ASCII.GetBytes(expectedToken);
            _completion = completion;
        }

        public override void OnReceive(Context? context, Intent? intent)
        {
            if (intent == null ||
                !string.Equals(intent.Action, StatusAction, StringComparison.Ordinal))
                return;

            var receivedToken = System.Text.Encoding.ASCII.GetBytes(
                intent.GetStringExtra(SessionTokenExtra) ?? string.Empty);
            if (!CryptographicOperations.FixedTimeEquals(
                    _expectedToken,
                    receivedToken))
                return;

            var gameIds = intent.GetStringArrayListExtra(GameIdsExtra)
                ?.Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
                ?? Array.Empty<string>();
            _completion.TrySetResult(gameIds);
        }
    }
}
