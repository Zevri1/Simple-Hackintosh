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

internal sealed record KextProjectInfo(string Name, string Repository, string AssetPrefix);
internal sealed record KextReleaseInfo(string Project, string Repository, string Version, Uri DownloadUri, string Sha256, long ExpectedBytes, string AssetName);
internal sealed record KextDownloadProgress(string Stage, long Received, long? Expected);
internal sealed record KextInstallResult(string Project, string Version, string Destination, IReadOnlyList<string> KextNames, string Sha256);

internal static class KextCatalogService
{
    private static readonly KextProjectInfo[] Projects =
    [
        new("Lilu", "acidanthera/Lilu", "Lilu"),
        new("VirtualSMC", "acidanthera/VirtualSMC", "VirtualSMC"),
        new("WhateverGreen", "acidanthera/WhateverGreen", "WhateverGreen"),
        new("AppleALC", "acidanthera/AppleALC", "AppleALC"),
        new("CpuTscSync", "acidanthera/CpuTscSync", "CpuTscSync")
    ];

    private static readonly HttpClient Client = new(new HttpClientHandler
    {
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
    }) { Timeout = Timeout.InfiniteTimeSpan };

    public static IReadOnlyList<KextProjectInfo> GetSupportedProjects() => Projects;

    public static async Task<KextReleaseInfo> GetLatestStableReleaseAsync(KextProjectInfo project, CancellationToken cancellationToken = default)
    {
        if (!Projects.Any(item => item.Repository == project.Repository && item.AssetPrefix == project.AssetPrefix && item.Name == project.Name))
            throw new InvalidDataException("The selected Kext project is not in the built-in official-source allowlist.");
        var api = new Uri($"https://api.github.com/repos/{project.Repository}/releases/latest");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(2));
        using var response = await SendFollowingAllowedRedirectsAsync(api, IsGitHubApiHost, timeout.Token);
        response.EnsureSuccessStatusCode();
        await using var payload = await response.Content.ReadAsStreamAsync(timeout.Token);
        using var document = await JsonDocument.ParseAsync(payload, cancellationToken: timeout.Token);
        return ParseLatestRelease(project, document.RootElement);
    }

    internal static KextReleaseInfo ParseLatestRelease(KextProjectInfo project, JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("The Kext release response is not a JSON object.");
        if (root.TryGetProperty("draft", out var draft) && draft.ValueKind == JsonValueKind.True)
            throw new InvalidDataException("GitHub returned a draft Kext release.");
        if (root.TryGetProperty("prerelease", out var prerelease) && prerelease.ValueKind == JsonValueKind.True)
            throw new InvalidDataException("GitHub returned a prerelease instead of a stable release.");
        string tag = RequiredString(root, "tag_name");
        if (tag.Length > 40 || tag.Contains('/') || tag.Contains('\\') || tag.Contains("..", StringComparison.Ordinal))
            throw new InvalidDataException("The Kext release tag is not a safe version identifier.");

        string expectedAsset = $"{project.AssetPrefix}-{tag}-RELEASE.zip";
        if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("The official Kext release has no asset list.");

        foreach (var asset in assets.EnumerateArray())
        {
            if (asset.ValueKind != JsonValueKind.Object ||
                !asset.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String ||
                !string.Equals(name.GetString(), expectedAsset, StringComparison.Ordinal)) continue;
            if (!asset.TryGetProperty("digest", out var digestElement) || digestElement.ValueKind != JsonValueKind.String)
                throw new NotSupportedException("The official release does not publish a SHA-256 digest for this package. The app will not download an unverifiable Kext archive.");
            string digest = digestElement.GetString() ?? "";
            if (!digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) || digest.Length != 71)
                throw new InvalidDataException("The official Kext package SHA-256 digest is malformed.");
            string hash = digest["sha256:".Length..].ToLowerInvariant();
            if (!IsSha256(hash)) throw new InvalidDataException("The official Kext package SHA-256 digest is not hexadecimal.");

            if (!asset.TryGetProperty("browser_download_url", out var urlElement) || urlElement.ValueKind != JsonValueKind.String ||
                !Uri.TryCreate(urlElement.GetString(), UriKind.Absolute, out var uri) ||
                uri.Scheme != Uri.UriSchemeHttps || uri.Host != "github.com" ||
                uri.AbsolutePath != $"/{project.Repository}/releases/download/{tag}/{expectedAsset}")
                throw new InvalidDataException("The Kext package URL does not match the official upstream release path.");
            if (!asset.TryGetProperty("size", out var sizeElement) || !sizeElement.TryGetInt64(out long size) ||
                size <= 0 || size > 100L * 1024 * 1024)
                throw new InvalidDataException("The official Kext package has an unexpected download size.");
            return new KextReleaseInfo(project.Name, project.Repository, tag, uri, hash, size, expectedAsset);
        }
        throw new NotSupportedException("No stable RELEASE ZIP with a published SHA-256 digest was found. No unofficial source will be substituted.");
    }

    public static async Task<KextInstallResult> DownloadAndExtractAsync(
        KextReleaseInfo release, string destination, IProgress<KextDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(destination) || !Directory.Exists(destination))
            throw new DirectoryNotFoundException("Choose an existing destination folder.");
        var project = Projects.SingleOrDefault(item => item.Name == release.Project && item.Repository == release.Repository);
        if (project is null) throw new InvalidDataException("The selected Kext release is not from an allowed upstream project.");
        string expectedAsset = $"{project.AssetPrefix}-{release.Version}-RELEASE.zip";
        if (release.AssetName != expectedAsset || release.DownloadUri.Scheme != Uri.UriSchemeHttps ||
            release.DownloadUri.Host != "github.com" ||
            release.DownloadUri.AbsolutePath != $"/{project.Repository}/releases/download/{release.Version}/{expectedAsset}" ||
            !IsSha256(release.Sha256) || release.ExpectedBytes <= 0 || release.ExpectedBytes > 100L * 1024 * 1024)
            throw new InvalidDataException("The Kext release metadata did not match the allowlisted official asset.");

        string finalName = $"Kexts-{SafeName(release.Project)}-{SafeName(release.Version)}";
        string finalPath = Path.Combine(destination, finalName);
        if (Directory.Exists(finalPath) || File.Exists(finalPath))
            throw new IOException("A folder for this Kext version already exists. Choose another destination; existing files are never overwritten.");

        string workingRoot = Path.Combine(destination, ".simple-hackintosh-kext-" + Guid.NewGuid().ToString("N"));
        string packageRoot = Path.Combine(workingRoot, "package");
        string archivePath = Path.Combine(workingRoot, release.AssetName + ".partial");
        Directory.CreateDirectory(packageRoot);
        try
        {
            string actualHash = await DownloadAndHashAsync(release, archivePath, progress, cancellationToken);
            if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(actualHash), Convert.FromHexString(release.Sha256)))
                throw new InvalidDataException("Kext archive SHA-256 verification failed. Nothing was extracted.");

            progress?.Report(new KextDownloadProgress("Verifying archive and extracting Kext bundles…", release.ExpectedBytes, release.ExpectedBytes));
            var names = ExtractKextBundles(archivePath, packageRoot);
            File.WriteAllText(Path.Combine(packageRoot, "PACKAGE-INFO.txt"),
                $"Project: {release.Project}{Environment.NewLine}Repository: https://github.com/{release.Repository}{Environment.NewLine}Version: {release.Version}{Environment.NewLine}Archive: {release.AssetName}{Environment.NewLine}SHA-256: {actualHash}{Environment.NewLine}{Environment.NewLine}Only Kext bundles from the verified official release archive were extracted. Review upstream compatibility notes and add only the required bundles to EFI/OC/Kexts. This folder does not modify config.plist or any existing EFI.{Environment.NewLine}",
                new UTF8Encoding(false));
            cancellationToken.ThrowIfCancellationRequested();
            if (Directory.Exists(finalPath) || File.Exists(finalPath))
                throw new IOException("A destination folder appeared while extracting. Nothing was overwritten.");
            Directory.Move(packageRoot, finalPath);
            progress?.Report(new KextDownloadProgress("Official Kext package extracted successfully.", release.ExpectedBytes, release.ExpectedBytes));
            return new KextInstallResult(release.Project, release.Version, finalPath, names, actualHash);
        }
        finally { try { if (Directory.Exists(workingRoot)) Directory.Delete(workingRoot, true); } catch { } }
    }

    internal static string ValidateArchivePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.IndexOf('\0') >= 0)
            throw new InvalidDataException("The Kext archive contains an empty or invalid path.");
        string normalized = value.Replace('\\', '/');
        if (normalized.StartsWith('/') || (normalized.Length >= 2 && char.IsLetter(normalized[0]) && normalized[1] == ':'))
            throw new InvalidDataException("The Kext archive contains an absolute path.");
        var parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || parts.Any(part => part is "." or ".." || part.Contains(':')))
            throw new InvalidDataException("The Kext archive contains a path traversal or invalid path segment.");
        return string.Join('/', parts);
    }

    internal static string? FindKextBundleRelativePath(string archivePath)
    {
        string[] parts = ValidateArchivePath(archivePath).Split('/');
        int index = Array.FindIndex(parts, part => part.EndsWith(".kext", StringComparison.OrdinalIgnoreCase));
        if (index < 0) return null;
        string name = parts[index];
        if (name.Length <= 5 || name.Any(ch => !(char.IsAsciiLetterOrDigit(ch) || ch is '.' or '-' or '_')))
            throw new InvalidDataException("The Kext archive contains an invalid bundle name.");
        return string.Join('/', parts.Skip(index));
    }

    private static IReadOnlyList<string> ExtractKextBundles(string archivePath, string packageRoot)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        if (archive.Entries.Count == 0 || archive.Entries.Count > 20000)
            throw new InvalidDataException("The Kext archive has an unexpected number of entries.");
        long total = 0;
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var infoFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Entries)
        {
            string normalized = ValidateArchivePath(entry.FullName);
            if (!paths.Add(normalized)) throw new InvalidDataException("The Kext archive contains duplicate paths.");
            total += entry.Length;
            if (entry.Length < 0 || total > 500L * 1024 * 1024)
                throw new InvalidDataException("The Kext archive exceeds the uncompressed size safety limit.");
            int unixType = (int)((entry.ExternalAttributes >> 16) & 0xF000);
            if (unixType == 0xA000 || (entry.ExternalAttributes & 0x400) != 0)
                throw new InvalidDataException("The Kext archive contains a link or reparse-point entry.");

            string? bundleRelative = FindKextBundleRelativePath(normalized);
            if (bundleRelative is null) continue;
            string[] bundleParts = bundleRelative.Split('/');
            string bundleName = bundleParts[0];
            roots.Add(bundleName);
            string inner = string.Join('/', bundleParts.Skip(1));
            string bundleRoot = SafeCombine(packageRoot, bundleName);
            string outputPath = inner.Length == 0 ? bundleRoot : SafeCombine(bundleRoot, inner);
            if (entry.FullName.EndsWith('/') || string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(outputPath);
                continue;
            }
            if (entry.Length > 100L * 1024 * 1024)
                throw new InvalidDataException("An extracted Kext file exceeds the per-file safety limit.");
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            using var input = entry.Open();
            using var output = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            input.CopyTo(output);
            if (inner.Equals("Contents/Info.plist", StringComparison.OrdinalIgnoreCase))
                infoFiles.Add(bundleName);
        }
        if (roots.Count == 0 || roots.Any(name => !infoFiles.Contains(name)))
            throw new InvalidDataException("The official archive did not contain complete Kext bundles with Contents/Info.plist.");
        return roots.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string SafeCombine(string root, string relative)
    {
        string fullRoot = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
        string fullPath = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("An archive path escaped the Kext staging directory.");
        return fullPath;
    }

    private static async Task<string> DownloadAndHashAsync(KextReleaseInfo release, string outputPath,
        IProgress<KextDownloadProgress>? progress, CancellationToken cancellationToken)
    {
        using var response = await SendFollowingAllowedRedirectsAsync(release.DownloadUri, IsReleaseAssetHost, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[128 * 1024];
        long received = 0;
        int count;
        while ((count = await input.ReadAsync(buffer, cancellationToken)) > 0)
        {
            received += count;
            if (received > release.ExpectedBytes || received > 100L * 1024 * 1024)
                throw new InvalidDataException("The Kext archive exceeded its published size limit.");
            hash.AppendData(buffer, 0, count);
            await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
            progress?.Report(new KextDownloadProgress("Downloading and hashing the official Kext archive…", received, release.ExpectedBytes));
        }
        await output.FlushAsync(cancellationToken);
        if (received != release.ExpectedBytes)
            throw new InvalidDataException($"Kext archive size mismatch. Expected {release.ExpectedBytes:N0} bytes; received {received:N0} bytes.");
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static async Task<HttpResponseMessage> SendFollowingAllowedRedirectsAsync(
        Uri initial, Func<Uri, bool> allowed, CancellationToken cancellationToken)
    {
        Uri current = initial;
        for (int hop = 0; hop <= 5; hop++)
        {
            if (!allowed(current)) throw new InvalidDataException("The Kext metadata or download used an untrusted HTTPS host.");
            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            request.Headers.UserAgent.ParseAdd("Simple-Hackintosh-Studio/0.5");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            int status = (int)response.StatusCode;
            if (status is not (301 or 302 or 303 or 307 or 308)) return response;
            var location = response.Headers.Location;
            response.Dispose();
            if (location is null || hop == 5) throw new HttpRequestException("GitHub returned an invalid or excessive redirect chain.");
            current = location.IsAbsoluteUri ? location : new Uri(current, location);
        }
        throw new HttpRequestException("GitHub redirect limit exceeded.");
    }

    private static bool IsGitHubApiHost(Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttps && (uri.Host == "api.github.com" || uri.Host == "github.com");

    private static bool IsReleaseAssetHost(Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttps &&
        (uri.Host == "github.com" || uri.Host == "release-assets.githubusercontent.com" || uri.Host == "objects.githubusercontent.com");

    private static string RequiredString(JsonElement value, string property)
    {
        if (!value.TryGetProperty(property, out var item) || item.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(item.GetString()))
            throw new InvalidDataException($"The official GitHub release metadata is missing '{property}'.");
        return item.GetString()!;
    }

    private static string SafeName(string value) =>
        string.Concat(value.Where(ch => char.IsAsciiLetterOrDigit(ch) || ch is '.' or '-' or '_'));

    private static bool IsSha256(string value) => value.Length == 64 && value.All(Uri.IsHexDigit);
}
