using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Android.Content;

namespace TeknoParrotUi.Avalonia.Android;

internal sealed class AndroidRpcs3x6Firmware(Context context)
{
    private const string Package = "com.teknogods.rpcs3x6";
    private const string Receiver = "net.rpcs3.TeknoParrotSessionControlReceiver";
    private const string Prefix = "com.teknoparrot.rpcs3x6";
    private readonly Context _context = context.ApplicationContext ?? context;

    internal async Task<bool> IsConfiguredAsync()
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var receiver = new FirmwareReceiver(token, completion);
        var filter = new IntentFilter(Prefix + ".action.FIRMWARE_STATUS");
        if (OperatingSystem.IsAndroidVersionAtLeast(33)) _context.RegisterReceiver(receiver, filter, ReceiverFlags.Exported);
        else
#pragma warning disable CA1422
            _context.RegisterReceiver(receiver, filter);
#pragma warning restore CA1422
        try
        {
            var intent = new Intent(Prefix + ".action.QUERY_FIRMWARE");
            intent.SetComponent(new ComponentName(Package, Receiver));
            intent.PutExtra(Prefix + ".extra.CALLBACK_PACKAGE", _context.PackageName);
            intent.PutExtra(Prefix + ".extra.SESSION_TOKEN", token);
            _context.SendBroadcast(intent);
            return await completion.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
        finally
        {
            try { _context.UnregisterReceiver(receiver); } catch (Java.Lang.IllegalArgumentException) { }
            receiver.Dispose();
        }
    }

    private sealed class FirmwareReceiver(string token, TaskCompletionSource<bool> completion) : BroadcastReceiver
    {
        private readonly byte[] _token = System.Text.Encoding.ASCII.GetBytes(token);
        public override void OnReceive(Context? context, Intent? intent)
        {
            if (intent?.Action != Prefix + ".action.FIRMWARE_STATUS") return;
            var received = System.Text.Encoding.ASCII.GetBytes(intent.GetStringExtra(Prefix + ".extra.SESSION_TOKEN") ?? "");
            if (!CryptographicOperations.FixedTimeEquals(_token, received)) return;
            completion.TrySetResult(intent.GetBooleanExtra(Prefix + ".extra.FIRMWARE_READY", false));
        }
    }
}
