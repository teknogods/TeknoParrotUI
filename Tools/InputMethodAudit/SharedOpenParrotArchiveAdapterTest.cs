using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TeknoParrotUi.Common.Updater;

namespace InputMethodAudit
{
    internal static class SharedOpenParrotArchiveAdapterTest
    {
        public static int Run()
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "tp-openparrot-adapter-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                TestPackage(
                    root,
                    "OpenParrotWin32",
                    "OpenParrotWin32",
                    new Dictionary<string, byte[]>
                    {
                        ["OpenParrot.dll"] = Encoding.ASCII.GetBytes("x86-core"),
                        ["OpenParrotLoader.exe"] = Encoding.ASCII.GetBytes("x86-loader"),
                        ["iDmacDrv32.dll"] = Encoding.ASCII.GetBytes("x86-idmac")
                    });
                TestPackage(
                    root,
                    "OpenParrotx64",
                    "OpenParrotWin64",
                    new Dictionary<string, byte[]>
                    {
                        ["OpenParrot64.dll"] = Encoding.ASCII.GetBytes("x64-core"),
                        ["OpenParrotLoader64.exe"] = Encoding.ASCII.GetBytes("x64-loader")
                    });

                RequireRejected(
                    root,
                    "missing-core",
                    new Dictionary<string, byte[]>
                    {
                        ["OpenParrotLoader.exe"] = Encoding.ASCII.GetBytes("loader")
                    },
                    "OpenParrotWin32",
                    "missing required core");
                RequireRejected(
                    root,
                    "nested-entry",
                    new Dictionary<string, byte[]>
                    {
                        ["OpenParrot.dll"] = Encoding.ASCII.GetBytes("core"),
                        ["OpenParrotLoader.exe"] = Encoding.ASCII.GetBytes("loader"),
                        ["nested/file.dll"] = Encoding.ASCII.GetBytes("nested")
                    },
                    "OpenParrotWin32",
                    "nested source entry");
                RequireRejected(
                    root,
                    "unsupported-package",
                    new Dictionary<string, byte[]>
                    {
                        ["OpenParrot.dll"] = Encoding.ASCII.GetBytes("core"),
                        ["OpenParrotLoader.exe"] = Encoding.ASCII.GetBytes("loader")
                    },
                    "TeknoParrot",
                    "unsupported package id");

                Console.WriteLine(
                    "Shared OpenParrot archive adapter: PASS " +
                    "(x86/x64 envelope and rejection contracts)");
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("Shared OpenParrot archive adapter: FAIL");
                Console.Error.WriteLine(error);
                return 1;
            }
            finally
            {
                try
                {
                    Directory.Delete(root, recursive: true);
                }
                catch
                {
                    // Best effort for a test-only temporary directory.
                }
            }
        }

        private static void TestPackage(
            string root,
            string packageId,
            string runtimeRoot,
            IReadOnlyDictionary<string, byte[]> files)
        {
            var source = Path.Combine(root, packageId + ".zip");
            var envelope = Path.Combine(root, packageId + ".install.zip");
            WriteArchive(source, files);

            var digest = SharedOpenParrotArchiveAdapter.CreateInstallEnvelope(
                source,
                envelope,
                packageId,
                "1.0.0.773");
            var actualDigest = SHA256.HashData(File.ReadAllBytes(envelope));
            if (!CryptographicOperations.FixedTimeEquals(digest, actualDigest))
                throw new InvalidOperationException(
                    packageId + " returned the wrong envelope digest.");

            using var archive = ZipFile.OpenRead(envelope);
            var manifestEntry = archive.GetEntry("teknoparrot-package.json")
                ?? throw new InvalidOperationException(
                    packageId + " envelope has no manifest.");
            using var manifestStream = manifestEntry.Open();
            using var manifest = JsonDocument.Parse(manifestStream);
            var rootElement = manifest.RootElement;
            if (rootElement.GetProperty("schemaVersion").GetInt32() != 1 ||
                rootElement.GetProperty("packageId").GetString() != packageId ||
                rootElement.GetProperty("platform").GetString() != "android" ||
                rootElement.GetProperty("version").GetString() != "1.0.0.773")
                throw new InvalidOperationException(
                    packageId + " envelope has the wrong manifest identity.");

            var listedFiles = rootElement.GetProperty("files")
                .EnumerateArray()
                .ToDictionary(
                    file => file.GetProperty("path").GetString()!,
                    file => file,
                    StringComparer.Ordinal);
            if (listedFiles.Count != files.Count)
                throw new InvalidOperationException(
                    packageId + " manifest has the wrong file count.");

            foreach (var sourceFile in files)
            {
                var relativePath = runtimeRoot + "/" + sourceFile.Key;
                if (!listedFiles.TryGetValue(relativePath, out var listed))
                    throw new InvalidOperationException(
                        packageId + " manifest omitted " + relativePath + ".");
                if (listed.GetProperty("size").GetInt64() != sourceFile.Value.LongLength ||
                    listed.GetProperty("sha256").GetString() !=
                    Convert.ToHexString(SHA256.HashData(sourceFile.Value))
                        .ToLowerInvariant())
                    throw new InvalidOperationException(
                        packageId + " manifest metadata is wrong for " +
                        relativePath + ".");
                var payload = archive.GetEntry("payload/" + relativePath)
                    ?? throw new InvalidOperationException(
                        packageId + " envelope omitted " + relativePath + ".");
                using var payloadStream = payload.Open();
                using var copy = new MemoryStream();
                payloadStream.CopyTo(copy);
                if (!copy.ToArray().SequenceEqual(sourceFile.Value))
                    throw new InvalidOperationException(
                        packageId + " payload changed " + relativePath + ".");
            }
        }

        private static void RequireRejected(
            string root,
            string name,
            IReadOnlyDictionary<string, byte[]> files,
            string packageId,
            string scenario)
        {
            var source = Path.Combine(root, name + ".zip");
            var envelope = Path.Combine(root, name + ".install.zip");
            WriteArchive(source, files);
            try
            {
                SharedOpenParrotArchiveAdapter.CreateInstallEnvelope(
                    source,
                    envelope,
                    packageId,
                    "1.0.0.773");
            }
            catch (Exception)
            {
                if (File.Exists(envelope))
                    throw new InvalidOperationException(
                        "Rejected " + scenario + " left an install envelope.");
                return;
            }
            throw new InvalidOperationException(
                "The adapter accepted " + scenario + ".");
        }

        private static void WriteArchive(
            string path,
            IReadOnlyDictionary<string, byte[]> files)
        {
            using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
            foreach (var file in files)
            {
                var entry = archive.CreateEntry(file.Key);
                using var output = entry.Open();
                output.Write(file.Value, 0, file.Value.Length);
            }
        }
    }
}
