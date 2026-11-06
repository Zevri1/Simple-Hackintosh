using System.IO;
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
}
