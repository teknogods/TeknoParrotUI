using System;
using System.Threading;
using System.Threading.Tasks;
using Android.Content;
using Android.OS;
using TeknoParrotUi.AndroidBridge;
using TeknoParrotUi.Common.Activation;

namespace TeknoParrotUi.Avalonia.Android;

/// <summary>
/// Signed cross-package adapter for BudgieLoader inside the single managed
/// Winlator Wine prefix. Serial material remains in memory only and is never
/// placed in an Intent, preference, log or status message.
/// </summary>
internal sealed class AndroidWinlatorActivationBackend
{
    private const string PreferencesName = "teknoparrot-activation";
    private const string CachedActiveKey = "activated";
    private const string WinlatorProvisioningActivity =
        "com.winlator.TeknoParrotProvisioningActivity";
    private readonly Context _context;

    public AndroidWinlatorActivationBackend(Context context)
    {
        _context = context.ApplicationContext ?? context;
    }

    public bool IsActivatedCached() =>
        _context.GetSharedPreferences(PreferencesName, FileCreationMode.Private)
            ?.GetBoolean(CachedActiveKey, false) == true;

    public async Task<TeknoParrotActivationStatus> GetStatusAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var status = await InvokeAsync(
                "status", string.Empty, requestPermission: false, cancellationToken)
                .ConfigureAwait(false);
            SaveCachedStatus(status.IsActivated);
            return new TeknoParrotActivationStatus(status.IsActivated, StatusMessage(status));
        }
        catch (Exception error)
        {
            return new TeknoParrotActivationStatus(
                IsActivatedCached(), "Could not query Winlator activation: " + error.Message);
        }
    }

    public Task<TeknoParrotActivationResult> ActivateAsync(
        string serial,
        CancellationToken cancellationToken) =>
        ChangeAsync("register", serial, expectedActive: true, cancellationToken);

    public Task<TeknoParrotActivationResult> DeactivateAsync(
        CancellationToken cancellationToken) =>
        ChangeAsync("deactivate", string.Empty, expectedActive: false, cancellationToken);

    private async Task<TeknoParrotActivationResult> ChangeAsync(
        string operation,
        string serial,
        bool expectedActive,
        CancellationToken cancellationToken)
    {
        try
        {
            var status = await InvokeAsync(
                operation, serial, requestPermission: true, cancellationToken)
                .ConfigureAwait(false);
            SaveCachedStatus(status.IsActivated);
            var success = status.State == (expectedActive ? "active" : "inactive") &&
                          status.IsActivated == expectedActive;
            return new TeknoParrotActivationResult(
                success,
                status.IsActivated,
                StatusMessage(status),
                Array.Empty<string>());
        }
        catch (Exception error)
        {
            return new TeknoParrotActivationResult(
                false,
                IsActivatedCached(),
                "Android subscription activation failed: " + error.Message,
                Array.Empty<string>());
        }
    }

    private async Task<WinlatorActivationStatus> InvokeAsync(
        string operation,
        string serial,
        bool requestPermission,
        CancellationToken cancellationToken)
    {
        using var connection = new WinlatorConnection(_context);
        var service = await connection.BindAsync(cancellationToken).ConfigureAwait(false);
        var serviceProtocolVersion = service.GetProtocolVersion();
        if (!WinlatorSessionContract.IsCompatibleServiceProtocolVersion(serviceProtocolVersion))
            throw new InvalidOperationException(
                "Install the matching TeknoParrot Winlator companion build.");
        _ = WinlatorSessionContract.ParseCapabilities(
            service.GetCapabilities(serviceProtocolVersion),
            serviceProtocolVersion);

        var status = await Task.Run(
            () => ParseRemoteStatus(
                service.ManageTeknoParrotActivation(1, operation, serial)),
            cancellationToken).ConfigureAwait(false);
        if (!status.NeedsStoragePermission || !requestPermission)
            return status;

        // This operation came directly from the visible Register/Deactivate
        // button. Open Winlator's signature-protected permission trampoline;
        // keep the serial in this method's memory and retry only after the
        // companion reports that permission has actually been granted.
        var permissionIntent = new Intent();
        permissionIntent.SetComponent(new ComponentName(
            BridgeProtocol.WinlatorServicePackage,
            WinlatorProvisioningActivity));
        permissionIntent.AddFlags(ActivityFlags.NewTask | ActivityFlags.NoAnimation);
        _context.StartActivity(permissionIntent);

        var deadline = DateTime.UtcNow.AddMinutes(5);
        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
            status = await Task.Run(
                () => ParseRemoteStatus(
                    service.ManageTeknoParrotActivation(1, operation, serial)),
                cancellationToken).ConfigureAwait(false);
            if (!status.NeedsStoragePermission)
                return status;
        }
        throw new TimeoutException("Winlator game-folder permission was not granted.");
    }

    private static WinlatorActivationStatus ParseRemoteStatus(string? value)
    {
        // Reflection failures in the bridge service predate the v5 JSON
        // activation envelope and use its bounded legacy status form. Convert
        // it here so users see the actual fault without weakening the strict
        // parser for successful responses.
        const string faultPrefix = "state=fault;error=";
        if (value?.StartsWith(faultPrefix, StringComparison.Ordinal) == true)
        {
            var message = value[faultPrefix.Length..]
                .Replace('\r', ' ')
                .Replace('\n', ' ');
            if (message.Length > 512)
                message = message[..512];
            return new WinlatorActivationStatus(1, "fault", false, message);
        }
        return WinlatorSessionContract.ParseActivationStatus(value);
    }

    private void SaveCachedStatus(bool activated)
    {
        _context.GetSharedPreferences(PreferencesName, FileCreationMode.Private)
            ?.Edit()
            ?.PutBoolean(CachedActiveKey, activated)
            ?.Apply();
    }

    private static string StatusMessage(WinlatorActivationStatus status)
    {
        if (!string.IsNullOrWhiteSpace(status.Message))
            return status.Message;
        return status.State switch
        {
            "active" => "Subscription is activated in the managed Winlator container.",
            "inactive" => "No subscription activation is installed in Winlator.",
            "permission-required" => "Allow Winlator game-folder access before activating.",
            "unavailable" => "The installed Winlator companion has no TeknoParrot core runtime.",
            _ => "Winlator could not determine the subscription activation state."
        };
    }

    private sealed class WinlatorConnection : Java.Lang.Object, IServiceConnection, IDisposable
    {
        private readonly Context _context;
        private readonly TaskCompletionSource<ITeknoParrotWinlatorService> _connected =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _bound;

        public WinlatorConnection(Context context)
        {
            _context = context;
        }

        public async Task<ITeknoParrotWinlatorService> BindAsync(
            CancellationToken cancellationToken)
        {
            var intent = new Intent(BridgeProtocol.WinlatorServiceAction);
            intent.SetComponent(new ComponentName(
                BridgeProtocol.WinlatorServicePackage,
                BridgeProtocol.WinlatorServiceClass));
            var flags = Bind.AutoCreate;
            if (OperatingSystem.IsAndroidVersionAtLeast(34))
                flags |= Bind.AllowActivityStarts;
            _bound = _context.BindService(intent, this, flags);
            if (!_bound)
                throw new InvalidOperationException("Android refused the Winlator bridge binding.");
            using var registration = cancellationToken.Register(
                () => _connected.TrySetCanceled(cancellationToken));
            try
            {
                return await _connected.Task
                    .WaitAsync(TimeSpan.FromSeconds(30), cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                throw new TimeoutException(
                    "The Winlator bridge did not connect within 30 seconds. " +
                    "Open TeknoParrot again to retry.");
            }
        }

        public void OnServiceConnected(ComponentName? name, IBinder? service)
        {
            if (service == null)
                _connected.TrySetException(
                    new InvalidOperationException("Winlator returned a null Binder."));
            else
                _connected.TrySetResult(ITeknoParrotWinlatorServiceStub.AsInterface(service));
        }

        public void OnServiceDisconnected(ComponentName? name)
        {
        }

        public void OnBindingDied(ComponentName? name) =>
            _connected.TrySetException(
                new InvalidOperationException("The Winlator activation binding died."));

        public void OnNullBinding(ComponentName? name) =>
            _connected.TrySetException(
                new InvalidOperationException("Winlator returned a null service binding."));

        public new void Dispose()
        {
            if (!_bound)
                return;
            try
            {
                _context.UnbindService(this);
            }
            catch (ArgumentException)
            {
            }
            _bound = false;
        }
    }
}
