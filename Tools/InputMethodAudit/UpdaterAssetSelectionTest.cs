using System;
using System.Collections.Generic;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.Updater;

namespace InputMethodAudit
{
    internal static class UpdaterAssetSelectionTest
    {
        public static int Run()
        {
            var failures = new List<string>();
            var windows = new GithubAsset
            {
                browser_download_url =
                    "https://example.invalid/TeknoParrotUi-1.2.3.4-win-x64.zip"
            };
            var linux = new GithubAsset
            {
                browser_download_url =
                    "https://example.invalid/TeknoParrotUi-1.2.3.4-linux-x64.zip"
            };
            var ui = new UpdaterComponent { name = "TeknoParrotUI" };
            var component = new UpdaterComponent { name = "OpenParrotWin32" };
            var androidUi = new GithubAsset
            {
                browser_download_url =
                    "https://example.invalid/TeknoParrotUi-1.2.3.4-android-arm64.apk"
            };
            var androidCompanion = new GithubAsset
            {
                browser_download_url =
                    "https://example.invalid/TeknoParrotWinlator-1.2.3.4-android-arm64.apk"
            };
            var androidPcsx2x6 = new GithubAsset
            {
                browser_download_url =
                    "https://example.invalid/pcsx2x6-2.6.1.12345678-android-arm64.apk"
            };
            var androidSpecificOpenParrot = new GithubAsset
            {
                name = "OpenParrotWin32-1.0.0.773-android.zip",
                browser_download_url =
                    "https://example.invalid/OpenParrotWin32-1.0.0.773-android.zip"
            };
            var sharedOpenParrot = new GithubAsset
            {
                name = "OpenParrotWin32.zip",
                browser_download_url =
                    "https://example.invalid/OpenParrotWin32.zip"
            };
            var windowsPcsx2x6 = new GithubAsset
            {
                name = "pcsx2x6-2.6.1.12345678-windows.zip",
                browser_download_url =
                    "https://example.invalid/pcsx2x6-2.6.1.12345678-windows.zip"
            };
            var linuxPcsx2x6 = new GithubAsset
            {
                name = "pcsx2x6-2.6.1.12345678-linux.zip",
                browser_download_url =
                    "https://example.invalid/pcsx2x6-2.6.1.12345678-linux.zip"
            };

            ExpectSame(
                linux,
                UpdaterCore.PickAssetForPlatform(
                    ui,
                    new GithubRelease { assets = new List<GithubAsset> { windows, linux } },
                    "linux"),
                "Linux UI selection",
                failures);
            ExpectSame(
                windows,
                UpdaterCore.PickAssetForPlatform(
                    ui,
                    new GithubRelease { assets = new List<GithubAsset> { linux, windows } },
                    "win"),
                "Windows UI selection",
                failures);

            var wrongOnly = UpdaterCore.PickAssetForPlatform(
                ui,
                new GithubRelease { assets = new List<GithubAsset> { windows } },
                "linux");
            if (wrongOnly != null)
                failures.Add("Linux UI selection accepted the Windows-only asset.");

            ExpectSame(
                windows,
                UpdaterCore.PickAssetForPlatform(
                    component,
                    new GithubRelease { assets = new List<GithubAsset> { windows, linux } },
                    "linux"),
                "Single-platform component compatibility",
                failures);

            var empty = UpdaterCore.PickAssetForPlatform(
                ui,
                new GithubRelease { assets = new List<GithubAsset>() },
                "linux");
            if (empty != null)
                failures.Add("Empty release unexpectedly selected an asset.");

            ExpectSame(
                androidUi,
                UpdaterCore.PickAssetForPlatform(
                    new UpdaterComponent
                    {
                        name = "TeknoParrotUI",
                        assetNamePrefix = "TeknoParrotUi-",
                        assetNameMarker = "-android-arm64.apk"
                    },
                    new GithubRelease
                    {
                        assets = new List<GithubAsset>
                        {
                            windows,
                            androidCompanion,
                            androidUi
                        }
                    },
                    "android-arm64"),
                "Android UI package selection",
                failures);
            ExpectSame(
                androidCompanion,
                UpdaterCore.PickAssetForPlatform(
                    new UpdaterComponent
                    {
                        name = "TeknoParrot Winlator",
                        assetNamePrefix = "TeknoParrotWinlator-",
                        assetNameMarker = "-android-arm64.apk"
                    },
                    new GithubRelease
                    {
                        assets = new List<GithubAsset>
                        {
                            androidUi,
                            androidCompanion
                        }
                    },
                    "android-arm64"),
                "Android companion package selection",
                failures);
            ExpectSame(
                androidPcsx2x6,
                UpdaterCore.PickAssetForPlatform(
                    new UpdaterComponent
                    {
                        name = "pcsx2x6",
                        assetNamePrefix = "pcsx2x6-",
                        assetNameMarker = "-android-arm64.apk"
                    },
                    new GithubRelease
                    {
                        assets = new List<GithubAsset>
                        {
                            androidUi,
                            androidPcsx2x6
                        }
                    },
                    "android-arm64"),
                "Android PCSX2X6 module selection",
                failures);
            ExpectSame(
                windowsPcsx2x6,
                UpdaterCore.PickAssetForPlatform(
                    new UpdaterComponent
                    {
                        name = "pcsx2x6",
                        assetNamePrefix = "pcsx2x6-",
                        assetNameMarker = "-windows.zip"
                    },
                    new GithubRelease
                    {
                        assets = new List<GithubAsset>
                        {
                            linuxPcsx2x6,
                            windowsPcsx2x6
                        }
                    },
                    "win"),
                "Windows PCSX2X6 archive selection",
                failures);
            ExpectSame(
                linuxPcsx2x6,
                UpdaterCore.PickAssetForPlatform(
                    new UpdaterComponent
                    {
                        name = "pcsx2x6",
                        assetNamePrefix = "pcsx2x6-",
                        assetNameMarker = "-linux.zip"
                    },
                    new GithubRelease
                    {
                        assets = new List<GithubAsset>
                        {
                            windowsPcsx2x6,
                            linuxPcsx2x6
                        }
                    },
                    "linux"),
                "Linux PCSX2X6 archive selection",
                failures);
            if (UpdaterCore.GetVersionNumber("2.6.1.12345678") != 12345678)
                failures.Add("Numeric PCSX2X6 module version was not update-comparable.");

            var androidRuntime = new UpdaterComponent
            {
                name = "OpenParrotWin32",
                assetNameExact = "OpenParrotWin32.zip"
            };
            ExpectSame(
                sharedOpenParrot,
                UpdaterCore.PickAssetForPlatform(
                    androidRuntime,
                    new GithubRelease
                    {
                        assets = new List<GithubAsset>
                        {
                            androidSpecificOpenParrot,
                            sharedOpenParrot
                        }
                    },
                    "android"),
                "Shared OpenParrot runtime archive selection",
                failures);
            if (UpdaterCore.PickAssetForPlatform(
                    androidRuntime,
                    new GithubRelease
                    {
                        assets = new List<GithubAsset> { androidSpecificOpenParrot }
                    },
                    "android") != null)
                failures.Add(
                    "Shared runtime selection accepted an Android-specific OpenParrot ZIP.");

            var defaultComponents = UpdaterComponent.BuildDefaultComponents("TeknoParrotUi.dll");
            foreach (var packageId in new[] { "OpenParrotWin32", "OpenParrotx64" })
            {
                var defaultComponent = defaultComponents.Find(candidate =>
                    candidate.name == packageId);
                if (defaultComponent?.assetNameExact != packageId + ".zip")
                    failures.Add(
                        packageId + " desktop updater does not use the shared release ZIP.");
            }

            if (failures.Count == 0)
            {
                Console.WriteLine(
                    "Updater asset selection: PASS " +
                    "(desktop/Android shared OpenParrot contract)");
                return 0;
            }

            foreach (var failure in failures)
                Console.Error.WriteLine(failure);
            Console.Error.WriteLine($"Updater asset selection: FAIL ({failures.Count} failure(s))");
            return 1;
        }

        private static void ExpectSame(
            GithubAsset expected,
            GithubAsset actual,
            string scenario,
            ICollection<string> failures)
        {
            if (!ReferenceEquals(expected, actual))
                failures.Add($"{scenario} selected the wrong asset.");
        }
    }
}
