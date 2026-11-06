using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleHackintosh.App;

internal static class HardwareProfileService
{
    private const string PowerShellScript =
        "$ErrorActionPreference='SilentlyContinue'; " +
        "$cs=Get-CimInstance Win32_ComputerSystem | Select-Object -First 1 Manufacturer,Model; " +
        "$cpu=Get-CimInstance Win32_Processor | Select-Object -First 1 Manufacturer,Name,Family,Architecture,NumberOfCores; " +
        "$bios=Get-CimInstance Win32_BIOS | Select-Object -First 1 Manufacturer,SMBIOSBIOSVersion; " +
        "$gpus=@(Get-CimInstance Win32_VideoController | ForEach-Object { [pscustomobject]@{Name=$_.Name; Pnp=$_.PNPDeviceID} }); " +
        "$nets=@(Get-CimInstance Win32_NetworkAdapter | Where-Object { $_.PhysicalAdapter -and $_.PNPDeviceID } | ForEach-Object { [pscustomobject]@{Name=$_.Name; Pnp=$_.PNPDeviceID; AdapterType=$_.AdapterType} }); " +
        "$disks=@(Get-CimInstance Win32_DiskDrive | ForEach-Object { [pscustomobject]@{Model=$_.Model; InterfaceType=$_.InterfaceType; Size=$_.Size} }); " +
        "$audio=@(Get-CimInstance Win32_SoundDevice | ForEach-Object { $_.Name }); " +
        "$bt=@(Get-CimInstance Win32_PnPEntity | Where-Object { $_.Name -match 'Bluetooth' } | ForEach-Object { $_.Name }); " +
        "$mode='Unknown'; try { $fw=(Get-ComputerInfo -Property BiosFirmwareType).BiosFirmwareType; if ($fw -match 'Uefi') { $mode='UEFI' } elseif ($fw -match 'Legacy') { $mode='Legacy' } } catch {}; " +
        "[pscustomobject]@{ComputerSystem=$cs; Processor=$cpu; Bios=$bios; Graphics=$gpus; Network=$nets; Storage=$disks; Audio=$audio; Bluetooth=$bt; FirmwareMode=$mode} | ConvertTo-Json -Depth 6 -Compress";

    public static async Task<string> CollectProfileAsync(CancellationToken cancellationToken = default)
    {
        string output = await RunPowerShellAsync(PowerShellScript, cancellationToken);
        using var document = JsonDocument.Parse(output);
        var root = document.RootElement;

        JsonElement cs = GetObject(root, "ComputerSystem");
        JsonElement cpu = GetObject(root, "Processor");
        JsonElement bios = GetObject(root, "Bios");
        string? Read(JsonElement obj, string property) => GetString(obj, property);
        var pci = new Regex(@"VEN_([0-9A-Fa-f]{4}).*DEV_([0-9A-Fa-f]{4})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        var gpuItems = new List<object>();
        foreach (var item in GetArray(root, "Graphics"))
        {
            string model = Read(item, "Name") ?? "Unknown GPU";
            string pnp = Read(item, "Pnp") ?? "";
            var match = pci.Match(pnp);
            var gpu = new Dictionary<string, object?> { ["model"] = model };
            string? vendor = match.Success ? VendorName(match.Groups[1].Value) : InferVendor(model);
            if (vendor is not null) gpu["vendor"] = vendor;
            if (match.Success)
            {
                gpu["pciVendorId"] = match.Groups[1].Value.ToUpperInvariant();
                gpu["pciDeviceId"] = match.Groups[2].Value.ToUpperInvariant();
            }
            gpuItems.Add(gpu);
        }

        var wifi = new List<object>();
        var ethernet = new List<object>();
        foreach (var item in GetArray(root, "Network"))
        {
            string name = Read(item, "Name") ?? "Unknown adapter";
            string pnp = Read(item, "Pnp") ?? "";
            var match = pci.Match(pnp);
            var target = Regex.IsMatch(name, @"wi-?fi|wireless|802\.11|wlan", RegexOptions.IgnoreCase) ? wifi :
                Regex.IsMatch(name, @"ethernet|gigabit|gbe|lan|i21\d|i22\d", RegexOptions.IgnoreCase) ? ethernet : null;
            if (target is null) continue;
            var adapter = new Dictionary<string, object?> { ["model"] = name };
            string? vendor = match.Success ? VendorName(match.Groups[1].Value) : InferVendor(name);
            if (vendor is not null) adapter["vendor"] = vendor;
            if (match.Success)
            {
                adapter["pciVendorId"] = match.Groups[1].Value.ToUpperInvariant();
                adapter["pciDeviceId"] = match.Groups[2].Value.ToUpperInvariant();
            }
            target.Add(adapter);
        }

        var storage = new List<object>();
        foreach (var item in GetArray(root, "Storage"))
        {
            var disk = new Dictionary<string, object?>();
            string? model = Read(item, "Model");
            string? interfaceType = Read(item, "InterfaceType");
            if (!string.IsNullOrWhiteSpace(model)) disk["model"] = model.Trim();
            if (!string.IsNullOrWhiteSpace(interfaceType)) disk["interface"] = interfaceType.Trim();
            if (item.TryGetProperty("Size", out var size) && long.TryParse(size.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long bytes) && bytes > 0)
                disk["capacityBytes"] = bytes;
            storage.Add(disk);
        }

        string? firmwareMode = Read(root, "FirmwareMode");
        var profile = new Dictionary<string, object?>
        {
            ["schemaVersion"] = 1,
            ["sourcePlatform"] = "windows",
            ["device"] = new Dictionary<string, object?>
            {
                ["manufacturer"] = Read(cs, "Manufacturer"),
                ["model"] = Read(cs, "Model"),
                ["firmwareVendor"] = Read(bios, "Manufacturer"),
                ["firmwareVersion"] = Read(bios, "SMBIOSBIOSVersion"),
                ["firmwareMode"] = firmwareMode is "UEFI" or "Legacy" ? firmwareMode : "Unknown"
            }.Where(pair => pair.Value is not null).ToDictionary(pair => pair.Key, pair => pair.Value),
            ["cpu"] = BuildCpu(cpu),
            ["gpu"] = gpuItems,
            ["network"] = new Dictionary<string, object?>
            {
                ["wifiAdapters"] = wifi,
                ["ethernetAdapters"] = ethernet,
                ["bluetoothAdapters"] = GetArray(root, "Bluetooth").Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() : null)
                    .Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
            },
            ["audio"] = new Dictionary<string, object?>
            {
                ["controllers"] = GetArray(root, "Audio").Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() : null)
                    .Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
            },
            ["storage"] = storage,
            ["notes"] = new[]
            {
                "Automatically collected from Windows CIM. Missing properties are omitted or marked Unknown.",
                "Only PCI vendor/device IDs are retained; device instance paths, serial numbers, UUIDs and MAC addresses are not exported.",
                "Network adapter categories are included only when the Windows adapter name clearly indicates Wi-Fi or Ethernet."
            }
        };
        string json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
        ValidateProfile(json);
        return json;
    }

    public static string ValidateProfile(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || Encoding.UTF8.GetByteCount(json) > 2 * 1024 * 1024)
            throw new InvalidDataException("The hardware profile is empty or exceeds the 2 MB size limit.");
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 24 });
        var root = document.RootElement;
        RequireObject(root, "root");
        CheckKeys(root, "schemaVersion", "sourcePlatform", "device", "cpu", "gpu", "network", "audio", "storage", "notes");
        if (!root.TryGetProperty("schemaVersion", out var version) || version.ValueKind != JsonValueKind.Number || version.GetInt32() != 1)
            throw new InvalidDataException("Only hardware profile schemaVersion 1 is supported.");
        if (!root.TryGetProperty("sourcePlatform", out var source) || source.ValueKind != JsonValueKind.String ||
            source.GetString() is not ("windows" or "macos" or "manual"))
            throw new InvalidDataException("sourcePlatform must be windows, macos or manual.");

        var device = RequiredObject(root, "device");
        CheckKeys(device, "manufacturer", "model", "firmwareVendor", "firmwareVersion", "firmwareMode");
        ValidateOptionalStrings(device, "manufacturer", "model", "firmwareVendor", "firmwareVersion", "firmwareMode");
        if (device.TryGetProperty("firmwareMode", out var mode) && mode.GetString() is not ("UEFI" or "Legacy" or "Unknown"))
            throw new InvalidDataException("device.firmwareMode has an unsupported value.");

        var cpu = RequiredObject(root, "cpu");
        CheckKeys(cpu, "vendor", "model", "family", "architecture", "coreCount");
        ValidateOptionalStrings(cpu, "vendor", "model", "family", "architecture");
        if (cpu.TryGetProperty("coreCount", out var cores) && (cores.ValueKind != JsonValueKind.Number || cores.GetInt32() < 0))
            throw new InvalidDataException("cpu.coreCount must be a non-negative integer.");

        foreach (var item in RequiredArray(root, "gpu").EnumerateArray())
        {
            RequireObject(item, "gpu item");
            CheckKeys(item, "vendor", "model", "pciVendorId", "pciDeviceId", "vramBytes", "connectedDisplays");
            ValidateOptionalStrings(item, "vendor", "model", "pciVendorId", "pciDeviceId");
            if (item.TryGetProperty("vramBytes", out var vram) && (vram.ValueKind != JsonValueKind.Number || vram.GetInt64() < 0))
                throw new InvalidDataException("gpu.vramBytes must be a non-negative integer.");
            if (item.TryGetProperty("connectedDisplays", out var displays)) RequireStringArray(displays, "gpu.connectedDisplays");
        }

        var network = RequiredObject(root, "network");
        CheckKeys(network, "wifiAdapters", "ethernetAdapters", "bluetoothAdapters");
        foreach (string key in new[] { "wifiAdapters", "ethernetAdapters" })
        {
            if (!network.TryGetProperty(key, out var adapters)) continue;
            if (adapters.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException($"network.{key} must be an array.");
            foreach (var item in adapters.EnumerateArray())
            {
                RequireObject(item, key);
                CheckKeys(item, "vendor", "model", "pciVendorId", "pciDeviceId");
                ValidateOptionalStrings(item, "vendor", "model", "pciVendorId", "pciDeviceId");
            }
        }
        if (network.TryGetProperty("bluetoothAdapters", out var bluetooth)) RequireStringArray(bluetooth, "network.bluetoothAdapters");

        var audio = RequiredObject(root, "audio");
        CheckKeys(audio, "codec", "controllers");
        ValidateOptionalStrings(audio, "codec");
        if (audio.TryGetProperty("controllers", out var controllers)) RequireStringArray(controllers, "audio.controllers");

        foreach (var item in RequiredArray(root, "storage").EnumerateArray())
        {
            RequireObject(item, "storage item");
            CheckKeys(item, "model", "controller", "interface", "capacityBytes");
            ValidateOptionalStrings(item, "model", "controller", "interface");
            if (item.TryGetProperty("capacityBytes", out var capacity) && (capacity.ValueKind != JsonValueKind.Number || capacity.GetInt64() < 0))
                throw new InvalidDataException("storage.capacityBytes must be a non-negative integer.");
        }
        if (root.TryGetProperty("notes", out var notes)) RequireStringArray(notes, "notes");
        return "Valid Hardware Profile Schema v1.";
    }

    public static string BuildConfigurationChecklist(string json)
    {
        ValidateProfile(json);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var device = root.GetProperty("device");
        var cpu = root.GetProperty("cpu");
        var network = root.GetProperty("network");
        var audio = root.GetProperty("audio");

        string Value(JsonElement source, string key) =>
            source.TryGetProperty(key, out var property) && property.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(property.GetString())
                ? Clean(property.GetString())
                : "Not reported";
        string Clean(string? value) =>
            string.IsNullOrWhiteSpace(value)
                ? "Not reported"
                : string.Join(" ", value.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)).Trim();
        string Pci(JsonElement source)
        {
            string vendor = Value(source, "pciVendorId");
            string product = Value(source, "pciDeviceId");
            return vendor == "Not reported" && product == "Not reported"
                ? "PCI IDs not reported"
                : $"PCI VEN_{vendor} DEV_{product}";
        }
        string ArraySummary(JsonElement source, string key)
        {
            if (!source.TryGetProperty(key, out var array) || array.ValueKind != JsonValueKind.Array || array.GetArrayLength() == 0)
                return "Not reported";
            return string.Join("; ", array.EnumerateArray()
                .Select(item => item.ValueKind == JsonValueKind.String ? Clean(item.GetString()) : "Not reported"));
        }

        var lines = new List<string>
        {
            "# Guided EFI Preparation Checklist",
            "",
            $"Profile source: {Value(root, "sourcePlatform")}",
            $"Computer: {Value(device, "manufacturer")} / {Value(device, "model")}",
            $"CPU: {Value(cpu, "vendor")} / {Value(cpu, "model")}",
            $"CPU family / architecture: {Value(cpu, "family")} / {Value(cpu, "architecture")}",
            $"Firmware mode reported by Windows: {Value(device, "firmwareMode")}",
            "",
            "> Read-only planning aid. This is not a compatibility verdict and does not generate or modify an EFI.",
            "",
            "## 1. Lock the software versions",
            "- [ ] Choose the exact target macOS version.",
            "- [ ] Choose one OpenCore version. Keep its EFI binaries, Sample.plist, drivers, tools, and ocvalidate on that same version.",
            "- [ ] Confirm every selected Kext and driver supports both the target macOS and the chosen OpenCore version.",
            "",
            "## 2. Verify the CPU",
            $"- Detected CPU: {Value(cpu, "model")}",
            $"- Family / architecture: {Value(cpu, "family")} / {Value(cpu, "architecture")}",
            "- [ ] Confirm the CPU microarchitecture and CPUID family/model using a reliable hardware reference.",
            "- [ ] Review CPU-specific Kernel and Booter requirements for the selected macOS version.",
            "- [ ] Do not enable CPU quirks solely from the processor marketing name.",
            "",
            "## 3. Verify every GPU"
        };

        var gpus = root.GetProperty("gpu");
        if (gpus.GetArrayLength() == 0)
            lines.Add("- No GPU was reported. Inspect Device Manager and collect the exact PCI hardware ID.");
        else
        {
            int index = 0;
            foreach (var gpu in gpus.EnumerateArray())
            {
                index++;
                lines.Add($"- GPU {index}: {Value(gpu, "vendor")} / {Value(gpu, "model")} — {Pci(gpu)}");
            }
        }
        lines.AddRange(new[]
        {
            "- [ ] Check the exact GPU generation and PCI device ID against the target macOS version.",
            "- [ ] For an iGPU, verify framebuffer and connector requirements from the exact platform documentation.",
            "- [ ] For a discrete GPU, confirm native support and version-specific limitations before planning to use it.",
            "",
            "## 4. Verify networking"
        });

        AddNetwork("Wi-Fi", "wifiAdapters");
        AddNetwork("Ethernet", "ethernetAdapters");
        lines.Add("- Bluetooth reported: " + ArraySummary(network, "bluetoothAdapters"));
        lines.Add("- [ ] Identify the wireless chipset by hardware ID; the Windows display name alone is not enough.");
        lines.Add("- [ ] Check official Kext documentation for macOS-version support and required dependencies.");
        lines.Add("- [ ] Review Bluetooth firmware requirements separately from Wi-Fi.");
        lines.Add("");

        lines.Add("## 5. Verify audio");
        lines.Add("- Controllers: " + ArraySummary(audio, "controllers"));
        lines.Add("- Codec: " + Value(audio, "codec"));
        lines.Add("- [ ] Identify the exact codec ID before selecting an AppleALC layout.");
        lines.Add("");

        lines.Add("## 6. Verify storage and firmware");
        var storage = root.GetProperty("storage");
        if (storage.GetArrayLength() == 0)
            lines.Add("- No storage devices were reported.");
        else
        {
            int index = 0;
            foreach (var disk in storage.EnumerateArray())
                lines.Add($"- Storage {++index}: {Value(disk, "model")} — {Value(disk, "interface")} — {Value(disk, "capacityBytes")} bytes");
        }
        lines.Add("- [ ] Identify the actual storage controller and BIOS mode; a Windows disk-interface label does not prove AHCI or RAID mode.");
        lines.Add("- [ ] Review UEFI, Secure Boot, CSM, CFG Lock, and other firmware settings on the target computer before deciding whether any change is appropriate.");
        lines.Add("- [ ] Back up the existing EFI and important files.");
        lines.Add("");

        lines.Add("## 7. Assemble and validate");
        lines.Add("- [ ] Select ACPI tables for this exact board or laptop model; do not copy another computer's ACPI folder.");
        lines.Add("- [ ] Add only required Kexts and their dependencies from official project releases.");
        lines.Add("- [ ] Check enabled ACPI, Kext, and UEFI driver paths against the actual EFI folder.");
        lines.Add("- [ ] Generate unique SMBIOS identifiers privately for the target computer; never copy another person's serials.");
        lines.Add("- [ ] Run the official ocvalidate version matching the config.plist.");
        lines.Add("- [ ] Test from removable media while preserving existing internal boot files.");
        lines.Add("");

        lines.Add("## Information still needed");
        lines.Add("- Exact target macOS and OpenCore versions.");
        lines.Add("- Confirmed CPU CPUID/microarchitecture, GPU IDs, wireless/Bluetooth chipset, Ethernet controller, and audio codec.");
        lines.Add("- Storage-controller mode, required ACPI tables, and relevant firmware settings.");
        lines.Add("");
        lines.Add("## Official references");
        lines.Add("- Dortania OpenCore Install Guide: https://dortania.github.io/OpenCore-Install-Guide/");
        lines.Add("- GPU Buyers Guide: https://dortania.github.io/GPU-Buyers-Guide/");
        lines.Add("- Wireless Buyers Guide: https://dortania.github.io/Wireless-Buyers-Guide/");
        lines.Add("- Acidanthera OpenCore releases: https://github.com/acidanthera/OpenCorePkg/releases");
        lines.Add("");
        lines.Add("A completed checklist is not proof that an EFI will boot. Validate and test each change on the actual target hardware.");
        return string.Join(Environment.NewLine, lines);

        void AddNetwork(string label, string key)
        {
            if (!network.TryGetProperty(key, out var adapters) || adapters.ValueKind != JsonValueKind.Array || adapters.GetArrayLength() == 0)
            {
                lines.Add($"- {label}: no adapter was classified by the current Windows query.");
                return;
            }
            int index = 0;
            foreach (var adapter in adapters.EnumerateArray())
                lines.Add($"- {label} {++index}: {Value(adapter, "vendor")} / {Value(adapter, "model")} — {Pci(adapter)}");
        }
    }

    public static string BuildSummary(string json)
    {
        ValidateProfile(json);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        string Value(JsonElement obj, string key) => obj.TryGetProperty(key, out var prop) && prop.ValueKind == JsonValueKind.String ? prop.GetString()! : "Not reported";
        var device = root.GetProperty("device");
        var cpu = root.GetProperty("cpu");
        return string.Join(Environment.NewLine, new[]
        {
            "Hardware Profile — Schema v1",
            "Source: " + Value(root, "sourcePlatform"),
            "Manufacturer: " + Value(device, "manufacturer"),
            "Model: " + Value(device, "model"),
            "Firmware: " + Value(device, "firmwareVendor") + " / " + Value(device, "firmwareVersion"),
            "CPU: " + Value(cpu, "vendor") + " / " + Value(cpu, "model"),
            "GPU count: " + root.GetProperty("gpu").GetArrayLength().ToString(CultureInfo.InvariantCulture),
            "Storage devices: " + root.GetProperty("storage").GetArrayLength().ToString(CultureInfo.InvariantCulture),
            "Note: This is detected hardware data, not a compatibility verdict."
        });
    }

    private static Dictionary<string, object?> BuildCpu(JsonElement cpu)
    {
        var result = new Dictionary<string, object?>();
        AddString(result, "vendor", cpu, "Manufacturer");
        AddString(result, "model", cpu, "Name");
        AddString(result, "family", cpu, "Family");
        if (cpu.TryGetProperty("Architecture", out var architecture))
        {
            string arch = architecture.ToString() switch { "9" => "x64", "12" => "ARM64", "0" => "x86", _ => architecture.ToString() };
            if (!string.IsNullOrWhiteSpace(arch)) result["architecture"] = arch;
        }
        if (cpu.TryGetProperty("NumberOfCores", out var count) && int.TryParse(count.ToString(), out int cores) && cores >= 0)
            result["coreCount"] = cores;
        return result;
    }

    private static async Task<string> RunPowerShellAsync(string script, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo
        {
            FileName = "powershell.exe", UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-NonInteractive");
        start.ArgumentList.Add("-Command");
        start.ArgumentList.Add(script);
        using var process = new Process { StartInfo = start };
        if (!process.Start()) throw new InvalidOperationException("Could not start Windows PowerShell.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(35));
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            try { process.Kill(true); } catch { }
            cancellationToken.ThrowIfCancellationRequested();
            throw new TimeoutException("Hardware profile collection exceeded 35 seconds.");
        }
        string output = await outputTask;
        string error = await errorTask;
        if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? "Windows returned no hardware profile data." : error.Trim());
        return output;
    }

    private static JsonElement GetObject(JsonElement root, string key)
    {
        if (!root.TryGetProperty(key, out var value) || value.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException($"Windows did not return the '{key}' hardware section.");
        return value;
    }

    private static IEnumerable<JsonElement> GetArray(JsonElement root, string key) =>
        root.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Array ? value.EnumerateArray() : Enumerable.Empty<JsonElement>();

    private static string? GetString(JsonElement root, string key) =>
        root.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static void AddString(Dictionary<string, object?> output, string key, JsonElement source, string property)
    {
        string? value = GetString(source, property);
        if (!string.IsNullOrWhiteSpace(value)) output[key] = value.Trim();
    }

    private static string VendorName(string id) => id.ToUpperInvariant() switch
    {
        "8086" => "Intel", "10DE" => "NVIDIA", "1002" => "AMD", "1022" => "AMD",
        "14E4" => "Broadcom", "168C" => "Qualcomm Atheros", "1969" => "Qualcomm Atheros",
        "10EC" => "Realtek", _ => "PCI " + id.ToUpperInvariant()
    };

    private static string? InferVendor(string value)
    {
        if (value.Contains("Intel", StringComparison.OrdinalIgnoreCase)) return "Intel";
        if (value.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase)) return "NVIDIA";
        if (value.Contains("AMD", StringComparison.OrdinalIgnoreCase) || value.Contains("Radeon", StringComparison.OrdinalIgnoreCase)) return "AMD";
        if (value.Contains("Realtek", StringComparison.OrdinalIgnoreCase)) return "Realtek";
        if (value.Contains("Broadcom", StringComparison.OrdinalIgnoreCase)) return "Broadcom";
        return null;
    }

    private static void RequireObject(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object) throw new InvalidDataException($"{name} must be an object.");
    }

    private static JsonElement RequiredObject(JsonElement parent, string key)
    {
        if (!parent.TryGetProperty(key, out var value)) throw new InvalidDataException($"Required object '{key}' is missing.");
        RequireObject(value, key);
        return value;
    }

    private static JsonElement RequiredArray(JsonElement parent, string key)
    {
        if (!parent.TryGetProperty(key, out var value) || value.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException($"Required array '{key}' is missing or invalid.");
        return value;
    }

    private static void CheckKeys(JsonElement element, params string[] allowed)
    {
        var set = new HashSet<string>(allowed, StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
            if (!set.Contains(property.Name)) throw new InvalidDataException($"Unsupported schema property: {property.Name}");
    }

    private static void ValidateOptionalStrings(JsonElement item, params string[] names)
    {
        foreach (string name in names)
            if (item.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.String)
                throw new InvalidDataException($"{name} must be a string.");
    }

    private static void RequireStringArray(JsonElement value, string name)
    {
        if (value.ValueKind != JsonValueKind.Array) throw new InvalidDataException($"{name} must be an array of strings.");
        foreach (var item in value.EnumerateArray())
            if (item.ValueKind != JsonValueKind.String) throw new InvalidDataException($"{name} must contain only strings.");
    }
}
