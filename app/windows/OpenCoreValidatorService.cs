using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleHackintosh.App;

internal sealed record OpenCoreValidationResult(string Version, bool Success, int ExitCode, string Output);

internal static partial class OpenCoreReleaseService
{
    private static readonly Regex ValidatorVersionBanner = new(
        @"only compatible with OpenCore version\s+([0-9]+(?:\.[0-9]+){1,3})",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static async Task<OpenCoreValidationResult> ValidateWithLatestOfficialAsync(
        string configText, IProgress<OpenCoreProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(configText))
            throw new InvalidDataException("Open a non-empty XML config.plist before running official validation.");
        if (Encoding.UTF8.GetByteCount(configText) > 5 * 1024 * 1024)
            throw new InvalidDataException("The config.plist exceeds the 5 MB safety limit.");

        var release = await GetLatestReleaseAsync(cancellationToken);
        progress?.Report(new OpenCoreProgress($"Preparing official ocvalidate for OpenCore {release.Version}…", 0, null));
        string validatorPath = await EnsureWindowsValidatorAsync(release, progress, cancellationToken);

        string workRoot = Path.Combine(Path.GetTempPath(), "SimpleHackintosh", "ocvalidate-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workRoot);
        try
        {
            string configPath = Path.Combine(workRoot, "config.plist");
            await File.WriteAllTextAsync(configPath, configText, new UTF8Encoding(false), cancellationToken);

            progress?.Report(new OpenCoreProgress($"Running ocvalidate for OpenCore {release.Version}…", 0, null));
            var start = new ProcessStartInfo
            {
                FileName = validatorPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            start.ArgumentList.Add(configPath);

            using var process = new Process { StartInfo = start };
            if (!process.Start()) throw new InvalidOperationException("Could not start the verified official ocvalidate executable.");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(45));
            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
            Task<string> stderrTask = process.StandardError.ReadToEndAsync();
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(true); } catch { }
                try { await process.WaitForExitAsync(); } catch { }
                if (cancellationToken.IsCancellationRequested) throw;
                throw new TimeoutException("Official ocvalidate did not finish within 45 seconds.");
            }

            string stdout = await stdoutTask;
            string stderr = await stderrTask;
            string output = string.Join(Environment.NewLine, new[] { stdout.Trim(), stderr.Trim() }.Where(value => value.Length > 0));
            string? reportedVersion = ParseValidatorVersionBanner(output);
            if (reportedVersion is null)
                throw new InvalidDataException("The executable did not print the expected OpenCore version banner. It was not treated as a valid validator.");
            if (!string.Equals(reportedVersion, release.Version, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"The downloaded validator reports OpenCore {reportedVersion}, but the verified release is {release.Version}. Validation was stopped.");

            return new OpenCoreValidationResult(release.Version, process.ExitCode == 0, process.ExitCode,
                string.IsNullOrWhiteSpace(output) ? "ocvalidate returned no text output." : output);
        }
        finally
        {
            try { if (Directory.Exists(workRoot)) Directory.Delete(workRoot, true); } catch { }
        }
    }

    internal static string? ParseValidatorVersionBanner(string output)
    {
        if (string.IsNullOrWhiteSpace(output)) return null;
        var match = ValidatorVersionBanner.Match(output);
        return match.Success ? match.Groups[1].Value : null;
    }

    internal static bool IsWindowsValidatorPath(string path)
    {
        string normalized = ValidateArchivePath(path);
        return normalized.Equals("Utilities/ocvalidate/ocvalidate.exe", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("X64/Utilities/ocvalidate/ocvalidate.exe", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> EnsureWindowsValidatorAsync(
        OpenCoreReleaseInfo release, IProgress<OpenCoreProgress>? progress, CancellationToken cancellationToken)
    {
        string safeVersion = string.Concat(release.Version.Where(ch => char.IsAsciiLetterOrDigit(ch) || ch is '.' or '-' or '_'));
        if (safeVersion.Length == 0) throw new InvalidDataException("OpenCore release version is not a safe cache identifier.");
        string localRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localRoot)) throw new DirectoryNotFoundException("Windows Local AppData could not be located.");
        string cacheDirectory = Path.Combine(localRoot, "SimpleHackintosh", "Tools", "OpenCore-" + safeVersion);
        Directory.CreateDirectory(cacheDirectory);
        string executablePath = Path.Combine(cacheDirectory, "ocvalidate.exe");
        string hashPath = executablePath + ".sha256";

        if (File.Exists(executablePath) && File.Exists(hashPath))
        {
            try
            {
                string expected = (await File.ReadAllTextAsync(hashPath, cancellationToken)).Trim();
                string actual = await HashFileAsync(executablePath, cancellationToken);
                if (IsSha256(expected) && string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                    return executablePath;
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        string staging = Path.Combine(Path.GetTempPath(), "SimpleHackintosh", "ocvalidate-download-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        string archivePath = Path.Combine(staging, release.AssetName + ".partial");
        string temporaryExe = executablePath + "." + Guid.NewGuid().ToString("N") + ".partial";
        string temporaryHash = hashPath + "." + Guid.NewGuid().ToString("N") + ".partial";
        try
        {
            string actualArchiveHash = await DownloadAndHashAsync(release, archivePath, progress, cancellationToken);
            if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(actualArchiveHash), Convert.FromHexString(release.Sha256)))
                throw new InvalidDataException("OpenCore release SHA-256 verification failed. The validator was not extracted.");

            progress?.Report(new OpenCoreProgress("Extracting the Windows validator from the verified official archive…", release.ExpectedBytes, release.ExpectedBytes));
            using (var archive = ZipFile.OpenRead(archivePath))
            {
                if (archive.Entries.Count == 0 || archive.Entries.Count > 20000)
                    throw new InvalidDataException("The official OpenCore archive has an unexpected number of entries.");
                var seen = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
                long uncompressed = 0;
                foreach (var entry in archive.Entries)
                {
                    string normalized = ValidateArchivePath(entry.FullName);
                    if (!seen.Add(normalized)) throw new InvalidDataException("The official OpenCore archive contains duplicate paths.");
                    uncompressed += entry.Length;
                    if (entry.Length < 0 || uncompressed > 300L * 1024 * 1024)
                        throw new InvalidDataException("The official OpenCore archive exceeds the uncompressed size safety limit.");
                }

                var matches = archive.Entries
                    .Where(entry => !string.IsNullOrEmpty(entry.Name) && IsWindowsValidatorPath(entry.FullName))
                    .ToArray();
                if (matches.Length == 0)
                    throw new NotSupportedException("The verified official OpenCore release does not contain a Windows ocvalidate.exe. No unofficial executable will be substituted.");
                if (matches.Length != 1)
                    throw new InvalidDataException("The official archive contains multiple Windows ocvalidate candidates.");
                var validator = matches[0];
                int unixType = (int)((validator.ExternalAttributes >> 16) & 0xF000);
                if (unixType == 0xA000 || (validator.ExternalAttributes & 0x400) != 0)
                    throw new InvalidDataException("The ocvalidate archive entry is a link or reparse-point entry.");
                if (validator.Length <= 0 || validator.Length > 64L * 1024 * 1024)
                    throw new InvalidDataException("The official Windows ocvalidate executable has an unexpected size.");

                await using var input = validator.Open();
                await using var output = new FileStream(temporaryExe, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                    128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
                var buffer = new byte[128 * 1024];
                long copied = 0;
                int count;
                while ((count = await input.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    copied += count;
                    if (copied > validator.Length || copied > 64L * 1024 * 1024)
                        throw new InvalidDataException("The extracted validator exceeded its declared size.");
                    await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
                }
                await output.FlushAsync(cancellationToken);
                if (copied != validator.Length)
                    throw new InvalidDataException("The extracted validator size does not match the archive entry.");
            }

            string executableHash = await HashFileAsync(temporaryExe, cancellationToken);
            await File.WriteAllTextAsync(temporaryHash, executableHash + Environment.NewLine, new UTF8Encoding(false), cancellationToken);
            File.Move(temporaryExe, executablePath, true);
            File.Move(temporaryHash, hashPath, true);
            return executablePath;
        }
        finally
        {
            try { if (File.Exists(temporaryExe)) File.Delete(temporaryExe); } catch { }
            try { if (File.Exists(temporaryHash)) File.Delete(temporaryHash); } catch { }
            try { if (Directory.Exists(staging)) Directory.Delete(staging, true); } catch { }
        }
    }

    private static async Task<string> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        byte[] digest = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(digest).ToLowerInvariant();
    }

    private static bool IsSha256(string value)
    {
        if (value.Length != 64) return false;
        return value.All(Uri.IsHexDigit);
    }
}
