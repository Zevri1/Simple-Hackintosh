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
