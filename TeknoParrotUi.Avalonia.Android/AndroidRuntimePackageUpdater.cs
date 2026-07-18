using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using Android.Content;
using Android.OS;
using TeknoParrotUi.AndroidBridge;
using TeknoParrotUi.Common.Updater;

namespace TeknoParrotUi.Avalonia.Android;

/// <summary>
/// Downloads a content-addressed shared runtime archive into TPUI's private
/// cache, verifies the published asset, adapts it to Winlator's installation
/// envelope locally, and transfers a read-only descriptor to the
/// signature-protected bridge. Emulator/core payloads never live in either APK.
/// </summary>
internal sealed class AndroidRuntimePackageUpdater
{
    private const string PreferenceName = "teknoparrot-runtime-packages";
    private readonly Context _context;

    public AndroidRuntimePackageUpdater(Context context) =>
        _context = context.ApplicationContext
            ?? throw new ArgumentException("Android application context is unavailable.");

    public string ReadInstalledVersion(string packageId) =>
        _context.GetSharedPreferences(PreferenceName, FileCreationMode.Private)!
            .GetString(packageId, UpdaterComponent.NotInstalled)
        ?? UpdaterComponent.NotInstalled;

    public async Task<IReadOnlyDictionary<string, string>> QueryInstalledVersionsAsync(
        CancellationToken cancellationToken)
    {
        using var connection = new WinlatorRuntimeConnection(_context);
        var binder = await connection.BindAsync(cancellationToken).ConfigureAwait(false);
        var data = Parcel.Obtain();
        var reply = Parcel.Obtain();
        try
        {
            data.WriteInterfaceToken(BridgeProtocol.WinlatorInterfaceDescriptor);
            if (!binder.Transact(
                    BridgeProtocol.QueryWinlatorRuntimePackagesTransaction,
                    data,
                    reply,
                    0))
                throw new InvalidOperationException(
                    "Winlator rejected the runtime-package query.");
            reply.ReadException();
            var envelope = reply.ReadString();
            if (string.IsNullOrWhiteSpace(envelope))
                throw new InvalidDataException(
                    "Winlator returned no runtime-package status.");
            using var document = JsonDocument.Parse(envelope);
            var root = document.RootElement;
            if (!root.TryGetProperty("schemaVersion", out var schema) ||
                schema.GetInt32() != 1 ||
                !root.TryGetProperty("packages", out var packages) ||
                packages.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException(
                    "Winlator returned an incompatible runtime-package status.");
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var package in packages.EnumerateObject())
            {
                if (package.Value.ValueKind == JsonValueKind.String &&
                    package.Value.GetString() is { Length: > 0 } version)
                    result[package.Name] = version;
            }
            return result;
        }
        finally
        {
            reply.Recycle();
            data.Recycle();
        }
    }

    public async Task<string> DownloadAndInstallAsync(
        UpdateCheckResult update,
        IProgress<double> progress,
        CancellationToken cancellationToken)
    {
        var packageId = update.Component.runtimePackageId;
        if (string.IsNullOrWhiteSpace(packageId))
            throw new InvalidDataException("The Android runtime package has no package id.");

        var asset = UpdaterCore.PickAssetForPlatform(
            update.Component,
            update.Release,
            "android")
            ?? throw new InvalidOperationException(
                $"No Android runtime archive was published for {packageId}.");
        if (!Uri.TryCreate(asset.browser_download_url, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidDataException("The Android runtime package URL is not HTTPS.");
        if (!TryParseSha256(asset.digest, out var expectedDigest))
            throw new InvalidDataException(
                $"{packageId} has no authoritative sha256 digest. " +
                "The updater will not install an unverifiable runtime package.");
        if (asset.size <= 0)
            throw new InvalidDataException(
                $"{packageId} has no authoritative package size.");

        var updateDir = Path.Combine(_context.CacheDir!.AbsolutePath, "runtime-updates");
        Directory.CreateDirectory(updateDir);
        var archivePath = Path.Combine(updateDir, packageId + ".zip");
        var actualDigest = await DownloadVerifiedAsync(
            uri,
            archivePath,
            asset.size,
            progress,
            cancellationToken).ConfigureAwait(false);
        if (!CryptographicOperations.FixedTimeEquals(expectedDigest, actualDigest))
            throw new InvalidDataException(
                $"The downloaded {packageId} archive failed SHA256 verification.");

        var installEnvelopePath = Path.Combine(
            updateDir,
            packageId + ".install.zip");
        var installEnvelopeDigest =
            SharedOpenParrotArchiveAdapter.CreateInstallEnvelope(
                archivePath,
                installEnvelopePath,
                packageId,
                update.OnlineVersion,
                cancellationToken);
        progress.Report(95);

        using var descriptor = ParcelFileDescriptor.Open(
            new Java.IO.File(installEnvelopePath),
            ParcelFileMode.ReadOnly)
            ?? throw new IOException("Could not open the runtime archive for Winlator.");
        using var connection = new WinlatorRuntimeConnection(_context);
        var binder = await connection.BindAsync(cancellationToken).ConfigureAwait(false);
        var status = InstallThroughBridge(
            binder,
            descriptor,
            packageId,
            update.OnlineVersion,
            "sha256:" +
            Convert.ToHexString(installEnvelopeDigest).ToLowerInvariant());

        var preferences = _context.GetSharedPreferences(
            PreferenceName,
            FileCreationMode.Private)
            ?? throw new InvalidOperationException(
                "Android runtime-package preferences are unavailable.");
        var editor = preferences.Edit()
            ?? throw new InvalidOperationException(
                "Android runtime-package preferences cannot be edited.");
        editor.PutString(packageId, update.OnlineVersion);
        editor.Apply();
        update.Component._localVersion = update.OnlineVersion;
        progress.Report(100);
        return string.IsNullOrWhiteSpace(status)
            ? $"{packageId} {update.OnlineVersion} installed."
            : status;
    }

    private static async Task<byte[]> DownloadVerifiedAsync(
        Uri uri,
        string destination,
        long expectedSize,
        IProgress<double> progress,
        CancellationToken cancellationToken)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        using var response = await client.GetAsync(
            uri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is long contentLength &&
            contentLength != expectedSize)
            throw new InvalidDataException(
                $"Runtime package length is {contentLength}, expected {expectedSize}.");

        await using var input = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var output = new FileStream(
            destination,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            1024 * 1024,
            useAsync: true);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[1024 * 1024];
        long received = 0;
        int count;
        while ((count = await input.ReadAsync(
                   buffer.AsMemory(),
                   cancellationToken).ConfigureAwait(false)) != 0)
        {
            received += count;
            if (received > expectedSize)
                throw new InvalidDataException(
                    "Runtime package exceeded its declared size.");
            hash.AppendData(buffer, 0, count);
            await output.WriteAsync(
                buffer.AsMemory(0, count),
                cancellationToken).ConfigureAwait(false);
            progress.Report(received * 90d / expectedSize);
        }
        if (received != expectedSize)
            throw new InvalidDataException(
                $"Runtime package length is {received}, expected {expectedSize}.");
        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
        return hash.GetHashAndReset();
    }

    private static string InstallThroughBridge(
        IBinder binder,
        ParcelFileDescriptor descriptor,
        string packageId,
        string version,
        string digest)
    {
        var data = Parcel.Obtain();
        var reply = Parcel.Obtain();
        try
        {
            data.WriteInterfaceToken(BridgeProtocol.WinlatorInterfaceDescriptor);
            data.WriteString(packageId);
            data.WriteString(version);
            data.WriteString(digest);
            var fileDescriptor = descriptor.FileDescriptor
                ?? throw new IOException(
                    "The Android runtime archive descriptor is unavailable.");
            data.WriteFileDescriptor(fileDescriptor);
            if (!binder.Transact(
                    BridgeProtocol.InstallWinlatorRuntimePackageTransaction,
                    data,
                    reply,
                    0))
                throw new InvalidOperationException(
                    "Winlator rejected the runtime-package Binder transaction.");
            reply.ReadException();
            return reply.ReadString() ?? string.Empty;
        }
        finally
        {
            reply.Recycle();
            data.Recycle();
        }
    }

    private static bool TryParseSha256(string? value, out byte[] digest)
    {
        digest = Array.Empty<byte>();
        const string prefix = "sha256:";
        if (string.IsNullOrWhiteSpace(value) ||
            !value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;
        try
        {
            digest = Convert.FromHexString(value[prefix.Length..]);
            return digest.Length == 32;
        }
        catch (FormatException)
        {
            digest = Array.Empty<byte>();
            return false;
        }
    }

    private sealed class WinlatorRuntimeConnection :
        Java.Lang.Object,
        IServiceConnection,
        IDisposable
    {
        private readonly Context _context;
        private readonly TaskCompletionSource<IBinder> _connected =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _bound;

        public WinlatorRuntimeConnection(Context context) => _context = context;

        public async Task<IBinder> BindAsync(CancellationToken cancellationToken)
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
                throw new InvalidOperationException(
                    "Android refused the Winlator runtime-package bridge binding.");
            using var registration = cancellationToken.Register(
                () => _connected.TrySetCanceled(cancellationToken));
            return await _connected.Task
                .WaitAsync(TimeSpan.FromSeconds(30), cancellationToken)
                .ConfigureAwait(false);
        }

        public void OnServiceConnected(ComponentName? name, IBinder? service)
        {
            if (service == null)
                _connected.TrySetException(
                    new InvalidOperationException("Winlator returned a null Binder."));
            else
                _connected.TrySetResult(service);
        }

        public void OnServiceDisconnected(ComponentName? name)
        {
        }

        public void OnBindingDied(ComponentName? name) =>
            _connected.TrySetException(
                new InvalidOperationException("The Winlator runtime binding died."));

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
