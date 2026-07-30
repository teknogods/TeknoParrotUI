using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Android.Content;

namespace TeknoParrotUi.Avalonia.Android;

/// <summary>
/// Authenticated BIOS-readiness query for the PCSX2X6 companion. The actual
/// BIOS file remains in PCSX2X6 private storage and is never exposed to TPUI.
/// </summary>
internal sealed class AndroidPcsx2x6Bios
{
    private const string ArmsPackage = "com.teknogods.tekno2x6";
    private const string ArmsReceiver =
        "com.armsx2.TeknoParrotSessionControlReceiver";
    private const string QueryAction =
        "com.teknoparrot.pcsx2x6.action.QUERY_BIOS";
    private const string StatusAction =
        "com.teknoparrot.pcsx2x6.action.BIOS_STATUS";
    private const string CallbackPackageExtra =
        "com.teknoparrot.pcsx2x6.extra.CALLBACK_PACKAGE";
    private const string SessionTokenExtra =
        "com.teknoparrot.pcsx2x6.extra.SESSION_TOKEN";
    private const string BiosReadyExtra =
        "com.teknoparrot.pcsx2x6.extra.BIOS_READY";

    private readonly Context _context;

    public AndroidPcsx2x6Bios(Context context) =>
        _context = context.ApplicationContext ?? context;

    public async Task<bool> IsConfiguredAsync(
        CancellationToken cancellationToken)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var completion = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var receiver = new BiosStatusReceiver(token, completion);
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
            return await completion.Task
                .WaitAsync(TimeSpan.FromSeconds(5), cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            try
            {
                _context.UnregisterReceiver(receiver);
            }
            catch (Java.Lang.IllegalArgumentException)
            {
                // Android may tear down the receiver during cancellation.
            }
            receiver.Dispose();
        }
    }

    private sealed class BiosStatusReceiver : BroadcastReceiver
    {
        private readonly byte[] _expectedToken;
        private readonly TaskCompletionSource<bool> _completion;

        public BiosStatusReceiver(
            string expectedToken,
            TaskCompletionSource<bool> completion)
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

            _completion.TrySetResult(
                intent.GetBooleanExtra(BiosReadyExtra, false));
        }
    }
}
