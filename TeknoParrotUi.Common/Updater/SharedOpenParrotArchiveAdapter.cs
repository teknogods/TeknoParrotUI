using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace TeknoParrotUi.Common.Updater
{
    /// <summary>
    /// Converts the standard flat OpenParrot release ZIP into Winlator's
    /// manifest/payload installation envelope. The published archive remains
    /// unchanged and is shared by Windows, Linux/Wine, and Android.
    /// </summary>
    public static class SharedOpenParrotArchiveAdapter
    {
        private const int MaxFiles = 20000;
        private const long MaxDeclaredBytes = 20L * 1024 * 1024 * 1024;
        private static readonly DateTimeOffset StableTimestamp =
            new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public static byte[] CreateInstallEnvelope(
            string sourceArchivePath,
            string destinationArchivePath,
            string packageId,
            string version,
            CancellationToken cancellationToken = default)
        {
            var contract = ResolveContract(packageId);
            ValidateVersion(version);

            var sourcePath = Path.GetFullPath(sourceArchivePath);
            var destinationPath = Path.GetFullPath(destinationArchivePath);
            if (string.Equals(
                    sourcePath,
                    destinationPath,
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(
                    "The shared release archive and install envelope must be different files.");
            if (!File.Exists(sourcePath))
                throw new FileNotFoundException(
                    "The shared OpenParrot release archive is missing.",
                    sourcePath);

            var destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destinationDirectory))
                Directory.CreateDirectory(destinationDirectory);

            try
            {
                using var sourceStream = new FileStream(
                    sourcePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read);
                using var sourceArchive = new ZipArchive(
                    sourceStream,
                    ZipArchiveMode.Read,
                    leaveOpen: false);
                var files = InspectSourceArchive(
                    sourceArchive,
                    contract,
                    cancellationToken);

                using (var outputStream = new FileStream(
                           destinationPath,
                           FileMode.Create,
                           FileAccess.ReadWrite,
                           FileShare.None))
                using (var outputArchive = new ZipArchive(
                           outputStream,
                           ZipArchiveMode.Create,
                           leaveOpen: false))
                {
                    WriteManifest(
                        outputArchive,
                        packageId,
                        version,
                        files);
                    foreach (var file in files)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var outputEntry = outputArchive.CreateEntry(
                            "payload/" + contract.RuntimeRoot + "/" + file.Name,
                            CompressionLevel.Optimal);
                        outputEntry.LastWriteTime = StableTimestamp;
                        using var input = file.Entry.Open();
                        using var output = outputEntry.Open();
                        CopyExact(
                            input,
                            output,
                            file.Size,
                            cancellationToken);
                    }
                }

                using var envelope = File.OpenRead(destinationPath);
                return SHA256.HashData(envelope);
            }
            catch
            {
                try
                {
                    if (File.Exists(destinationPath))
                        File.Delete(destinationPath);
                }
                catch
                {
                    // Preserve the original exception. The destination is a
                    // private-cache artifact and will be replaced next run.
                }
                throw;
            }
        }

        private static IReadOnlyList<SourceFile> InspectSourceArchive(
            ZipArchive archive,
            PackageContract contract,
            CancellationToken cancellationToken)
        {
            if (archive.Entries.Count == 0 ||
                archive.Entries.Count > MaxFiles)
                throw new InvalidDataException(
                    "The shared OpenParrot archive has an invalid file count.");

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var files = new List<SourceFile>(archive.Entries.Count);
            long totalSize = 0;
            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ValidateFlatEntry(entry);
                if (!names.Add(entry.Name))
                    throw new InvalidDataException(
                        "The shared OpenParrot archive contains duplicate file '" +
                        entry.Name + "'.");
                if (entry.Length < 0 ||
                    entry.Length > MaxDeclaredBytes - totalSize)
                    throw new InvalidDataException(
                        "The shared OpenParrot archive has an invalid declared size.");
                totalSize += entry.Length;

                using var input = entry.Open();
                var digest = HashExact(
                    input,
                    entry.Length,
                    cancellationToken);
                files.Add(new SourceFile(
                    entry.Name,
                    entry.Length,
                    Convert.ToHexString(digest).ToLowerInvariant(),
                    entry));
            }

            foreach (var requiredFile in contract.RequiredFiles)
            {
                if (!names.Contains(requiredFile))
                    throw new InvalidDataException(
                        "The shared OpenParrot archive is missing required file '" +
                        requiredFile + "'.");
            }

            return files
                .OrderBy(file => file.Name, StringComparer.Ordinal)
                .ToArray();
        }

        private static void ValidateFlatEntry(ZipArchiveEntry entry)
        {
            var name = entry.FullName;
            var unixMode = (entry.ExternalAttributes >> 16) & 0xF000;
            if (string.IsNullOrWhiteSpace(name) ||
                string.IsNullOrEmpty(entry.Name) ||
                !string.Equals(name, entry.Name, StringComparison.Ordinal) ||
                name.Length > 255 ||
                name is "." or ".." ||
                name.Contains('/') ||
                name.Contains('\\') ||
                name.Contains('\0') ||
                name.Any(char.IsControl) ||
                unixMode == 0xA000)
                throw new InvalidDataException(
                    "The shared OpenParrot archive contains unsafe entry '" +
                    name + "'.");
        }

        private static byte[] HashExact(
            Stream input,
            long expectedSize,
            CancellationToken cancellationToken)
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[1024 * 1024];
            long readTotal = 0;
            int count;
            while ((count = input.Read(buffer, 0, buffer.Length)) != 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                readTotal += count;
                if (readTotal > expectedSize)
                    throw new InvalidDataException(
                        "A shared OpenParrot entry exceeded its declared size.");
                hash.AppendData(buffer, 0, count);
            }
            if (readTotal != expectedSize)
                throw new InvalidDataException(
                    "A shared OpenParrot entry did not match its declared size.");
            return hash.GetHashAndReset();
        }

        private static void CopyExact(
            Stream input,
            Stream output,
            long expectedSize,
            CancellationToken cancellationToken)
        {
            var buffer = new byte[1024 * 1024];
            long copied = 0;
            int count;
            while ((count = input.Read(buffer, 0, buffer.Length)) != 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                copied += count;
                if (copied > expectedSize)
                    throw new InvalidDataException(
                        "A shared OpenParrot entry exceeded its declared size.");
                output.Write(buffer, 0, count);
            }
            if (copied != expectedSize)
                throw new InvalidDataException(
                    "A shared OpenParrot entry did not match its declared size.");
        }

        private static void WriteManifest(
            ZipArchive archive,
            string packageId,
            string version,
            IReadOnlyList<SourceFile> files)
        {
            var manifest = new
            {
                schemaVersion = 1,
                packageId,
                platform = "android",
                version,
                files = files.Select(file => new
                {
                    path = ResolveContract(packageId).RuntimeRoot + "/" + file.Name,
                    size = file.Size,
                    sha256 = file.Sha256
                }).ToArray()
            };
            var manifestEntry = archive.CreateEntry(
                "teknoparrot-package.json",
                CompressionLevel.Optimal);
            manifestEntry.LastWriteTime = StableTimestamp;
            using var output = manifestEntry.Open();
            using var writer = new StreamWriter(
                output,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            writer.Write(JsonSerializer.Serialize(manifest));
        }

        private static PackageContract ResolveContract(string packageId) =>
            packageId switch
            {
                "OpenParrotWin32" => new PackageContract(
                    "OpenParrotWin32",
                    new[] { "OpenParrot.dll", "OpenParrotLoader.exe" }),
                "OpenParrotx64" => new PackageContract(
                    "OpenParrotWin64",
                    new[] { "OpenParrot64.dll", "OpenParrotLoader64.exe" }),
                _ => throw new InvalidDataException(
                    "Unsupported shared OpenParrot package id '" + packageId + "'.")
            };

        private static void ValidateVersion(string version)
        {
            if (string.IsNullOrWhiteSpace(version) ||
                version.Length > 128 ||
                version.Any(character =>
                    !(character is >= 'A' and <= 'Z') &&
                    !(character is >= 'a' and <= 'z') &&
                    !(character is >= '0' and <= '9') &&
                    character is not ('.' or '_' or '+' or '-')))
                throw new InvalidDataException(
                    "The OpenParrot runtime package version is invalid.");
        }

        private sealed record PackageContract(
            string RuntimeRoot,
            IReadOnlyList<string> RequiredFiles);

        private sealed record SourceFile(
            string Name,
            long Size,
            string Sha256,
            ZipArchiveEntry Entry);
    }
}
