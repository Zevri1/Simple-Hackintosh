using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleHackintosh.App;

internal sealed record OpenCoreReleaseInfo(string Version, Uri DownloadUri, string Sha256, long ExpectedBytes, string AssetName);
internal sealed record OpenCoreBuildResult(string Version, string EfiPath, string Sha256);
internal sealed record OpenCoreProgress(string Stage, long Received, long? Expected);

internal static class OpenCoreReleaseService
{
    private static readonly Uri LatestReleaseApi = new("https://api.github.com/repos/acidanthera/OpenCorePkg/releases/latest");
    private static readonly HttpClient Client = new(new HttpClientHandler
    {
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
    }) { Timeout = Timeout.InfiniteTimeSpan };

    public static async Task<OpenCoreReleaseInfo> GetLatestReleaseAsync(CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(2));
        using var response = await SendFollowingAllowedRedirectsAsync(LatestReleaseApi, IsGitHubHost, timeout.Token);
        response.EnsureSuccessStatusCode();
        await using var payload = await response.Content.ReadAsStreamAsync(timeout.Token);
        using var document = await JsonDocument.ParseAsync(payload, cancellationToken: timeout.Token);
        var root = document.RootElement;
        if (root.TryGetProperty("draft", out var draft) && draft.GetBoolean())
            throw new InvalidDataException("GitHub returned a draft OpenCore release.");
        if (root.TryGetProperty("prerelease", out var prerelease) && prerelease.GetBoolean())
            throw new InvalidDataException("GitHub returned a prerelease instead of a stable OpenCore release.");

        string tag = RequiredString(root, "tag_name");
        if (tag.Length > 40 || tag.Contains('/') || tag.Contains('\\') || tag.Contains("..", StringComparison.Ordinal))
            throw new InvalidDataException("The OpenCore release tag is not a safe version identifier.");
        if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("The official release did not contain an asset list.");

        foreach (var asset in assets.EnumerateArray())
        {
            string name = RequiredString(asset, "name");
            if (!string.Equals(name, $"OpenCore-{tag}-RELEASE.zip", StringComparison.Ordinal)) continue;
            string digest = RequiredString(asset, "digest");
            if (!digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) || digest.Length != 71)
                throw new InvalidDataException("GitHub did not publish a valid SHA-256 digest for this release asset.");
            string hash = digest["sha256:".Length..].ToLowerInvariant();
            if (!IsHexSha256(hash)) throw new InvalidDataException("The published release digest is malformed.");

            if (!Uri.TryCreate(RequiredString(asset, "browser_download_url"), UriKind.Absolute, out var downloadUri) ||
                downloadUri.Host != "github.com" ||
                downloadUri.AbsolutePath != $"/acidanthera/OpenCorePkg/releases/download/{tag}/{name}")
                throw new InvalidDataException("The release asset URL does not match the official Acidanthera OpenCorePkg release path.");

            if (!asset.TryGetProperty("size", out var sizeElement) || !sizeElement.TryGetInt64(out long size) ||
                size <= 0 || size > 100L * 1024 * 1024)
                throw new InvalidDataException("The OpenCore release asset has an unexpected size.");

            return new OpenCoreReleaseInfo(tag, downloadUri, hash, size, name);
        }

        throw new InvalidDataException("The latest stable OpenCore release has no x64 RELEASE ZIP asset with a published digest.");
    }

    public static async Task<OpenCoreBuildResult> BuildStarterAsync(string destination,
        IProgress<OpenCoreProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(destination) || !Directory.Exists(destination))
            throw new DirectoryNotFoundException("Choose an existing destination folder.");
        string targetEfi = Path.Combine(destination, "EFI");
        if (Directory.Exists(targetEfi) || File.Exists(targetEfi))
            throw new IOException("An EFI item already exists at the destination. Choose another folder; existing files are never overwritten.");

        string staging = Path.Combine(destination, ".simple-hackintosh-opencore-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            progress?.Report(new OpenCoreProgress("Reading the official OpenCore release…", 0, null));
            var release = await GetLatestReleaseAsync(cancellationToken);
            string archive = Path.Combine(staging, release.AssetName + ".partial");
            string actualHash = await DownloadAndHashAsync(release, archive, progress, cancellationToken);
            if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(actualHash), Convert.FromHexString(release.Sha256)))
                throw new InvalidDataException("OpenCore SHA-256 verification failed. No EFI folder was created.");

            progress?.Report(new OpenCoreProgress("Verifying archive and preparing the EFI starter…", release.ExpectedBytes, release.ExpectedBytes));
            string efiStaging = Path.Combine(staging, "EFI");
            Directory.CreateDirectory(efiStaging);
            ExtractEfiAndSample(archive, efiStaging);

            File.WriteAllText(Path.Combine(efiStaging, "OpenCore-Version.txt"),
                $"OpenCore {release.Version} — official Acidanthera RELEASE package{Environment.NewLine}SHA-256: {actualHash}{Environment.NewLine}",
                new System.Text.UTF8Encoding(false));
            string warning = $@"SIMPLE HACKINTOSH — EFI STARTER

OpenCore version: {release.Version}
Official archive SHA-256: {actualHash}

IMPORTANT: This is an OpenCore starter folder, NOT a ready-to-boot,
computer-specific EFI. config.plist is the matching upstream Sample.plist.
It has not been configured for your motherboard, CPU, GPU, storage, audio,
network, ACPI tables, Kexts, SMBIOS, NVRAM or UEFI firmware.

Before using it:
1. Build a hardware-specific configuration using the official OpenCore guide.
2. Select compatible ACPI, Kexts and UEFI drivers for the exact target PC.
3. Configure PlatformInfo/SMBIOS with identifiers generated for that PC.
4. Validate with ocvalidate from OpenCore {release.Version}.
5. Keep a backup and test from removable media before changing internal boot files.

This tool did not write to a disk partition, EFI System Partition or firmware setting.
";
            File.WriteAllText(Path.Combine(efiStaging, "READ-ME-FIRST.txt"), warning, new System.Text.UTF8Encoding(false));

            cancellationToken.ThrowIfCancellationRequested();
            if (Directory.Exists(targetEfi) || File.Exists(targetEfi))
                throw new IOException("An EFI item appeared at the destination during the build. Nothing was overwritten.");
            Directory.Move(efiStaging, targetEfi);
            progress?.Report(new OpenCoreProgress("EFI starter created successfully.", release.ExpectedBytes, release.ExpectedBytes));
            return new OpenCoreBuildResult(release.Version, targetEfi, actualHash);
        }
        finally
        {
            try { if (Directory.Exists(staging)) Directory.Delete(staging, true); } catch { }
        }
    }

    private static async Task<string> DownloadAndHashAsync(OpenCoreReleaseInfo release, string outputPath,
        IProgress<OpenCoreProgress>? progress, CancellationToken cancellationToken)
    {
        using var response = await SendFollowingAllowedRedirectsAsync(release.DownloadUri, IsReleaseAssetHost, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            256 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[256 * 1024];
        long received = 0;
        int count;
        while ((count = await input.ReadAsync(buffer, cancellationToken)) > 0)
        {
            received += count;
            if (received > release.ExpectedBytes || received > 100L * 1024 * 1024)
                throw new InvalidDataException("The OpenCore archive exceeded its published size limit.");
            hash.AppendData(buffer, 0, count);
            await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
            progress?.Report(new OpenCoreProgress("Downloading and hashing official OpenCore…", received, release.ExpectedBytes));
        }
        await output.FlushAsync(cancellationToken);
        if (received != release.ExpectedBytes)
            throw new InvalidDataException($"OpenCore archive size mismatch. Expected {release.ExpectedBytes:N0} bytes; received {received:N0} bytes.");
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static void ExtractEfiAndSample(string archivePath, string efiDestination)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        if (archive.Entries.Count == 0 || archive.Entries.Count > 20000)
            throw new InvalidDataException("The OpenCore archive has an unexpected number of entries.");

        long totalUncompressed = 0;
        var entryPaths = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Entries)
        {
            string normalized = ValidateArchivePath(entry.FullName);
            if (!entryPaths.Add(normalized)) throw new InvalidDataException("The OpenCore archive contains duplicate paths.");
            totalUncompressed += entry.Length;
            if (entry.Length < 0 || totalUncompressed > 300L * 1024 * 1024)
                throw new InvalidDataException("The OpenCore archive exceeds the uncompressed size safety limit.");
            int unixType = (int)((entry.ExternalAttributes >> 16) & 0xF000);
            if (unixType == 0xA000 || (entry.ExternalAttributes & 0x400) != 0)
                throw new InvalidDataException("The OpenCore archive contains a link or reparse-point entry.");
        }

        bool copiedBoot = false;
        bool copiedCore = false;
        string? sampleEntry = null;
        foreach (var entry in archive.Entries)
        {
            string normalized = ValidateArchivePath(entry.FullName);
            if (normalized.Equals("Docs/Sample.plist", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("X64/Docs/Sample.plist", StringComparison.OrdinalIgnoreCase))
            {
                sampleEntry = normalized;
                continue;
            }
            const string x64Prefix = "X64/EFI/";
            if (!normalized.StartsWith(x64Prefix, StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(entry.Name))
                continue;

            string relative = normalized[x64Prefix.Length..];
            string destination = SafeCombine(efiDestination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            using var source = entry.Open();
            using var target = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            source.CopyTo(target);
            if (relative.Equals("BOOT/BOOTx64.efi", StringComparison.OrdinalIgnoreCase)) copiedBoot = true;
            if (relative.Equals("OC/OpenCore.efi", StringComparison.OrdinalIgnoreCase)) copiedCore = true;
        }

        if (!copiedBoot || !copiedCore || sampleEntry is null)
            throw new InvalidDataException("The release archive is missing X64/EFI/BOOT/BOOTx64.efi, X64/EFI/OC/OpenCore.efi or Docs/Sample.plist.");
        var sample = archive.GetEntry(sampleEntry) ?? throw new InvalidDataException("The matching Sample.plist was not found.");
        if (sample.Length <= 0 || sample.Length > 5 * 1024 * 1024)
            throw new InvalidDataException("The OpenCore Sample.plist has an unexpected size.");
        string configPath = SafeCombine(efiDestination, "OC/config.plist");
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        using var sampleInput = sample.Open();
        using var configOutput = new FileStream(configPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        sampleInput.CopyTo(configOutput);
    }

    internal static string ValidateArchivePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.IndexOf('\0') >= 0)
            throw new InvalidDataException("The OpenCore archive contains an empty or invalid path.");
        string normalized = value.Replace('\\', '/');
        if (normalized.StartsWith('/') || normalized.StartsWith("//", StringComparison.Ordinal) ||
            (normalized.Length >= 2 && char.IsLetter(normalized[0]) && normalized[1] == ':'))
            throw new InvalidDataException("The OpenCore archive contains an absolute path.");
        var parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || parts.Any(part => part is "." or ".." || part.Contains(':')))
            throw new InvalidDataException("The OpenCore archive contains a path traversal or invalid path segment.");
        return string.Join('/', parts);
    }

    internal static string SafeCombine(string root, string relative)
    {
        string fullRoot = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
        string fullPath = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("An archive path escaped the staging directory.");
        return fullPath;
    }

    private static async Task<HttpResponseMessage> SendFollowingAllowedRedirectsAsync(Uri initial,
        Func<Uri, bool> allowed, CancellationToken cancellationToken)
    {
        Uri current = initial;
        for (int hop = 0; hop <= 5; hop++)
        {
            if (!allowed(current)) throw new InvalidDataException("A download URL or redirect used an untrusted host.");
            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            request.Headers.UserAgent.ParseAdd("Simple-Hackintosh-Studio/0.3");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            int code = (int)response.StatusCode;
            if (code is not (301 or 302 or 303 or 307 or 308)) return response;
            var location = response.Headers.Location;
            response.Dispose();
            if (location is null || hop == 5)
                throw new HttpRequestException("GitHub returned an invalid or excessive redirect chain.");
            current = location.IsAbsoluteUri ? location : new Uri(current, location);
        }
        throw new HttpRequestException("GitHub redirect limit exceeded.");
    }

    private static bool IsGitHubHost(Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttps && (uri.Host == "api.github.com" || uri.Host == "github.com");

    private static bool IsReleaseAssetHost(Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttps &&
        (uri.Host == "github.com" || uri.Host == "release-assets.githubusercontent.com" || uri.Host == "objects.githubusercontent.com");

    private static string RequiredString(JsonElement value, string property)
    {
        if (!value.TryGetProperty(property, out var item) || item.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(item.GetString()))
            throw new InvalidDataException($"The official release metadata is missing '{property}'.");
        return item.GetString()!;
    }

    private static bool IsHexSha256(string value)
    {
        if (value.Length != 64) return false;
        foreach (char ch in value)
            if (!Uri.IsHexDigit(ch)) return false;
        return true;
    }
}
