using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleHackintosh.App;

internal sealed record OfficialComponentSource(string Name, string Repository, string Purpose, string Guidance);
internal sealed record OfficialComponentRelease(
    OfficialComponentSource Source, string Version, Uri DownloadUri, string? Sha256,
    long ExpectedBytes, Uri ReleasePageUri);
internal sealed record ComponentDownloadProgress(string Stage, long Received, long? Expected);
internal sealed record ComponentDownloadResult(
    string ComponentName, string Version, string OutputDirectory, string Sha256, IReadOnlyList<string> KextNames);

internal static class OfficialComponentService
{
    private static readonly OfficialComponentSource[] KnownSources =
    [
        new("Lilu", "acidanthera/Lilu", "Common patching framework used by several plugins.",
            "Treat Lilu as a dependency where a selected plugin requires it; it is not a universal fix by itself."),
        new("VirtualSMC", "acidanthera/VirtualSMC", "SMC emulation and related companion kexts.",
            "Choose only the VirtualSMC components appropriate to the target hardware; do not inject every companion kext automatically."),
        new("WhateverGreen", "acidanthera/WhateverGreen", "Graphics-related compatibility and property handling.",
            "Check the exact GPU generation and target macOS release before enabling it."),
        new("AppleALC", "acidanthera/AppleALC", "AppleHDA-based audio support for supported codecs.",
            "The exact audio codec and layout must be identified separately; downloading AppleALC does not configure audio."),
        new("IntelMausi", "acidanthera/IntelMausi", "Ethernet support for compatible Intel network controllers.",
            "Confirm the exact Ethernet controller PCI ID and supported macOS versions before use."),
        new("NVMeFix", "acidanthera/NVMeFix", "NVMe power-management and compatibility improvements.",
            "Do not add it unless the target NVMe controller and observed issue make it relevant."),
        new("RestrictEvents", "acidanthera/RestrictEvents", "Targeted workarounds for selected platform and macOS behaviours.",
            "Use only for a documented matching issue; it is not required on every Hackintosh."),
        new("CpuTscSync", "acidanthera/CpuTscSync", "TSC synchronization for selected CPU and firmware combinations.",
            "Check the CPU family, firmware behaviour, and upstream guidance before adding this kext.")
    ];

    private static readonly HttpClient Client = new(new HttpClientHandler
    {
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
    }) { Timeout = Timeout.InfiniteTimeSpan };

    public static IReadOnlyList<OfficialComponentSource> Sources => KnownSources;

    public static async Task<OfficialComponentRelease> GetLatestStableReleaseAsync(
        OfficialComponentSource source, CancellationToken cancellationToken = default)
    {
        EnsureKnownSource(source);
        var apiUri = new Uri($"https://api.github.com/repos/{source.Repository}/releases?per_page=20&page=1");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(2));
        using var response = await SendFollowingAllowedRedirectsAsync(apiUri, IsGitHubApiHost, timeout.Token);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token);
        var release = ParseLatestStableRelease(source, document.RootElement);
        return release;
    }

    internal static OfficialComponentRelease ParseLatestStableRelease(
        OfficialComponentSource source, JsonElement root)
    {
        EnsureKnownSource(source);
        if (root.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("The official GitHub release list is not a JSON array.");

        foreach (var item in root.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            if (item.TryGetProperty("draft", out var draft) && draft.ValueKind == JsonValueKind.True) continue;
            if (item.TryGetProperty("prerelease", out var prerelease) && prerelease.ValueKind == JsonValueKind.True) continue;
            if (!item.TryGetProperty("tag_name", out var tagElement) || tagElement.ValueKind != JsonValueKind.String) continue;
            string tag = tagElement.GetString() ?? "";
            if (!IsSafeIdentifier(tag)) continue;

            if (!item.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array) continue;
            string expectedAssetName = $"{source.Name}-{tag}-RELEASE.zip";
            foreach (var asset in assets.EnumerateArray())
            {
                if (asset.ValueKind != JsonValueKind.Object ||
                    !asset.TryGetProperty("name", out var nameElement) ||
                    nameElement.ValueKind != JsonValueKind.String ||
                    !string.Equals(nameElement.GetString(), expectedAssetName, StringComparison.Ordinal))
                    continue;

                if (!Uri.TryCreate(ReadString(asset, "browser_download_url"), UriKind.Absolute, out var downloadUri) ||
                    !IsOfficialAssetUri(source, tag, expectedAssetName, downloadUri))
                    throw new InvalidDataException($"The {source.Name} release asset URL does not match its official GitHub repository.");

                if (!asset.TryGetProperty("size", out var sizeElement) ||
                    !sizeElement.TryGetInt64(out long size) || size <= 0 || size > 100L * 1024 * 1024)
                    throw new InvalidDataException($"The {source.Name} release asset has an unexpected size.");

                string? digest = null;
                string? digestText = ReadOptionalString(asset, "digest");
                if (!string.IsNullOrWhiteSpace(digestText))
                {
                    if (!digestText.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) ||
                        digestText.Length != 71 ||
                        !IsHexSha256(digestText["sha256:".Length..]))
                        throw new InvalidDataException($"The {source.Name} release has a malformed SHA-256 digest.");
                    digest = digestText["sha256:".Length..].ToLowerInvariant();
                }

                var releasePage = new Uri($"https://github.com/{source.Repository}/releases/tag/{Uri.EscapeDataString(tag)}");
                return new OfficialComponentRelease(source, tag, downloadUri, digest, size, releasePage);
            }
        }

        throw new InvalidDataException($"No stable RELEASE ZIP was found for {source.Name} in the latest official releases.");
    }

    public static async Task<ComponentDownloadResult> DownloadAndExtractKextsAsync(
        OfficialComponentRelease release, string destination,
        IProgress<ComponentDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        EnsureKnownSource(release.Source);
        if (release.Sha256 is null)
            throw new InvalidDataException("GitHub has not published a SHA-256 digest for this asset. The app will not download an unverifiable package.");
        if (!IsOfficialAssetUri(release.Source, release.Version,
            $"{release.Source.Name}-{release.Version}-RELEASE.zip", release.DownloadUri))
            throw new InvalidDataException("The selected component URL is not the official release asset.");
        if (!Directory.Exists(destination))
            throw new DirectoryNotFoundException("Choose an existing destination folder.");

        string componentSlug = SafeSlug(release.Source.Name);
        string versionSlug = SafeSlug(release.Version);
        string finalDirectory = Path.Combine(destination, $"SimpleHackintosh-{componentSlug}-{versionSlug}");
        if (Directory.Exists(finalDirectory) || File.Exists(finalDirectory))
            throw new IOException("The component output folder already exists. Choose another destination; existing files are never overwritten.");

        string staging = Path.Combine(destination, ".simple-hackintosh-component-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        string archivePath = Path.Combine(staging, "component.zip.partial");
        try
        {
            progress?.Report(new ComponentDownloadProgress("Downloading the official component archive…", 0, release.ExpectedBytes));
            string actualHash = await DownloadAndHashAsync(release, archivePath, progress, cancellationToken);
            if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(actualHash), Convert.FromHexString(release.Sha256)))
                throw new InvalidDataException("The official component SHA-256 verification failed. Nothing was extracted.");

            progress?.Report(new ComponentDownloadProgress("Extracting verified Kext bundles…", release.ExpectedBytes, release.ExpectedBytes));
            var kextNames = ExtractKextBundles(archivePath, Path.Combine(staging, "Kexts"));
            string notes = $@"SIMPLE HACKINTOSH — OFFICIAL COMPONENT DOWNLOAD

Component: {release.Source.Name}
Repository: https://github.com/{release.Source.Repository}
Version: {release.Version}
Archive SHA-256: {actualHash}
Release notes: {release.ReleasePageUri}

Extracted Kext bundles:
{string.Join(Environment.NewLine, kextNames.Select(name => "- " + name))}

IMPORTANT:
- Files were extracted from the official upstream RELEASE ZIP after SHA-256 verification.
- This folder is a download/staging area, not an installed EFI.
- No config.plist was edited and no Kext was injected into an existing EFI.
- Confirm the exact hardware, target macOS version, dependencies, and upstream release notes before use.
- Add the required Kernel -> Add entries yourself, then run the matching official ocvalidate.
- Do not copy all extracted Kexts into EFI unless each one is actually required.
";
            await File.WriteAllTextAsync(Path.Combine(staging, "READ-ME-FIRST.txt"), notes,
                new UTF8Encoding(false), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            Directory.Move(staging, finalDirectory);
            progress?.Report(new ComponentDownloadProgress("Verified component download and extraction complete.", release.ExpectedBytes, release.ExpectedBytes));
            return new ComponentDownloadResult(release.Source.Name, release.Version, finalDirectory, actualHash, kextNames);
        }
        catch
        {
            try { if (Directory.Exists(staging)) Directory.Delete(staging, true); } catch { }
            throw;
        }
    }

    internal static string ValidateArchivePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.IndexOf('\0') >= 0)
            throw new InvalidDataException("The component archive contains an empty or invalid path.");
        string normalized = path.Replace('\\', '/');
        if (normalized.StartsWith('/') ||
            (normalized.Length >= 2 && char.IsLetter(normalized[0]) && normalized[1] == ':'))
            throw new InvalidDataException("The component archive contains an absolute path.");
        var parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || parts.Any(part => part is "." or ".." || part.Contains(':')))
            throw new InvalidDataException("The component archive contains a path traversal or invalid path segment.");
        return string.Join('/', parts);
    }

    internal static IReadOnlyList<string> ExtractKextBundles(string archivePath, string destination)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        if (archive.Entries.Count == 0 || archive.Entries.Count > 20000)
            throw new InvalidDataException("The component archive has an unexpected number of entries.");

        long totalUncompressed = 0;
        int extractedFiles = 0;
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var kextNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var infoPlists = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in archive.Entries)
        {
            string normalized = ValidateArchivePath(entry.FullName);
            if (!seenPaths.Add(normalized))
                throw new InvalidDataException("The component archive contains duplicate paths.");
            totalUncompressed += entry.Length;
            if (entry.Length < 0 || totalUncompressed > 500L * 1024 * 1024)
                throw new InvalidDataException("The component archive exceeds the 500 MB uncompressed safety limit.");

            int unixType = (int)((entry.ExternalAttributes >> 16) & 0xF000);
            if (unixType == 0xA000 || (entry.ExternalAttributes & 0x400) != 0)
                throw new InvalidDataException("The component archive contains a symbolic link or reparse-point entry.");

            if (string.IsNullOrEmpty(entry.Name)) continue;
            var parts = normalized.Split('/');
            int kextIndex = Array.FindIndex(parts, part => part.EndsWith(".kext", StringComparison.OrdinalIgnoreCase));
            if (kextIndex < 0 || kextIndex == parts.Length - 1) continue;

            string kextName = parts[kextIndex];
            string relative = string.Join('/', parts.Skip(kextIndex));
            string outputPath = SafeCombine(destination, relative);
            string rootKey = kextName;
            kextNames.Add(rootKey);
            if (relative.EndsWith("Contents/Info.plist", StringComparison.OrdinalIgnoreCase) ||
                relative.Equals(rootKey + "/Info.plist", StringComparison.OrdinalIgnoreCase))
                infoPlists.Add(rootKey);

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            using var input = entry.Open();
            using var output = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            input.CopyTo(output);
            extractedFiles++;
        }

        if (extractedFiles == 0 || kextNames.Count == 0)
            throw new InvalidDataException("The official archive did not contain any Kext bundles.");
        var missingInfo = kextNames.Where(name => !infoPlists.Contains(name)).ToArray();
        if (missingInfo.Length > 0)
            throw new InvalidDataException("The following Kext bundles did not contain an Info.plist: " + string.Join(", ", missingInfo));
        return kextNames.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    internal static string SafeCombine(string root, string relative)
    {
        string fullRoot = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
        string fullPath = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("A component archive path escaped the extraction folder.");
        return fullPath;
    }

    private static async Task<string> DownloadAndHashAsync(
        OfficialComponentRelease release, string outputPath,
        IProgress<ComponentDownloadProgress>? progress, CancellationToken cancellationToken)
    {
        using var response = await SendFollowingAllowedRedirectsAsync(release.DownloadUri, IsReleaseAssetHost, cancellationToken);
        response.EnsureSuccessStatusCode();
        long received = 0;
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            256 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[256 * 1024];
        int count;
        while ((count = await input.ReadAsync(buffer, cancellationToken)) > 0)
        {
            received += count;
            if (received > release.ExpectedBytes || received > 100L * 1024 * 1024)
                throw new InvalidDataException("The downloaded component archive exceeded its published size limit.");
            hash.AppendData(buffer, 0, count);
            await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
            progress?.Report(new ComponentDownloadProgress("Downloading and verifying the official release…", received, release.ExpectedBytes));
        }
        await output.FlushAsync(cancellationToken);
        if (received != release.ExpectedBytes)
            throw new InvalidDataException($"Component archive size mismatch. Expected {release.ExpectedBytes:N0} bytes; received {received:N0} bytes.");
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static async Task<HttpResponseMessage> SendFollowingAllowedRedirectsAsync(
        Uri initial, Func<Uri, bool> allowed, CancellationToken cancellationToken)
    {
        Uri current = initial;
        for (int hop = 0; hop <= 5; hop++)
        {
            if (!allowed(current))
                throw new InvalidDataException("The component API or download used an untrusted HTTPS host.");
            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            request.Headers.UserAgent.ParseAdd("Simple-Hackintosh-Studio/0.4");
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

    private static bool IsGitHubApiHost(Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttps && uri.Host.Equals("api.github.com", StringComparison.OrdinalIgnoreCase);

    private static bool IsReleaseAssetHost(Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttps &&
        (uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
         uri.Host.Equals("release-assets.githubusercontent.com", StringComparison.OrdinalIgnoreCase) ||
         uri.Host.Equals("objects.githubusercontent.com", StringComparison.OrdinalIgnoreCase));

    private static bool IsOfficialAssetUri(OfficialComponentSource source, string tag, string assetName, Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttps &&
        uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) &&
        uri.AbsolutePath.Equals($"/{source.Repository}/releases/download/{tag}/{assetName}", StringComparison.Ordinal);

    private static void EnsureKnownSource(OfficialComponentSource source)
    {
        if (!KnownSources.Any(item =>
            item.Name.Equals(source.Name, StringComparison.Ordinal) &&
            item.Repository.Equals(source.Repository, StringComparison.Ordinal)))
            throw new InvalidDataException("The selected component source is not in the built-in official-source catalogue.");
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? ReadOptionalString(JsonElement element, string name) => ReadString(element, name);

    private static bool IsSafeIdentifier(string value) =>
        value.Length is > 0 and <= 40 &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_') &&
        !value.Contains("..", StringComparison.Ordinal);

    private static bool IsHexSha256(string value) =>
        value.Length == 64 && value.All(Uri.IsHexDigit);

    private static string SafeSlug(string value) =>
        string.Concat(value.Where(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_'));
}
