using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleHackintosh.App;

internal static partial class OpenCoreReleaseService
{
    private static readonly Uri StableReleasesApi = new("https://api.github.com/repos/acidanthera/OpenCorePkg/releases");

    public static async Task<IReadOnlyList<OpenCoreReleaseInfo>> GetStableReleasesAsync(
        int maxResults = 20, CancellationToken cancellationToken = default)
    {
        if (maxResults is < 1 or > 50) throw new ArgumentOutOfRangeException(nameof(maxResults));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(2));
        var endpoint = new Uri(StableReleasesApi.AbsoluteUri + "?per_page=50&page=1");
        using var response = await SendFollowingAllowedRedirectsAsync(endpoint, IsGitHubHost, timeout.Token);
        response.EnsureSuccessStatusCode();
        await using var payload = await response.Content.ReadAsStreamAsync(timeout.Token);
        using var document = await JsonDocument.ParseAsync(payload, cancellationToken: timeout.Token);
        return ParseStableReleases(document.RootElement, maxResults);
    }

    internal static IReadOnlyList<OpenCoreReleaseInfo> ParseStableReleases(JsonElement root, int maxResults = 20)
    {
        if (maxResults is < 1 or > 50) throw new ArgumentOutOfRangeException(nameof(maxResults));
        if (root.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("The official OpenCore release list is not a JSON array.");
        var results = new List<OpenCoreReleaseInfo>();
        foreach (var release in root.EnumerateArray())
        {
            if (release.ValueKind != JsonValueKind.Object) continue;
            if (release.TryGetProperty("draft", out var draft) && draft.ValueKind == JsonValueKind.True) continue;
            if (release.TryGetProperty("prerelease", out var prerelease) && prerelease.ValueKind == JsonValueKind.True) continue;
            if (!release.TryGetProperty("tag_name", out var tagElement) || tagElement.ValueKind != JsonValueKind.String) continue;
            string tag = tagElement.GetString() ?? "";
            if (tag.Length == 0 || tag.Length > 40 || tag.Contains('/') || tag.Contains('\\') || tag.Contains("..", StringComparison.Ordinal)) continue;
            if (!release.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array) continue;
            foreach (var asset in assets.EnumerateArray())
            {
                if (asset.ValueKind != JsonValueKind.Object ||
                    !asset.TryGetProperty("name", out var nameElement) ||
                    nameElement.ValueKind != JsonValueKind.String ||
                    !string.Equals(nameElement.GetString(), $"OpenCore-{tag}-RELEASE.zip", StringComparison.Ordinal)) continue;
                if (!asset.TryGetProperty("digest", out var digestElement) || digestElement.ValueKind != JsonValueKind.String) break;
                string digest = digestElement.GetString() ?? "";
                if (!digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) || digest.Length != 71)
                    throw new InvalidDataException($"The published SHA-256 digest for OpenCore {tag} is malformed.");
                string hash = digest["sha256:".Length..].ToLowerInvariant();
                if (!IsHexSha256(hash)) throw new InvalidDataException($"The SHA-256 digest for OpenCore {tag} is not hexadecimal.");
                if (!asset.TryGetProperty("browser_download_url", out var urlElement) || urlElement.ValueKind != JsonValueKind.String ||
                    !Uri.TryCreate(urlElement.GetString(), UriKind.Absolute, out var downloadUri) ||
                    downloadUri.Host != "github.com" ||
                    downloadUri.AbsolutePath != $"/acidanthera/OpenCorePkg/releases/download/{tag}/OpenCore-{tag}-RELEASE.zip")
                    throw new InvalidDataException($"The download URL for OpenCore {tag} is not the official Acidanthera release asset.");
                if (!asset.TryGetProperty("size", out var sizeElement) || !sizeElement.TryGetInt64(out long size) ||
                    size <= 0 || size > 100L * 1024 * 1024)
                    throw new InvalidDataException($"The OpenCore {tag} release asset has an unexpected size.");
                results.Add(new OpenCoreReleaseInfo(tag, downloadUri, hash, size, $"OpenCore-{tag}-RELEASE.zip"));
                break;
            }
            if (results.Count >= maxResults) break;
        }
        return results;
    }
}
