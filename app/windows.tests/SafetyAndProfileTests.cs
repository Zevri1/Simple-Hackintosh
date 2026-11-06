using System.IO;
using System.Text.Json;
using SimpleHackintosh.App;
using Xunit;

namespace SimpleHackintosh.Tests;

public sealed class SafetyAndProfileTests
{
    private const string MinimalValidProfile = """
    {
      "schemaVersion": 1,
      "sourcePlatform": "windows",
      "device": {},
      "cpu": {},
      "gpu": [],
      "network": {},
      "audio": {},
      "storage": []
    }
    """;

    [Fact]
    public void AcceptsMinimalSchemaV1Profile()
    {
        Assert.Equal("Valid Hardware Profile Schema v1.", HardwareProfileService.ValidateProfile(MinimalValidProfile));
    }

    [Fact]
    public void RejectsUnknownRootProperties()
    {
        const string invalid = """
        {
          "schemaVersion": 1,
          "sourcePlatform": "windows",
          "device": {},
          "cpu": {},
          "gpu": [],
          "network": {},
          "audio": {},
          "storage": [],
          "serialNumber": "must-not-be-in-a-profile"
        }
        """;
        Assert.Throws<InvalidDataException>(() => HardwareProfileService.ValidateProfile(invalid));
    }

    [Fact]
    public void RejectsUnsupportedProfileVersions()
    {
        string invalid = MinimalValidProfile.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 2");
        Assert.Throws<InvalidDataException>(() => HardwareProfileService.ValidateProfile(invalid));
    }

    [Fact]
    public void AcceptsNormalOpenCoreArchivePath()
    {
        Assert.Equal("X64/EFI/OC/OpenCore.efi",
            OpenCoreReleaseService.ValidateArchivePath("X64/EFI/OC/OpenCore.efi"));
    }

    [Theory]
    [InlineData("../outside/boot.efi")]
    [InlineData(@"..\..\Windows\System32\calc.exe")]
    [InlineData(@"C:\Windows\System32\calc.exe")]
    [InlineData(@"\\server\share\outside")]
    public void RejectsUnsafeArchivePaths(string path)
    {
        Assert.Throws<InvalidDataException>(() => OpenCoreReleaseService.ValidateArchivePath(path));
    }

    [Fact]
    public void SafeCombineKeepsExtractionInsideStagingDirectory()
    {
        string root = Path.Combine(Path.GetTempPath(), "sh-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string result = OpenCoreReleaseService.SafeCombine(root, "OC/config.plist");
            Assert.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, result, StringComparison.OrdinalIgnoreCase);
            Assert.Throws<InvalidDataException>(() => OpenCoreReleaseService.SafeCombine(root, "../escape.txt"));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void ParsesOfficialOcvalidateVersionBanner()
    {
        const string output = "NOTE: This version of ocvalidate is only compatible with OpenCore version 1.0.8!\r\n";
        Assert.Equal("1.0.8", OpenCoreReleaseService.ParseValidatorVersionBanner(output));
    }

    [Theory]
    [InlineData("")]
    [InlineData("validation completed without a version banner")]
    public void RejectsMissingOcvalidateVersionBanner(string output)
    {
        Assert.Null(OpenCoreReleaseService.ParseValidatorVersionBanner(output));
    }

    [Theory]
    [InlineData("Utilities/ocvalidate/ocvalidate.exe")]
    [InlineData("X64/Utilities/ocvalidate/ocvalidate.exe")]
    public void RecognizesOfficialWindowsOcvalidateArchivePaths(string path)
    {
        Assert.True(OpenCoreReleaseService.IsWindowsValidatorPath(path));
    }

    [Theory]
    [InlineData("Utilities/ocvalidate/ocvalidate")]
    [InlineData("Other/ocvalidate.exe")]
    [InlineData("Utilities/ocvalidate/ocvalidate.exe/extra")]
    public void RejectsUnrecognizedOcvalidateArchivePaths(string path)
    {
        Assert.False(OpenCoreReleaseService.IsWindowsValidatorPath(path));
    }

    [Fact]
    public void BuildsReadOnlyGuidedChecklistFromProfile()
    {
        string checklist = HardwareProfileService.BuildConfigurationChecklist(MinimalValidProfile);
        Assert.Contains("# Guided EFI Preparation Checklist", checklist);
        Assert.Contains("Read-only planning aid", checklist);
        Assert.Contains("No GPU was reported", checklist);
        Assert.Contains("Run the official ocvalidate version", checklist);
        Assert.Contains("not proof that an EFI will boot", checklist);
    }
    [Fact]
    public void ParsesStableOpenCoreReleasesAndSkipsPrereleases()
    {
        const string json = """
        [
          { "tag_name": "2.0.0-beta", "draft": false, "prerelease": true, "assets": [] },
          {
            "tag_name": "1.0.8", "draft": false, "prerelease": false,
            "assets": [{
              "name": "OpenCore-1.0.8-RELEASE.zip",
              "digest": "sha256:2011e8b7216ecb2645d97ea710df965947edd406846e92050b9bc4190be6d27b",
              "size": 10544084,
              "browser_download_url": "https://github.com/acidanthera/OpenCorePkg/releases/download/1.0.8/OpenCore-1.0.8-RELEASE.zip"
            }]
          }
        ]
        """;
        using var document = JsonDocument.Parse(json);
        var releases = OpenCoreReleaseService.ParseStableReleases(document.RootElement);
        Assert.Single(releases);
        Assert.Equal("1.0.8", releases[0].Version);
    }

    [Fact]
    public void RejectsStableReleaseWithUntrustedAssetUrl()
    {
        const string json = """
        [{
          "tag_name": "1.0.8", "draft": false, "prerelease": false,
          "assets": [{
            "name": "OpenCore-1.0.8-RELEASE.zip",
            "digest": "sha256:2011e8b7216ecb2645d97ea710df965947edd406846e92050b9bc4190be6d27b",
            "size": 10544084,
            "browser_download_url": "https://example.invalid/OpenCore-1.0.8-RELEASE.zip"
          }]
        }]
        """;
        using var document = JsonDocument.Parse(json);
        Assert.Throws<InvalidDataException>(() => OpenCoreReleaseService.ParseStableReleases(document.RootElement));
    }

}
