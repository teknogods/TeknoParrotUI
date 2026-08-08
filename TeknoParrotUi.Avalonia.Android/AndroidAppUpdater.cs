using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Android.Content;
using Android.Content.PM;
using Android.Provider;
using AndroidX.Core.Content;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.Updater;
using AndroidUri = Android.Net.Uri;

namespace TeknoParrotUi.Avalonia.Android;

/// <summary>
/// Downloads a release APK into private cache, verifies its package identity
/// and signing certificate against the installed TeknoParrot UI, then opens
/// Android's confirmation-based package installer.
/// </summary>
internal sealed class AndroidAppUpdater
{
    // Keep Android package versioning independent from the desktop rolling
    // release. Desktop pushes can advance their release name without rebuilding
    // these APKs, which would otherwise produce a permanent false update.
    private const string ReleaseTag = "TeknoParrotUI-android";
    private const string CompanionPackage = "com.teknoparrot.winlator";
    private const string Pcsx2x6Package = "com.teknogods.tekno2x6";
    private const string DolphinPackage = "com.teknogods.teknodolphin";
    private const string Rpcs3x6Package = "com.teknogods.rpcs3x6";
    private readonly Context _context;
    private readonly AndroidRuntimePackageUpdater _runtimeUpdater;

    public AndroidAppUpdater(Context context)
    {
        _context = context.ApplicationContext
            ?? throw new ArgumentException("Android application context is unavailable.");
        _runtimeUpdater = new AndroidRuntimePackageUpdater(_context);
    }

    public IReadOnlyList<UpdaterComponent> BuildComponents()
    {
        return new[]
        {
            CreateApkComponent(
                "TeknoParrotUI",
                _context.PackageName!,
                ReleaseTag,
                "TeknoParrotUi-"),
            CreateApkComponent(
                "TeknoParrot Winlator",
                CompanionPackage,
                "winlator",
                "TeknoParrotWinlator-",
                "winlator",
                "ReaverTeknoGods"),
            CreateApkComponent(
                "Tekno2x6",
                Pcsx2x6Package,
                "pcsx2x6-android",
                "pcsx2x6-",
                "pcsx2x6",
                "ReaverTeknoGods"),
            CreateApkComponent(
                "TeknoDolphin",
                DolphinPackage,
                "teknodolphin-android",
                "teknodolphin-",
                "CrediarDolphin",
                "ReaverTeknoGods"),
            CreateApkComponent(
                "RPCS3X6",
                Rpcs3x6Package,
                "rpcs3x6-android",
                "rpcs3x6-",
                "rpcs3",
                "ReaverTeknoGods"),
            CreateRuntimeComponent("OpenParrotWin32"),
            CreateRuntimeComponent("OpenParrotx64"),
            CreateRuntimeComponent(
                "cxbxr",
                assetNamePrefix: "cxbxr_",
                assetNameMarker: "-android.zip",
                archiveIsInstallEnvelope: true)
        };
    }

    public async Task<string> DownloadAndLaunchInstallerAsync(
        UpdateCheckResult update,
        IProgress<double> progress,
        CancellationToken cancellationToken)
    {
        if (update.Component.deliveryKind ==
            UpdaterDeliveryKind.AndroidRuntimeArchive)
            return await _runtimeUpdater.DownloadAndInstallAsync(
                update,
                progress,
                cancellationToken);

        if (!_context.PackageManager!.CanRequestPackageInstalls())
        {
            var permission = new Intent(
                Settings.ActionManageUnknownAppSources,
                AndroidUri.Parse("package:" + _context.PackageName));
            permission.AddFlags(ActivityFlags.NewTask);
            _context.StartActivity(permission);
            return "Allow TeknoParrot to install updates, then press Update again.";
        }

        var asset = UpdaterCore.PickAssetForPlatform(
            update.Component,
            update.Release,
            "android")
            ?? throw new InvalidOperationException(
                $"No ARM64 Android APK was published for {update.Component.name}.");
        if (!Uri.TryCreate(asset.browser_download_url, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidDataException("The Android update URL is not HTTPS.");
        if (asset.size <= 0)
            throw new InvalidDataException(
                "The Android update has no authoritative package size.");
        byte[]? expectedDigest = null;
        if (!string.IsNullOrWhiteSpace(asset.digest))
        {
            if (!TryParseSha256(asset.digest, out var parsedDigest))
                throw new InvalidDataException(
                    "The Android update has an invalid SHA256 digest.");
            expectedDigest = parsedDigest;
        }

        var updateDir = Path.Combine(_context.CacheDir!.AbsolutePath, "updates");
        Directory.CreateDirectory(updateDir);
        var apkPath = Path.Combine(
            updateDir,
            update.Component.packageIdentity.Replace('.', '-') + "-update.apk");

        using (var client = new HttpClient { Timeout = TimeSpan.FromMinutes(15) })
        using (var response = await client.GetAsync(
                   uri,
                   HttpCompletionOption.ResponseHeadersRead,
                   cancellationToken))
        {
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is long contentLength &&
                contentLength != asset.size)
                throw new InvalidDataException(
                    $"Android package length is {contentLength}, expected {asset.size}.");
            var total = asset.size;
            await using var input = await response.Content.ReadAsStreamAsync(
                cancellationToken);
            await using var output = new FileStream(
                apkPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                1024 * 1024,
                useAsync: true);
            using var hash =
                IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[1024 * 1024];
            long received = 0;
            int count;
            while ((count = await input.ReadAsync(
                       buffer.AsMemory(0, buffer.Length),
                       cancellationToken)) != 0)
            {
                await output.WriteAsync(
                    buffer.AsMemory(0, count),
                    cancellationToken);
                received += count;
                if (received > total)
                    throw new InvalidDataException(
                        "Android package exceeded its declared size.");
                hash.AppendData(buffer, 0, count);
                progress.Report(received * 90d / total);
            }
            if (received != total)
                throw new InvalidDataException(
                    $"Android package length is {received}, expected {total}.");
            await output.FlushAsync(cancellationToken);
            var actualDigest = hash.GetHashAndReset();
            if (expectedDigest != null &&
                !CryptographicOperations.FixedTimeEquals(
                    expectedDigest,
                    actualDigest))
                throw new InvalidDataException(
                    "The downloaded Android package failed SHA256 verification.");
        }

        var candidate = VerifyPackage(apkPath, update.Component.name);
        VerifyInstallCompatibility(candidate, update.Component.name);
        progress.Report(95);

        var apkUri = FileProvider.GetUriForFile(
            _context,
            _context.PackageName + ".updates",
            new Java.IO.File(apkPath));
        var install = new Intent(Intent.ActionView);
        install.SetDataAndType(
            apkUri,
            "application/vnd.android.package-archive");
        install.AddFlags(
            ActivityFlags.NewTask |
            ActivityFlags.GrantReadUriPermission);
        _context.StartActivity(install);
        progress.Report(100);
        return $"Android installer opened for {update.Component.name}. Confirm the update, then return to TeknoParrot.";
    }

    public async Task RefreshComponentsAsync(
        IReadOnlyList<UpdaterComponent> components,
        CancellationToken cancellationToken)
    {
        var companionVersion = ReadInstalledVersion(CompanionPackage);
        IReadOnlyDictionary<string, string> runtimeVersions =
            new Dictionary<string, string>(StringComparer.Ordinal);
        if (companionVersion != UpdaterComponent.NotInstalled)
        {
            try
            {
                runtimeVersions = await _runtimeUpdater
                    .QueryInstalledVersionsAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // Compatibility with the previous companion while the paired
                // APK update is still waiting for Android installer approval.
                runtimeVersions = components
                    .Where(component => component.deliveryKind ==
                        UpdaterDeliveryKind.AndroidRuntimeArchive)
                    .ToDictionary(
                        component => component.runtimePackageId,
                        component => _runtimeUpdater.ReadInstalledVersion(
                            component.runtimePackageId),
                        StringComparer.Ordinal);
            }
        }
        foreach (var component in components)
        {
            if (component.deliveryKind == UpdaterDeliveryKind.AndroidApk)
                component._localVersion = ReadInstalledVersion(component.packageIdentity);
            else if (component.deliveryKind ==
                     UpdaterDeliveryKind.AndroidRuntimeArchive)
                component._localVersion = runtimeVersions.TryGetValue(
                    component.runtimePackageId,
                    out var version)
                    ? version
                    : UpdaterComponent.NotInstalled;
        }
    }

    private UpdaterComponent CreateApkComponent(
        string name,
        string packageName,
        string releaseTag,
        string? assetPrefix,
        string repositoryName = "TeknoParrotUI",
        string repositoryOwner = "teknogods")
    {
        return new UpdaterComponent
        {
            name = name,
            location = "package:" + packageName,
            reponame = repositoryName,
            userName = repositoryOwner,
            releaseTag = releaseTag,
            isManagedAssembly = true,
            assetNamePrefix = assetPrefix,
            assetNameMarker = "-android-arm64.apk",
            deliveryKind = UpdaterDeliveryKind.AndroidApk,
            packageIdentity = packageName,
            _localVersion = ReadInstalledVersion(packageName)
        };
    }

    private UpdaterComponent CreateRuntimeComponent(
        string packageId,
        string? assetNamePrefix = null,
        string? assetNameMarker = null,
        bool archiveIsInstallEnvelope = false)
    {
        return new UpdaterComponent
        {
            name = packageId,
            reponame = archiveIsInstallEnvelope ? "TeknoParrot" : "OpenParrot",
            userName = "teknogods",
            releaseTag = packageId,
            // This is the same flat OpenParrot archive consumed by Windows and
            // Linux. TPUI creates Winlator's installation envelope locally
            // after verifying the authoritative release digest.
            assetNameExact = archiveIsInstallEnvelope
                ? null
                : packageId + ".zip",
            assetNamePrefix = assetNamePrefix,
            assetNameMarker = assetNameMarker,
            deliveryKind = UpdaterDeliveryKind.AndroidRuntimeArchive,
            runtimePackageId = packageId,
            runtimeArchiveIsInstallEnvelope = archiveIsInstallEnvelope,
            _localVersion = _runtimeUpdater.ReadInstalledVersion(packageId)
        };
    }

    private string ReadInstalledVersion(string packageName)
    {
        try
        {
            return _context.PackageManager!
                       .GetPackageInfo(packageName, PackageInfoFlags.MetaData)
                       ?.VersionName
                   ?? "unknown";
        }
        catch (PackageManager.NameNotFoundException)
        {
            return UpdaterComponent.NotInstalled;
        }
    }

    private PackageInfo VerifyPackage(string apkPath, string componentName)
    {
        var manager = _context.PackageManager
            ?? throw new InvalidOperationException("Android package manager is unavailable.");
        var signatureFlags = OperatingSystem.IsAndroidVersionAtLeast(28)
            ? PackageInfoFlags.SigningCertificates
#pragma warning disable CS0618 // Android API 26-27 compatibility.
            : PackageInfoFlags.Signatures;
#pragma warning restore CS0618
        var candidate = manager.GetPackageArchiveInfo(
            apkPath,
            signatureFlags)
            ?? throw new InvalidDataException("The downloaded file is not a valid APK.");
        var expectedPackage = BuildComponents()
            .FirstOrDefault(component =>
                string.Equals(component.name, componentName, StringComparison.Ordinal))
            ?.packageIdentity
            ?? throw new InvalidDataException(
                $"No Android package identity is registered for {componentName}.");
        if (!string.Equals(
                candidate.PackageName,
                expectedPackage,
                StringComparison.Ordinal))
            throw new InvalidDataException(
                $"The downloaded APK has package '{candidate.PackageName}', expected '{expectedPackage}'.");

        var installedUi = manager.GetPackageInfo(
            _context.PackageName!,
            signatureFlags)
            ?? throw new InvalidOperationException(
                "Could not inspect the installed TeknoParrot signature.");
        var expectedDigests = ReadSignerDigests(installedUi);
        var candidateDigests = ReadSignerDigests(candidate);
        if (expectedDigests.Count == 0 ||
            !expectedDigests.SequenceEqual(
                candidateDigests,
                StringComparer.Ordinal))
            throw new InvalidDataException(
                "The downloaded APK is not signed by the installed TeknoParrot publisher.");
        return candidate;
    }

    private void VerifyInstallCompatibility(
        PackageInfo candidate,
        string componentName)
    {
        var manager = _context.PackageManager
            ?? throw new InvalidOperationException(
                "Android package manager is unavailable.");
        var packageName = candidate.PackageName
            ?? throw new InvalidDataException(
                "The downloaded APK has no package identity.");
        var signatureFlags = OperatingSystem.IsAndroidVersionAtLeast(28)
            ? PackageInfoFlags.SigningCertificates
#pragma warning disable CS0618 // Android API 26-27 compatibility.
            : PackageInfoFlags.Signatures;
#pragma warning restore CS0618

        PackageInfo? existing = null;
        var isInstalled = true;
        try
        {
            existing = manager.GetPackageInfo(packageName, signatureFlags);
        }
        catch (PackageManager.NameNotFoundException)
        {
            isInstalled = false;
            try
            {
                // PackageManager.MATCH_UNINSTALLED_PACKAGES. Android retains
                // version/signature metadata when an app is removed while
                // keeping its private data.
                const PackageInfoFlags matchUninstalledPackages =
                    (PackageInfoFlags)0x00002000;
                existing = manager.GetPackageInfo(
                    packageName,
                    signatureFlags | matchUninstalledPackages);
            }
            catch (PackageManager.NameNotFoundException)
            {
                return;
            }
        }

        if (existing == null)
            return;

        var existingDigests = ReadSignerDigests(existing);
        var candidateDigests = ReadSignerDigests(candidate);
        if (existingDigests.Count != 0 &&
            !existingDigests.SequenceEqual(
                candidateDigests,
                StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                isInstalled
                    ? $"The installed {componentName} uses a different signing key. " +
                      "Uninstall that developer build completely before installing " +
                      "the official update."
                    : $"Android still has private data from an older developer build " +
                      $"of {componentName} signed with a different key. Fully remove " +
                      "that retained app data before installing the official update.");
        }

        var existingVersionCode = ReadVersionCode(existing);
        var candidateVersionCode = ReadVersionCode(candidate);
        if (candidateVersionCode < existingVersionCode)
        {
            throw new InvalidOperationException(
                $"Android remembers {componentName} version code " +
                $"{existingVersionCode}, but this release is " +
                $"{candidateVersionCode}. A newer companion release is required.");
        }
    }

    private static long ReadVersionCode(PackageInfo package) =>
        OperatingSystem.IsAndroidVersionAtLeast(28)
            ? package.LongVersionCode
#pragma warning disable CS0618 // Android API 26-27 compatibility.
            : package.VersionCode;
#pragma warning restore CS0618

    private static IReadOnlyList<string> ReadSignerDigests(PackageInfo package)
    {
        var signatures = OperatingSystem.IsAndroidVersionAtLeast(28)
            ? package.SigningInfo?.GetApkContentsSigners()
#pragma warning disable CS0618 // Android API 26-27 compatibility.
            : package.Signatures;
#pragma warning restore CS0618
        if (signatures == null)
            return Array.Empty<string>();
        return signatures
            .Select(signature => signature?.ToByteArray())
            .Where(bytes => bytes is { Length: > 0 })
            .Select(bytes =>
                Convert.ToHexString(
                    SHA256.HashData(bytes!)))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool TryParseSha256(string value, out byte[] digest)
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
}
