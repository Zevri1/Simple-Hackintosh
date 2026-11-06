using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SimpleHackintosh.App;

internal sealed record HardwareKextFinding(string Component, string Status, string Evidence, string NextStep, string SourceUrl);

internal static class HardwareKextCompatibilityService
{
    private const string DortaniaKextGuide = "https://dortania.github.io/OpenCore-Install-Guide/ktext.html";
    private const string WirelessGuide = "https://dortania.github.io/Wireless-Buyers-Guide/Kext.html";
    private const string IntelMausiGuide = "https://github.com/acidanthera/IntelMausi";
    private const string AppleAlcGuide = "https://github.com/acidanthera/AppleALC/wiki/Supported-codecs";
    private const string WhateverGreenGuide = "https://github.com/acidanthera/WhateverGreen";
    private const string NvmeFixGuide = "https://github.com/acidanthera/NVMeFix";
    private const string VirtualSmcGuide = "https://github.com/acidanthera/VirtualSMC";
    private const string RestrictEventsGuide = "https://github.com/acidanthera/RestrictEvents";
    private const string CpuTscSyncGuide = "https://github.com/acidanthera/CpuTscSync";
    
    public static IReadOnlyList<HardwareKextFinding> Analyze(string profileJson, string? targetMacOS = null)
    {
        HardwareProfileService.ValidateProfile(profileJson);
        using var document = JsonDocument.Parse(profileJson, new JsonDocumentOptions { MaxDepth = 24 });
        var root = document.RootElement;
        var findings = new List<HardwareKextFinding>();

        JsonElement device = GetObject(root, "device");
        JsonElement cpu = GetObject(root, "cpu");
        JsonElement network = GetObject(root, "network");
        JsonElement audio = GetObject(root, "audio");

        string computer = JoinNonEmpty(Read(device, "manufacturer"), Read(device, "model"));
        string cpuName = JoinNonEmpty(Read(cpu, "vendor"), Read(cpu, "model"));
        string os = string.IsNullOrWhiteSpace(targetMacOS) ? "Not selected" : Clean(targetMacOS);
        var gpus = GetArray(root, "gpu").ToArray();
        var wifi = GetArray(network, "wifiAdapters").ToArray();
        var ethernet = GetArray(network, "ethernetAdapters").ToArray();
        var storage = GetArray(root, "storage").ToArray();
        string? codec = Read(audio, "codec");
        var audioControllers = GetArray(audio, "controllers")
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => Clean(item.GetString()))
            .Where(value => value.Length > 0).ToArray();

        // GPU note: presence alone is not proof of native support or a need for a patch.
        if (gpus.Length == 0)
        {
            findings.Add(new HardwareKextFinding(
                "WhateverGreen", "Insufficient data",
                "Windows did not report a GPU in this profile.",
                "Collect the exact GPU model and PCI vendor/device ID before deciding whether graphics patching is needed.",
                WhateverGreenGuide));
        }
        else
        {
            string gpuSummary = string.Join("; ", gpus.Select(item =>
                JoinNonEmpty(Read(item, "model"), FormatPci(item))));
            findings.Add(new HardwareKextFinding(
                "WhateverGreen", "Review required",
                "Detected GPU: " + gpuSummary + ". A GPU entry alone does not establish that WhateverGreen is required.",
                "Confirm the exact GPU generation, PCI device ID, display path and target macOS support. Use only the upstream guide for that hardware.",
                WhateverGreenGuide));
        }

        // Intel Wi-Fi is not an Acidanthera Kext in the curated release list.
        if (wifi.Length == 0)
        {
            findings.Add(new HardwareKextFinding(
                "Intel Wi-Fi (separate project)", "Not detected",
                "No Wi-Fi adapter was classified in this profile.",
                "If Wi-Fi is present but not listed, collect its exact PCI hardware ID and update the hardware profile.",
                WirelessGuide));
        }
        else
        {
            foreach (var adapter in wifi)
            {
                string model = Read(adapter, "model");
                string vendor = Read(adapter, "vendor");
                string vendorId = Read(adapter, "pciVendorId");
                bool intel = vendorId.Equals("8086", StringComparison.OrdinalIgnoreCase) ||
                    vendor.Contains("Intel", StringComparison.OrdinalIgnoreCase) ||
                    model.Contains("Intel", StringComparison.OrdinalIgnoreCase);

                if (intel)
                {
                    findings.Add(new HardwareKextFinding(
                        "Intel Wi-Fi (separate project)", "Detected — limitations apply",
                        $"Windows reports {JoinNonEmpty(model, FormatPci(adapter))}.",
                        "Review the OpenIntelWireless itlwm/AirportItlwm options and exact chipset/macOS support. Dortania documents limitations such as no AirDrop and reduced Continuity support; Intel Bluetooth uses a separate firmware Kext path.",
                        WirelessGuide));
                }
                else
                {
                    findings.Add(new HardwareKextFinding(
                        "Wi-Fi adapter", "Manual identification required",
                        $"Windows reports {JoinNonEmpty(model, FormatPci(adapter))}.",
                        "Identify the chipset from the exact PCI ID and check the wireless guide. A vendor name alone is not a compatibility result.",
                        WirelessGuide));
                }
            }
        }

        // IntelMausi matches a known controller family by name, but still requires PCI ID confirmation.
        var intelEthernet = ethernet.Select(item => new
        {
            Item = item,
            Model = Read(item, "model"),
            Vendor = Read(item, "vendor"),
            VendorId = Read(item, "pciVendorId")
        }).Where(value =>
            value.VendorId.Equals("8086", StringComparison.OrdinalIgnoreCase) ||
            value.Vendor.Contains("Intel", StringComparison.OrdinalIgnoreCase) ||
            value.Model.Contains("Intel", StringComparison.OrdinalIgnoreCase)).ToArray();

        if (intelEthernet.Length == 0)
        {
            findings.Add(new HardwareKextFinding(
                "IntelMausi", ethernet.Length == 0 ? "Not detected" : "No Intel Ethernet match",
                ethernet.Length == 0 ? "No Ethernet adapter was classified in this profile." :
                    "The reported Ethernet adapter names/IDs do not identify an Intel controller.",
                "Only consider IntelMausi after identifying an Intel controller that appears in the upstream supported list.",
                IntelMausiGuide));
        }
        else
        {
            foreach (var adapter in intelEthernet)
            {
                bool knownFamily = Regex.IsMatch(adapter.Model, @"\b(?:I217|I218|I219|82578|82579)\b",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                findings.Add(new HardwareKextFinding(
                    "IntelMausi", knownFamily ? "Potential match — verify exact PCI ID" : "Intel detected — exact model unknown",
                    $"Windows reports {JoinNonEmpty(adapter.Model, FormatPci(adapter.Item))}. " +
                    (knownFamily
                        ? "The controller name contains a family listed by the upstream guide."
                        : "The profile identifies Intel Ethernet but not a specific supported controller family."),
                    "Compare the exact PCI vendor/device ID against the upstream supported-controller list and check the selected macOS version before adding IntelMausi.",
                    IntelMausiGuide));
            }
        }

        // AppleALC cannot determine a layout from the generic Windows controller label.
        if (!string.IsNullOrWhiteSpace(codec))
        {
            findings.Add(new HardwareKextFinding(
                "AppleALC", "Codec reported — support must be checked",
                "Profile codec: " + Clean(codec),
                "Check that exact codec in AppleALC's Supported Codecs page and then select a documented layout-id. Do not guess the layout from the laptop model.",
                AppleAlcGuide));
        }
        else if (audioControllers.Length > 0)
        {
            findings.Add(new HardwareKextFinding(
                "AppleALC", "Insufficient data",
                "Windows reported audio controller(s): " + string.Join("; ", audioControllers) + ". No codec ID is present in this profile.",
                "Identify the exact HDA codec before selecting AppleALC or a layout-id. A controller name is not the codec ID.",
                AppleAlcGuide));
        }
        else
        {
            findings.Add(new HardwareKextFinding(
                "AppleALC", "Not detected",
                "No audio codec or controller was reported.",
                "Collect the exact audio codec ID before evaluating AppleALC.",
                AppleAlcGuide));
        }

        // NVMeFix is relevant only when the profile explicitly identifies NVMe storage.
        var nvme = storage.Where(item =>
            Read(item, "model").Contains("NVMe", StringComparison.OrdinalIgnoreCase) ||
            Read(item, "controller").Contains("NVMe", StringComparison.OrdinalIgnoreCase) ||
            Read(item, "interface").Contains("NVMe", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (nvme.Length > 0)
        {
            findings.Add(new HardwareKextFinding(
                "NVMeFix", "Potentially relevant — issue-specific",
                "Profile identifies NVMe storage: " + string.Join("; ", nvme.Select(item => JoinNonEmpty(Read(item, "model"), Read(item, "controller")))),
                "NVMeFix is not mandatory for every NVMe drive. Confirm that the controller and observed issue match its upstream purpose and verify the required Lilu version.",
                NvmeFixGuide));
        }
        else
        {
            findings.Add(new HardwareKextFinding(
                "NVMeFix", storage.Length == 0 ? "Insufficient data" : "No explicit NVMe evidence",
                storage.Length == 0
                    ? "No storage devices were reported."
                    : "Storage devices were reported, but the profile does not explicitly identify an NVMe controller. Windows' generic interface label may be insufficient.",
                "Confirm the storage controller model and whether an NVMe-specific issue exists before considering NVMeFix.",
                NvmeFixGuide));
        }

        findings.Add(new HardwareKextFinding(
            "Lilu", "Dependency framework — conditional",
            "Lilu provides a plug-in framework; this hardware profile does not itself prove that a Lilu plug-in is required.",
            "Add a compatible Lilu release only when a selected plug-in Kext requires it. Check the minimum Lilu version in each plug-in's upstream documentation.",
            "https://github.com/acidanthera/Lilu"));
        findings.Add(new HardwareKextFinding(
            "VirtualSMC", "Platform decision required",
            $"Detected system: {JoinNonEmpty(computer, cpuName)}.",
            "VirtualSMC is an SMC-emulation option, but this report cannot decide whether it is the correct SMC implementation for the target EFI. Review the computer-specific guide and use one SMC emulator.",
            VirtualSmcGuide));
        findings.Add(new HardwareKextFinding(
            "CpuTscSync", "Not inferable from this profile",
            $"CPU reported: {cpuName}. Windows CIM does not report whether the firmware/CPU combination has the required TSC adjustment behaviour.",
            "Do not add it based on the CPU marketing name alone. Investigate only when the exact CPU/firmware behaviour and a documented boot or wake symptom justify it.",
            CpuTscSyncGuide));
        findings.Add(new HardwareKextFinding(
            "RestrictEvents", "Issue-specific — no match inferred",
            "The profile does not identify a documented macOS/platform issue that requires RestrictEvents.",
            "Add only for a specific upstream-documented issue and its matching boot arguments.",
            RestrictEventsGuide));

        return FormatReport(computer, cpuName, os, findings);
    }

    private static string FormatReport(string computer, string cpu, string targetMacOS,
        IReadOnlyList<HardwareKextFinding> findings)
    {
        var lines = new List<string>
        {
            "# Hardware-to-Kext Review",
            "",
            "Computer: " + (computer.Length == 0 ? "Not reported" : computer),
            "CPU: " + (cpu.Length == 0 ? "Not reported" : cpu),
            "Target macOS: " + targetMacOS,
            "",
            "> This is a conservative evidence report, not an automatic compatibility verdict.",
            "> No Kext is downloaded, enabled, added to config.plist, or copied into an EFI.",
            ""
        };

        foreach (var item in findings)
        {
            lines.Add("## " + item.Component + " — " + item.Status);
            lines.Add(item.Evidence);
            lines.Add("Next step: " + item.NextStep);
            if (Uri.TryCreate(item.SourceUrl, UriKind.Absolute, out var uri) &&
                uri.Scheme == Uri.UriSchemeHttps &&
                uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
                Uri.TryCreate(item.SourceUrl, UriKind.Absolute, out uri) &&
                uri.Scheme == Uri.UriSchemeHttps &&
                uri.Host.Equals("dortania.github.io", StringComparison.OrdinalIgnoreCase))
                lines.Add("Upstream guide: " + item.SourceUrl);
            lines.Add("");
        }

        lines.Add("Use the exact OpenCore and macOS versions for the final review. Run the matching official ocvalidate after any config.plist change. A candidate match is not a guarantee that a Kext will work.");
        return string.Join(Environment.NewLine, lines);
    }

    private static JsonElement GetObject(JsonElement parent, string key) =>
        parent.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Object
            ? value
            : default;

    private static IEnumerable<JsonElement> GetArray(JsonElement parent, string key) =>
        parent.ValueKind == JsonValueKind.Object &&
        parent.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray()
            : Enumerable.Empty<JsonElement>();

    private static string Read(JsonElement value, string key) =>
        value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty(key, out var property) &&
        property.ValueKind == JsonValueKind.String
            ? Clean(property.GetString())
            : "";

    private static string Clean(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? ""
            : string.Join(" ", value.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)).Trim();

    private static string FormatPci(JsonElement value)
    {
        string vendor = Read(value, "pciVendorId");
        string device = Read(value, "pciDeviceId");
        if (vendor.Length == 0 && device.Length == 0) return "";
        return "PCI " + (vendor.Length == 0 ? "VEN_????" : "VEN_" + vendor) +
            " " + (device.Length == 0 ? "DEV_????" : "DEV_" + device);
    }

    private static string JoinNonEmpty(params string[] values) =>
        string.Join(" / ", values.Where(value => !string.IsNullOrWhiteSpace(value)));
}
