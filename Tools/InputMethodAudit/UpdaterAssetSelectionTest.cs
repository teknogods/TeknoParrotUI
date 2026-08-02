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
            var androidDolphin = new GithubAsset
            {
                browser_download_url =
                    "https://example.invalid/teknodolphin-1.0.0.12345678-android-arm64.apk"
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
            var unrelatedRuntime = new GithubAsset
            {
                name = "UnrelatedRuntime_1.0.0.1.zip",
                browser_download_url =
                    "https://example.invalid/UnrelatedRuntime_1.0.0.1.zip"
            };
            var sharedTeknoParrot = new GithubAsset
            {
                name = "TeknoParrotCore_1.0.0.3723.zip",
                browser_download_url =
                    "https://example.invalid/TeknoParrotCore_1.0.0.3723.zip"
            };
            var sharedElfLdr2 = new GithubAsset
            {
                name = "TeknoParrotElfLdr2Core_1.0.0.1407.zip",
                browser_download_url =
                    "https://example.invalid/TeknoParrotElfLdr2Core_1.0.0.1407.zip"
            };
            var sharedCxbxr = new GithubAsset
            {
                name = "cxbxr_1.0.0.17.zip",
                browser_download_url =
                    "https://example.invalid/cxbxr_1.0.0.17.zip"
            };
            var sharedDesktopPcsx2x6 = new GithubAsset
            {
                name = "pcsx2x6_1.0.0.11.zip",
                browser_download_url =
                    "https://example.invalid/pcsx2x6_1.0.0.11.zip"
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
                androidDolphin,
                UpdaterCore.PickAssetForPlatform(
                    new UpdaterComponent
                    {
                        name = "TeknoDolphin",
                        assetNamePrefix = "teknodolphin-",
                        assetNameMarker = "-android-arm64.apk"
                    },
                    new GithubRelease
                    {
                        assets = new List<GithubAsset>
                        {
                            androidPcsx2x6,
                            androidDolphin
                        }
                    },
                    "android-arm64"),
                "Android TeknoDolphin module selection",
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
            if (UpdaterCore.CompareVersions("0.0.116.35", "0.0.117.1") >= 0)
                failures.Add(
                    "RPCS3X6 build rollover was compared by revision only.");
            if (UpdaterCore.CompareVersions(
                    "1.0.0.85633782",
                    "1.0.0.85633783") >= 0)
                failures.Add(
                    "Large Android companion revision was not update-comparable.");

            var rpcs3x6 = new UpdaterComponent
            {
                name = "RPCS3X6",
                assetNamePrefix = "rpcs3x6-",
                assetNameMarker = "-android-arm64.apk",
                deliveryKind = UpdaterDeliveryKind.AndroidApk
            };
            var rpcs3x6Version = UpdaterCore.GetReleaseVersion(
                rpcs3x6,
                new GithubRelease
                {
                    name = "RPCS3X6 Android 0.0.116.35",
                    assets = new List<GithubAsset>
                    {
                        new GithubAsset
                        {
                            name = "rpcs3x6-0.0.116.35-android-arm64.apk"
                        }
                    }
                });
            if (rpcs3x6Version != "0.0.116.35")
                failures.Add(
                    "RPCS3X6 Android release title was not resolved from its APK filename.");

            var androidUiVersion = UpdaterCore.GetReleaseVersion(
                new UpdaterComponent
                {
                    name = "TeknoParrotUI",
                    assetNamePrefix = "TeknoParrotUi-",
                    assetNameMarker = "-android-arm64.apk",
                    deliveryKind = UpdaterDeliveryKind.AndroidApk
                },
                new GithubRelease
                {
                    name = "2.0.0.10113",
                    assets = new List<GithubAsset>
                    {
                        new GithubAsset
                        {
                            name = "TeknoParrotUi-2.0.0.20113-android-arm64.apk"
                        }
                    }
                });
            if (androidUiVersion != "2.0.0.20113")
                failures.Add(
                    "Android UI version did not follow the selected APK filename.");

            var legacyRuntimeVersion = UpdaterCore.GetReleaseVersion(
                new UpdaterComponent { name = "OpenParrotx64" },
                new GithubRelease { name = "OpenParrotx64_1.0.0.783" });
            if (legacyRuntimeVersion != "1.0.0.783")
                failures.Add("Legacy underscore-prefixed release version changed.");

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
            foreach (var sharedRuntime in new[]
                     {
                         (Component: "TeknoParrot", Asset: sharedTeknoParrot),
                         (Component: "TeknoParrotElfLdr2", Asset: sharedElfLdr2),
                         (Component: "cxbxr", Asset: sharedCxbxr),
                         (Component: "pcsx2x6", Asset: sharedDesktopPcsx2x6)
                     })
            {
                var defaultRuntime = defaultComponents.Find(candidate =>
                    candidate.name == sharedRuntime.Component);
                foreach (var platform in new[] { "linux", "win" })
                {
                    ExpectSame(
                        sharedRuntime.Asset,
                        UpdaterCore.PickAssetForPlatform(
                            defaultRuntime,
                            new GithubRelease
                            {
                                assets = new List<GithubAsset>
                                {
                                    unrelatedRuntime,
                                    sharedRuntime.Asset
                                }
                            },
                            platform),
                        platform + " shared " + sharedRuntime.Component +
                        " archive selection",
                        failures);
                }
            }
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
                    "(platform packages and shared runtime archives)");
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
