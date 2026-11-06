using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
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


    [Fact]
    public void OfficialComponentSourcesAreRestrictedToCuratedRepositories()
    {
        Assert.Contains(OfficialComponentService.Sources, source => source.Repository == "acidanthera/Lilu");
        Assert.Contains(OfficialComponentService.Sources, source => source.Repository == "acidanthera/AppleALC");
    }

    [Fact]
    public void ParsesVerifiedOfficialComponentRelease()
    {
        var source = OfficialComponentService.Sources.Single(item => item.Repository == "acidanthera/Lilu");
        const string json = """
        [{
          "tag_name": "1.7.2",
          "draft": false,
          "prerelease": false,
          "assets": [{
            "name": "Lilu-1.7.2-RELEASE.zip",
            "size": 781360,
            "digest": "sha256:53967d7dcfaab01023a33df2e969a89522f13d6654a6a56ac4711b62dabf3ab8",
            "browser_download_url": "https://github.com/acidanthera/Lilu/releases/download/1.7.2/Lilu-1.7.2-RELEASE.zip"
          }]
        }]
        """;
        using var document = JsonDocument.Parse(json);
        var release = OfficialComponentService.ParseLatestStableRelease(source, document.RootElement);
        Assert.Equal("1.7.2", release.Version);
        Assert.NotNull(release.Sha256);
        Assert.Equal(781360, release.ExpectedBytes);
    }

    [Fact]
    public void RefusesComponentReleaseWithoutPublishedDigestForDownloads()
    {
        var source = OfficialComponentService.Sources.Single(item => item.Repository == "acidanthera/IntelMausi");
        const string json = """
        [{
          "tag_name": "1.0.8",
          "draft": false,
          "prerelease": false,
          "assets": [{
            "name": "IntelMausi-1.0.8-RELEASE.zip",
            "size": 869695,
            "browser_download_url": "https://github.com/acidanthera/IntelMausi/releases/download/1.0.8/IntelMausi-1.0.8-RELEASE.zip"
          }]
        }]
        """;
        using var document = JsonDocument.Parse(json);
        var release = OfficialComponentService.ParseLatestStableRelease(source, document.RootElement);
        Assert.Null(release.Sha256);
    }

    [Fact]
    public void RejectsUntrustedOfficialComponentAssetUrl()
    {
        var source = OfficialComponentService.Sources.Single(item => item.Repository == "acidanthera/Lilu");
        const string json = """
        [{
          "tag_name": "1.7.2",
          "draft": false,
          "prerelease": false,
          "assets": [{
            "name": "Lilu-1.7.2-RELEASE.zip",
            "size": 781360,
            "digest": "sha256:53967d7dcfaab01023a33df2e969a89522f13d6654a6a56ac4711b62dabf3ab8",
            "browser_download_url": "https://example.invalid/Lilu-1.7.2-RELEASE.zip"
          }]
        }]
        """;
        using var document = JsonDocument.Parse(json);
        Assert.Throws<InvalidDataException>(() =>
            OfficialComponentService.ParseLatestStableRelease(source, document.RootElement));
    }

    [Theory]
    [InlineData("../outside/file.kext/Contents/Info.plist")]
    [InlineData(@"C:\outside\file.kext\Contents\Info.plist")]
    [InlineData(@"..\..\outside.kext\Contents\Info.plist")]
    public void RejectsUnsafeComponentArchivePaths(string path)
    {
        Assert.Throws<InvalidDataException>(() => OfficialComponentService.ValidateArchivePath(path));
    }

    [Fact]
    public async Task ExtractsOnlyKextBundlesIntoSafeFolder()
    {
        string root = Path.Combine(Path.GetTempPath(), "sh-kext-test-" + Guid.NewGuid().ToString("N"));
        string zip = root + ".zip";
        Directory.CreateDirectory(root);
        try
        {
            using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
            {
                var info = archive.CreateEntry("Lilu-1.7.2-RELEASE/Kexts/Release/Lilu.kext/Contents/Info.plist");
                using (var writer = new StreamWriter(info.Open())) writer.Write("<plist><dict/></plist>");
                var binary = archive.CreateEntry("Lilu-1.7.2-RELEASE/Kexts/Release/Lilu.kext/Contents/MacOS/Lilu");
                using (var writer = new StreamWriter(binary.Open())) writer.Write("test-binary");
                var docs = archive.CreateEntry("Lilu-1.7.2-RELEASE/Docs/README.md");
                using (var writer = new StreamWriter(docs.Open())) writer.Write("not extracted");
            }
            var names = await OfficialComponentService.ExtractKextBundlesAsync(zip, root);
            Assert.Equal(new[] { "Lilu.kext" }, names);
            Assert.True(File.Exists(Path.Combine(root, "Lilu.kext", "Contents", "Info.plist")));
            Assert.False(File.Exists(Path.Combine(root, "Docs", "README.md")));
        }
        finally
        {
            if (File.Exists(zip)) File.Delete(zip);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }


    [Fact]
    public void ParsesStandardAppleXmlPlistDoctypeWithoutResolvingExternalDtd()
    {
        const string xml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
        <plist version="1.0">
          <dict>
            <key>ShowPicker</key>
            <true/>
            <key>Timeout</key>
            <integer>5</integer>
          </dict>
        </plist>
        """;

        var document = SafePlistXml.Load(xml);

        Assert.Equal("plist", document.Root?.Name.LocalName);
        Assert.Null(document.DocumentType);
        Assert.Equal("ShowPicker", document.Root?.Element("dict")?.Elements().First().Value);
    }

    [Fact]
    public void RejectsMalformedXmlPlist()
    {
        const string xml = "<plist><dict><key>MissingValue</dict></plist>";
        Assert.ThrowsAny<System.Xml.XmlException>(() => SafePlistXml.Load(xml));
    }

    [Theory]
    [InlineData("Kexts/Lilu.kext/Contents/Info.plist", "Lilu.kext/Contents/Info.plist")]
    [InlineData("VirtualSMC.kext/Contents/PlugIns/SMCProcessor.kext/Contents/Info.plist", "VirtualSMC.kext/Contents/PlugIns/SMCProcessor.kext/Contents/Info.plist")]
    public void FindsKextBundleInsideOfficialArchivePaths(string path, string expected)
    {
        Assert.Equal(expected, KextCatalogService.FindKextBundleRelativePath(path));
    }

    [Theory]
    [InlineData("../outside/Lilu.kext/Contents/Info.plist")]
    [InlineData(@"C:\outside\Lilu.kext\Contents\Info.plist")]
    public void RejectsUnsafeKextArchivePaths(string path)
    {
        Assert.Throws<InvalidDataException>(() => KextCatalogService.FindKextBundleRelativePath(path));
    }

    [Fact]
    public void ParsesOnlySha256VerifiedOfficialKextReleaseAssets()
    {
        var project = KextCatalogService.GetSupportedProjects().Single(item => item.Name == "Lilu");
        const string json = """
        {
          "tag_name": "1.7.2",
          "draft": false,
          "prerelease": false,
          "assets": [{
            "name": "Lilu-1.7.2-RELEASE.zip",
            "digest": "sha256:53967d7dcfaab01023a33df2e969a89522f13d6654a6a56ac4711b62dabf3ab8",
            "size": 781360,
            "browser_download_url": "https://github.com/acidanthera/Lilu/releases/download/1.7.2/Lilu-1.7.2-RELEASE.zip"
          }]
        }
        """;
        using var document = JsonDocument.Parse(json);
        Assert.Equal("1.7.2", KextCatalogService.ParseLatestRelease(project, document.RootElement).Version);
    }


    [Fact]
    public void ConfigEditorListsAndEditsOnlyExistingScalarValues()
    {
        const string xml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
        <plist version="1.0">
          <dict>
            <key>Misc</key>
            <dict>
              <key>Boot</key>
              <dict>
                <key>ShowPicker</key><true/>
                <key>Timeout</key><integer>5</integer>
                <key>PickerMode</key><string>Builtin</string>
              </dict>
            </dict>
            <key>Kernel</key>
            <dict>
              <key>Add</key>
              <array>
                <dict><key>Enabled</key><false/><key>BundlePath</key><string>Lilu.kext</string></dict>
              </array>
            </dict>
          </dict>
        </plist>
        """;

        var session = ConfigPlistEditorSession.Load(xml);
        var showPicker = Assert.Single(session.Entries, item => item.Path == "Misc/Boot/ShowPicker");
        var timeout = Assert.Single(session.Entries, item => item.Path == "Misc/Boot/Timeout");
        var kextPath = Assert.Single(session.Entries, item => item.Path == "Kernel/Add[0]/BundlePath");
        Assert.Equal("boolean", showPicker.Type);
        Assert.Equal("integer", timeout.Type);
        Assert.Equal("string", kextPath.Type);

        session.SetValue(showPicker, "false");
        session.SetValue(timeout, "10");
        session.SetValue(kextPath, "VirtualSMC.kext");

        Assert.Equal("false", Assert.Single(session.Entries, item => item.Path == showPicker.Path).Value);
        Assert.Equal("10", Assert.Single(session.Entries, item => item.Path == timeout.Path).Value);
        Assert.Equal("VirtualSMC.kext", Assert.Single(session.Entries, item => item.Path == kextPath.Path).Value);
    }

    [Theory]
    [InlineData("integer", "not-a-number")]
    [InlineData("real", "NaN")]
    [InlineData("boolean", "yes")]
    public void ConfigEditorRejectsInvalidScalarValues(string type, string value)
    {
        string xml = type switch
        {
            "integer" => "<plist><dict><key>Value</key><integer>1</integer></dict></plist>",
            "real" => "<plist><dict><key>Value</key><real>1.5</real></dict></plist>",
            _ => "<plist><dict><key>Value</key><true/></dict></plist>"
        };
        var session = ConfigPlistEditorSession.Load(xml);
        var entry = Assert.Single(session.Entries);
        Assert.Equal(type, entry.Type == "true" ? "boolean" : entry.Type);
        Assert.Throws<InvalidDataException>(() => session.SetValue(entry, value));
    }

    [Fact]
    public void ConfigEditorSavesNewCopyAndNeverOverwritesExistingFile()
    {
        const string xml = "<plist><dict><key>Timeout</key><integer>5</integer></dict></plist>";
        var session = ConfigPlistEditorSession.Load(xml);
        string directory = Path.Combine(Path.GetTempPath(), "sh-config-edit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string existing = Path.Combine(directory, "existing.plist");
        string output = Path.Combine(directory, "new-config.plist");
        try
        {
            File.WriteAllText(existing, "keep-this-file");
            Assert.Throws<IOException>(() => session.SaveAsNewFile(existing));
            Assert.Equal("keep-this-file", File.ReadAllText(existing));

            session.SetValue(Assert.Single(session.Entries), "9");
            session.SaveAsNewFile(output);
            string savedText = File.ReadAllText(output);
            Assert.Contains("<!DOCTYPE plist PUBLIC", savedText);
            var saved = SafePlistXml.Load(savedText);
            Assert.Equal("9", saved.Root?.Element("dict")?.Element("integer")?.Value);
            Assert.Null(saved.DocumentType);
        }
        finally { Directory.Delete(directory, true); }
    }


    [Theory]
    [InlineData(@"C:\Downloads\OpenCore-1.0.8-RELEASE.zip", "1.0.8")]
    [InlineData("OpenCore-1.0.8.1-RELEASE.zip", "1.0.8.1")]
    public void ParsesOfflineReleaseZipVersion(string path, string expected)
    {
        Assert.Equal(expected, OpenCoreReleaseService.ParseOfflineReleaseVersion(path));
    }

    [Theory]
    [InlineData("OpenCore-1.0.8.zip")]
    [InlineData("OpenCore-1.0.8-DEBUG.zip")]
    [InlineData("OpenCore-../1.0.8-RELEASE.zip")]
    public void RejectsNonOfficialOfflineZipName(string path)
    {
        Assert.Throws<InvalidDataException>(() => OpenCoreReleaseService.ParseOfflineReleaseVersion(path));
    }

    [Fact]
    public void NormalizesOfficialSha256DigestPrefix()
    {
        string digest = new string('A', 64);
        Assert.Equal(digest.ToLowerInvariant(), OpenCoreReleaseService.NormalizePublishedSha256("sha256:" + digest));
        Assert.Equal(digest.ToLowerInvariant(), OpenCoreReleaseService.NormalizePublishedSha256(digest));
    }

    [Theory]
    [InlineData("")]
    [InlineData("sha256:1234")]
    [InlineData("g000000000000000000000000000000000000000000000000000000000000000")]
    public void RejectsInvalidPublishedSha256Digest(string digest)
    {
        Assert.Throws<InvalidDataException>(() => OpenCoreReleaseService.NormalizePublishedSha256(digest));
    }

    [Fact]
    public async Task ImportsOfflineReleaseOnlyAfterPublishedDigestMatches()
    {
        const string version = "9.9.99";
        string root = Path.Combine(Path.GetTempPath(), "sh-offline-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string zipPath = Path.Combine(root, $"OpenCore-{version}-RELEASE.zip");
        string cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SimpleHackintosh", "Tools", "OpenCore-" + version);
        try
        {
            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                foreach (string entryName in new[]
                {
                    "X64/EFI/BOOT/BOOTx64.efi",
                    "X64/EFI/OC/OpenCore.efi",
                    "Docs/Sample.plist",
                    "Utilities/ocvalidate/ocvalidate.exe"
                })
                {
                    var entry = archive.CreateEntry(entryName);
                    await using var stream = entry.Open();
                    await stream.WriteAsync(new byte[] { 0x53, 0x48, 0x54, 0x31 });
                }
            }

            string digest;
            await using (var input = File.OpenRead(zipPath))
                digest = Convert.ToHexString(await SHA256.HashDataAsync(input)).ToLowerInvariant();

            var imported = await OpenCoreReleaseService.ImportOfflineReleaseAsync(zipPath, "sha256:" + digest);
            Assert.Equal(version, imported.Version);
            Assert.True(File.Exists(Path.Combine(cache, "ocvalidate.exe")));
            Assert.True(File.Exists(Path.Combine(cache, "ocvalidate.exe.sha256")));

            await Assert.ThrowsAsync<InvalidDataException>(() =>
                OpenCoreReleaseService.ImportOfflineReleaseAsync(zipPath, new string('0', 64)));
        }
        finally
        {
            try { if (Directory.Exists(cache)) Directory.Delete(cache, true); } catch { }
            try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { }
        }
    }


    [Fact]
    public void OfficialKextLibraryShowsUpstreamDependenciesAndCompatibilityNotes()
    {
        var sources = OfficialComponentService.Sources;
        var lilu = Assert.Single(sources, item => item.Name == "Lilu");
        var virtualSmc = Assert.Single(sources, item => item.Name == "VirtualSMC");
        var whateverGreen = Assert.Single(sources, item => item.Name == "WhateverGreen");
        var appleAlc = Assert.Single(sources, item => item.Name == "AppleALC");
        var intelMausi = Assert.Single(sources, item => item.Name == "IntelMausi");
        var nvmeFix = Assert.Single(sources, item => item.Name == "NVMeFix");
        var cpuTscSync = Assert.Single(sources, item => item.Name == "CpuTscSync");

        Assert.Contains("plugin", lilu.Purpose, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Lilu", virtualSmc.Dependencies, StringComparison.Ordinal);
        Assert.Contains("1.5.6", whateverGreen.Dependencies, StringComparison.Ordinal);
        Assert.Contains("layout-id", appleAlc.Guidance, StringComparison.Ordinal);
        Assert.Contains("I219", intelMausi.Compatibility, StringComparison.Ordinal);
        Assert.Contains("1.4.1", nvmeFix.Dependencies, StringComparison.Ordinal);
        Assert.Contains("MSR_IA32_TSC_ADJUST", cpuTscSync.Compatibility, StringComparison.Ordinal);
    }

    [Fact]
    public void OfficialKextLibraryDocumentationLinksUseUpstreamGitHub()
    {
        foreach (var source in OfficialComponentService.Sources)
        {
            Assert.True(Uri.TryCreate(source.DocumentationUrl, UriKind.Absolute, out var uri));
            Assert.Equal(Uri.UriSchemeHttps, uri!.Scheme);
            Assert.Equal("github.com", uri.Host);
            Assert.StartsWith($"/{source.Repository}", uri.AbsolutePath, StringComparison.OrdinalIgnoreCase);
        }
    }


    [Fact]
    public void HardwareAssessmentIdentifiesPotentialIntelEthernetAndIntelWifiWithoutOverclaiming()
    {
        const string profile = """
        {
          "schemaVersion": 1,
          "sourcePlatform": "windows",
          "device": { "manufacturer": "Lenovo", "model": "ThinkPad L14 Gen 1" },
          "cpu": { "vendor": "GenuineIntel", "model": "Intel Core i5-10210U" },
          "gpu": [{ "vendor": "Intel", "model": "Intel UHD Graphics", "pciVendorId": "8086", "pciDeviceId": "9B41" }],
          "network": {
            "wifiAdapters": [{ "vendor": "Intel", "model": "Intel Wi-Fi 6 AX200", "pciVendorId": "8086", "pciDeviceId": "2723" }],
            "ethernetAdapters": [{ "vendor": "Intel", "model": "Intel Ethernet Connection I219-V", "pciVendorId": "8086", "pciDeviceId": "15BE" }],
            "bluetoothAdapters": ["Intel Bluetooth"]
          },
          "audio": { "controllers": ["Intel Smart Sound"] },
          "storage": [{ "model": "NVMe SSD", "controller": "NVMe", "interface": "NVMe" }]
        }
        """;

        string report = HardwareKextCompatibilityService.Analyze(profile, "macOS 26 Tahoe");

        Assert.Contains("IntelMausi — Potential match", report, StringComparison.Ordinal);
        Assert.Contains("I219-V", report, StringComparison.Ordinal);
        Assert.Contains("Intel Wi-Fi (separate project) — Detected", report, StringComparison.Ordinal);
        Assert.Contains("AirDrop", report, StringComparison.Ordinal);
        Assert.Contains("AppleALC — Insufficient data", report, StringComparison.Ordinal);
        Assert.Contains("No codec ID", report, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("NVMeFix — Potentially relevant", report, StringComparison.Ordinal);
        Assert.Contains("Target macOS: macOS 26 Tahoe", report, StringComparison.Ordinal);
        Assert.Contains("No Kext is downloaded, enabled", report, StringComparison.Ordinal);
    }

    [Fact]
    public void HardwareAssessmentNeverInfersKextsFromAnEmptyProfile()
    {
        const string profile = """
        {
          "schemaVersion": 1,
          "sourcePlatform": "manual",
          "device": {},
          "cpu": {},
          "gpu": [],
          "network": {},
          "audio": {},
          "storage": []
        }
        """;

        string report = HardwareKextCompatibilityService.Analyze(profile);

        Assert.Contains("WhateverGreen — Insufficient data", report, StringComparison.Ordinal);
        Assert.Contains("IntelMausi — Not detected", report, StringComparison.Ordinal);
        Assert.Contains("Intel Wi-Fi (separate project) — Not detected", report, StringComparison.Ordinal);
        Assert.Contains("AppleALC — Not detected", report, StringComparison.Ordinal);
        Assert.Contains("NVMeFix — Insufficient data", report, StringComparison.Ordinal);
        Assert.Contains("Target macOS: Not selected", report, StringComparison.Ordinal);
    }


    [Fact]
    public void ExtractsExactHdaCodecVendorAndDeviceIdsFromWindowsPnpIdentifiers()
    {
        string? codec = HardwareProfileService.BuildAudioCodecSummary(new[]
        {
            @"HDAUDIO\FUNC_01&VEN_10EC&DEV_0256&SUBSYS_17AA1234&REV_1000",
            @"HDAUDIO\FUNC_01&VEN_10EC&DEV_0256&SUBSYS_17AA1234&REV_1000",
            @"HDAUDIO\FUNC_02&VEN_8086&DEV_280B&SUBSYS_17AA1234&REV_1000",
            @"USB\VID_0BDA&PID_4014"
        });

        Assert.Equal("Realtek HDA codec (VEN_10EC DEV_0256)", codec);
    }

    [Fact]
    public void DoesNotGuessCodecFromFriendlyNameOrNonHdaPnPIdentifiers()
    {
        string? codec = HardwareProfileService.BuildAudioCodecSummary(new[]
        {
            "Realtek Audio",
            @"HDAUDIO\FUNC_02&VEN_10EC&DEV_0256&SUBSYS_17AA1234",
            @"USB\VID_0BDA&PID_4014"
        });

        Assert.Null(codec);
    }

}
