using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleHackintosh.App;

internal sealed record AppleInstallerOption(string ProductId, string Title, string Version, Uri PackageUri, long? ExpectedBytes);

internal static class AppleInstallerCatalog
{
    private static readonly Uri CatalogUri = new("https://swscan.apple.com/content/catalogs/others/index-27-26-15-14-13-12-11-10.16-10.15-10.14-10.13-10.12-10.11-10.10-10.9-10.8-10.7-10.6-10.5.merged-1.sucatalog");
    private static readonly HttpClient Client = new(new HttpClientHandler { AllowAutoRedirect = false, AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli }) { Timeout = Timeout.InfiniteTimeSpan };

    public static async Task<IReadOnlyList<AppleInstallerOption>> GetInstallersAsync(CancellationToken cancellationToken = default)
    {
        using var catalogTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        catalogTimeout.CancelAfter(TimeSpan.FromMinutes(5));
        cancellationToken = catalogTimeout.Token;
        using var response = await GetAppleResponseAsync(CatalogUri, cancellationToken);
        response.EnsureSuccessStatusCode();
        EnsureAppleHttps(response.RequestMessage?.RequestUri);
        var catalog = PlistParser.Parse(await ReadLimitedTextAsync(response, 100 * 1024 * 1024, cancellationToken));
        if (!catalog.TryGetValue("Products", out var value) || value is not Dictionary<string, object?> products)
            throw new InvalidDataException("Apple's catalog does not contain a Products dictionary.");

        var candidates = products.Where(pair => pair.Value is Dictionary<string, object?> product && IsFullInstaller(product) && FindInstallAssistant(product) is not null)
            .Select(pair => (Id: pair.Key, Product: (Dictionary<string, object?>)pair.Value!)).Take(120).ToList();
        var result = new List<AppleInstallerOption>();
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryGetString(candidate.Product, "ServerMetadataURL", out var address) || !Uri.TryCreate(address, UriKind.Absolute, out var metadataUri)) continue;
            EnsureAppleHttps(metadataUri);
            try
            {
                using var metadataResponse = await GetAppleResponseAsync(metadataUri, cancellationToken);
                metadataResponse.EnsureSuccessStatusCode();
                EnsureAppleHttps(metadataResponse.RequestMessage?.RequestUri);
                var metadata = PlistParser.Parse(await ReadLimitedTextAsync(metadataResponse, 4 * 1024 * 1024, cancellationToken));
                if (!TryGetString(metadata, "CFBundleShortVersionString", out var version)) continue;
                var package = FindInstallAssistant(candidate.Product);
                if (package is null) continue;
                var title = "macOS " + version;
                if (metadata.TryGetValue("localization", out var loc) && loc is Dictionary<string, object?> localizations &&
                    localizations.TryGetValue("English", out var en) && en is Dictionary<string, object?> english &&
                    TryGetString(english, "title", out var localized)) title = localized;
                result.Add(new AppleInstallerOption(candidate.Id, title, version, package.Value.Uri, package.Value.Size));
            }
            catch (HttpRequestException) { }
            catch (InvalidDataException) { }
        }
        return result.GroupBy(x => x.Version, StringComparer.OrdinalIgnoreCase).Select(x => x.First())
            .OrderByDescending(x => Version.TryParse(x.Version, out var v) ? v : new Version(0, 0)).ToArray();
    }

    public static async Task<string> DownloadAsync(AppleInstallerOption installer, string destination,
        IProgress<(long Received, long? Expected)>? progress = null, CancellationToken cancellationToken = default)
    {
        EnsureAppleHttps(installer.PackageUri);
        if (!Directory.Exists(destination)) throw new DirectoryNotFoundException("The selected destination folder does not exist.");
        var safeVersion = string.Concat(installer.Version.Where(ch => char.IsAsciiLetterOrDigit(ch) || ch is '.' or '-'));
        var finalPath = Path.Combine(destination, "InstallAssistant-" + safeVersion + ".pkg");
        var partialPath = finalPath + ".partial";
        if (File.Exists(finalPath) || File.Exists(partialPath)) throw new IOException("A package with this name already exists.");
        try
        {
            using var response = await GetAppleResponseAsync(installer.PackageUri, cancellationToken);
            response.EnsureSuccessStatusCode();
            EnsureAppleHttps(response.RequestMessage?.RequestUri);
            var expected = installer.ExpectedBytes ?? response.Content.Headers.ContentLength;
            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var output = new FileStream(partialPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var buffer = new byte[1024 * 1024];
            long received = 0;
            int count;
            while ((count = await input.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
                received += count;
                progress?.Report((received, expected));
            }
            await output.FlushAsync(cancellationToken);
            if (expected is > 0 && received != expected.Value)
                throw new InvalidDataException($"Download size mismatch. Expected {expected.Value:N0} bytes; received {received:N0} bytes.");
            File.Move(partialPath, finalPath);
            progress?.Report((received, expected ?? received));
            return finalPath;
        }
        catch
        {
            try { if (File.Exists(partialPath)) File.Delete(partialPath); } catch { }
            throw;
        }
    }

    private static bool IsFullInstaller(Dictionary<string, object?> product)
    {
        if (!product.TryGetValue("ExtendedMetaInfo", out var extValue) || extValue is not Dictionary<string, object?> ext ||
            !ext.TryGetValue("InstallAssistantPackageIdentifiers", out var idsValue) || idsValue is not Dictionary<string, object?> ids) return false;
        bool os = TryGetString(ids, "OSInstall", out var marker) && marker == "com.apple.mpkg.OSInstall";
        bool shared = TryGetString(ids, "SharedSupport", out var support) && support.StartsWith("com.apple.pkg.InstallAssistant", StringComparison.Ordinal);
        return os || shared;
    }

    private static (Uri Uri, long? Size)? FindInstallAssistant(Dictionary<string, object?> product)
    {
        if (!product.TryGetValue("Packages", out var value) || value is not List<object?> packages) return null;
        foreach (var item in packages.OfType<Dictionary<string, object?>>())
        {
            if (!TryGetString(item, "URL", out var address) || !Uri.TryCreate(address, UriKind.Absolute, out var uri) ||
                !string.Equals(Path.GetFileName(uri.AbsolutePath), "InstallAssistant.pkg", StringComparison.OrdinalIgnoreCase)) continue;
            EnsureAppleHttps(uri);
            long? size = item.TryGetValue("Size", out var sizeValue) && long.TryParse(sizeValue?.ToString(), out var parsed) ? parsed : null;
            return (uri, size);
        }
        return null;
    }

    private static bool TryGetString(Dictionary<string, object?> dictionary, string key, out string value)
    {
        if (dictionary.TryGetValue(key, out var raw) && raw is string text && !string.IsNullOrWhiteSpace(text)) { value = text; return true; }
        value = ""; return false;
    }

    private static async Task<HttpResponseMessage> GetAppleResponseAsync(Uri initial, CancellationToken cancellationToken)
    {
        Uri current = initial;
        for (int hop = 0; hop <= 5; hop++)
        {
            EnsureAppleHttps(current);
            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            int status = (int)response.StatusCode;
            if (status is not (301 or 302 or 303 or 307 or 308))
            {
                EnsureAppleHttps(response.RequestMessage?.RequestUri);
                return response;
            }

            var location = response.Headers.Location;
            response.Dispose();
            if (location is null || hop == 5)
                throw new HttpRequestException("Apple returned an invalid or excessive redirect chain.");
            current = location.IsAbsoluteUri ? location : new Uri(current, location);
        }
        throw new HttpRequestException("Apple redirect limit exceeded.");
    }

    private static void EnsureAppleHttps(Uri? uri)
    {
        var host = uri?.Host;
        bool appleHost = host is not null && (host == "apple.com" || host.EndsWith(".apple.com", StringComparison.OrdinalIgnoreCase) ||
            host == "cdn-apple.com" || host.EndsWith(".cdn-apple.com", StringComparison.OrdinalIgnoreCase));
        if (uri is null || uri.Scheme != Uri.UriSchemeHttps || !appleHost)
            throw new InvalidDataException("Apple download rejected: URL or redirect is not an HTTPS Apple domain.");
    }

    private static async Task<string> ReadLimitedTextAsync(HttpResponseMessage response, int maxBytes, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var memory = new MemoryStream();
        var buffer = new byte[64 * 1024];
        int count;
        while ((count = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (memory.Length + count > maxBytes) throw new InvalidDataException("Apple catalog metadata exceeded the safety size limit.");
            memory.Write(buffer, 0, count);
        }
        return System.Text.Encoding.UTF8.GetString(memory.ToArray());
    }
}
