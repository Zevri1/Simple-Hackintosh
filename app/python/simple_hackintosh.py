#!/usr/bin/env python3
"""Simple Hackintosh CLI: local, read-only OpenCore and hardware diagnostics."""
from __future__ import annotations

import argparse
import hashlib
import json
import os
import platform
import plistlib
import re
import shutil
import subprocess
import sys
import tempfile
import textwrap
import urllib.error
import urllib.parse
import urllib.request
import zipfile
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Any, Iterable

APP_VERSION = "2.4.0"
SECTIONS = (
    "ACPI", "Booter", "DeviceProperties", "Kernel",
    "Misc", "NVRAM", "PlatformInfo", "UEFI",
)
EXPECTED_TYPES: dict[str, dict[str, type]] = {
    "ACPI": {"Add": list, "Delete": list, "Patch": list, "Quirks": dict},
    "Booter": {"MmioWhitelist": list, "Patch": list, "Quirks": dict},
    "DeviceProperties": {"Add": dict, "Delete": dict},
    "Kernel": {
        "Add": list, "Block": list, "Emulate": dict, "Force": list,
        "Patch": list, "Quirks": dict,
    },
    "Misc": {
        "BlessOverride": list, "Boot": dict, "Debug": dict, "Entries": list,
        "Security": dict, "Serial": dict, "Tools": list,
    },
    "NVRAM": {"Add": dict, "Delete": dict, "LegacySchema": dict, "WriteFlash": bool},
    "PlatformInfo": {
        "Automatic": bool, "Generic": dict, "UpdateDataHub": bool,
        "UpdateNVRAM": bool, "UpdateSMBIOS": bool,
    },
    "UEFI": {
        "APFS": dict, "Audio": dict, "Drivers": list, "Input": dict,
        "Output": dict, "Quirks": dict,
    },
}


@dataclass
class Finding:
    level: str
    message: str
    code: str = ""


def _safe_archive_name(name: str) -> str | None:
    """Normalize a relative path and reject absolute or traversal paths."""
    value = name.replace("\\", "/")
    if value.startswith("/") or (len(value) >= 2 and value[1] == ":"):
        return None
    parts = [part for part in value.split("/") if part not in ("", ".")]
    if any(part == ".." for part in parts):
        return None
    return "/".join(parts)


def _relative_join(*parts: str) -> str:
    return "/".join(part.strip("/") for part in parts if part.strip("/")).casefold()


def _read_source(source: Path) -> tuple[dict[str, str], bytes | None, str | None]:
    """Return case-folded relative paths and config bytes, without extracting ZIPs."""
    if not source.exists():
        return {}, None, f"Path does not exist: {source}"
    entries: dict[str, str] = {}
    config_bytes: bytes | None = None

    if source.is_dir():
        base = source.resolve()
        for item in source.rglob("*"):
            try:
                resolved = item.resolve()
                resolved.relative_to(base)
            except (OSError, ValueError):
                continue
            if not item.is_file():
                continue
            rel = item.relative_to(source).as_posix()
            safe = _safe_archive_name(rel)
            if safe is not None:
                entries[safe.casefold()] = safe
        candidates = [
            original for key, original in entries.items()
            if key == "oc/config.plist" or key.endswith("/oc/config.plist")
        ]
        if candidates:
            chosen = sorted(candidates, key=len)[0]
            try:
                config_bytes = (source / Path(*chosen.split("/"))).read_bytes()
            except OSError as exc:
                return entries, None, f"Could not read {chosen}: {exc}"
        return entries, config_bytes, None

    if source.is_file() and zipfile.is_zipfile(source):
        try:
            with zipfile.ZipFile(source) as archive:
                for info in archive.infolist():
                    if info.is_dir():
                        continue
                    safe = _safe_archive_name(info.filename)
                    if safe is not None:
                        entries[safe.casefold()] = info.filename
                candidates = [
                    (key, original) for key, original in entries.items()
                    if key == "oc/config.plist" or key.endswith("/oc/config.plist")
                ]
                if candidates:
                    _, chosen = sorted(candidates, key=lambda pair: len(pair[0]))[0]
                    config_bytes = archive.read(chosen)
        except (OSError, zipfile.BadZipFile, KeyError) as exc:
            return {}, None, f"Could not read ZIP archive: {exc}"
        return entries, config_bytes, None

    return {}, None, "Choose a folder or a ZIP archive. Other archive formats are not supported."


def _read_member(source: Path, entries: dict[str, str], relative_path: str) -> bytes | None:
    original = entries.get(relative_path.casefold())
    if original is None:
        return None
    try:
        if source.is_dir():
            return (source / Path(*original.split("/"))).read_bytes()
        with zipfile.ZipFile(source) as archive:
            return archive.read(original)
    except (OSError, KeyError, zipfile.BadZipFile):
        return None


def validate_config_data(config: Any) -> list[Finding]:
    findings: list[Finding] = []
    if not isinstance(config, dict):
        return [Finding("ERROR", "The plist root must be a dictionary.", "PLIST_ROOT")]

    for section in SECTIONS:
        if section not in config:
            findings.append(Finding("ERROR", f"Missing top-level section: {section}", "SECTION_MISSING"))
            continue
        section_data = config[section]
        if not isinstance(section_data, dict):
            findings.append(Finding("ERROR", f"{section} must be a dictionary.", "SECTION_TYPE"))
            continue
        for key, expected in EXPECTED_TYPES.get(section, {}).items():
            if key in section_data and not isinstance(section_data[key], expected):
                findings.append(
                    Finding(
                        "ERROR",
                        f"{section}.{key} must be {expected.__name__}, not {type(section_data[key]).__name__}.",
                        "KEY_TYPE",
                    )
                )

    if not findings:
        findings.append(Finding("PASS", "All required top-level sections and checked container types look valid.", "PLIST_STRUCTURE"))
    return findings


def load_plist_bytes(data: bytes) -> tuple[Any | None, list[Finding]]:
    try:
        return plistlib.loads(data), []
    except (plistlib.InvalidFileException, ValueError, TypeError) as exc:
        return None, [Finding("ERROR", f"Could not parse property list: {exc}", "PLIST_PARSE")]


def _check_config_rows(
    config: dict[str, Any],
    source: Path | None = None,
    entries: dict[str, str] | None = None,
    oc_prefix: str = "",
) -> list[Finding]:
    findings: list[Finding] = []
    entries = entries or {}
    reference_groups = (
        ("ACPI", "Add", "Path", "ACPI"),
        ("Kernel", "Add", "BundlePath", "Kexts"),
        ("UEFI", "Drivers", "Path", "Drivers"),
        ("Misc", "Tools", "Path", "Tools"),
    )
    seen_paths: dict[tuple[str, str], int] = {}
    seen_bundle_ids: dict[str, int] = {}

    for section, key, field, folder in reference_groups:
        section_data = config.get(section, {})
        if not isinstance(section_data, dict):
            continue
        rows = section_data.get(key, [])
        if not isinstance(rows, list):
            continue
        for index, item in enumerate(rows):
            label = f"{section}.{key}[{index}]"
            if not isinstance(item, dict):
                findings.append(Finding("ERROR", f"{label} must be a dictionary.", "ENTRY_TYPE"))
                continue

            enabled = item.get("Enabled")
            if not isinstance(enabled, bool):
                findings.append(Finding("WARN", f"{label}.Enabled should be an explicit Boolean.", "ENTRY_ENABLED"))
            if enabled is False:
                continue

            ref = item.get(field)
            if not isinstance(ref, str) or not ref.strip():
                findings.append(Finding("ERROR", f"{label}.{field} is missing or is not a non-empty string.", "ENTRY_PATH"))
                continue
            safe_ref = _safe_archive_name(ref)
            if safe_ref is None:
                findings.append(Finding("ERROR", f"{label}.{field} contains an unsafe path.", "UNSAFE_PATH"))
                continue

            path_key = (folder, safe_ref.casefold())
            if path_key in seen_paths:
                findings.append(Finding("WARN", f"{label} repeats the path already used by {section}.{key}[{seen_paths[path_key]}].", "DUPLICATE_PATH"))
            else:
                seen_paths[path_key] = index

            if folder == "Kexts" and not safe_ref.casefold().endswith(".kext"):
                findings.append(Finding("WARN", f"{label}.{field} normally points to a .kext bundle.", "KEXT_SUFFIX"))
            if folder == "ACPI" and not safe_ref.casefold().endswith((".aml", ".bin")):
                findings.append(Finding("WARN", f"{label}.{field} normally uses an .aml or .bin file.", "ACPI_SUFFIX"))

            if source is not None and entries:
                expected = _relative_join(oc_prefix, folder, safe_ref)
                found = expected in entries
                if folder == "Kexts" and not found:
                    prefix = expected.rstrip("/") + "/"
                    found = any(entry.startswith(prefix) for entry in entries)
                if found:
                    findings.append(Finding("PASS", f"Found {folder}/{ref}.", "REFERENCE_FOUND"))
                else:
                    findings.append(Finding("WARN", f"Enabled entry refers to a missing path: {folder}/{ref}.", "REFERENCE_MISSING"))

            if folder == "Kexts":
                bundle_id = item.get("BundleIdentifier")
                if isinstance(bundle_id, str) and bundle_id.strip():
                    normalized_id = bundle_id.strip().casefold()
                    if normalized_id in seen_bundle_ids:
                        findings.append(Finding("WARN", f"Duplicate Kext BundleIdentifier: {bundle_id}.", "DUPLICATE_BUNDLE_ID"))
                    else:
                        seen_bundle_ids[normalized_id] = index

                    if source is not None and entries:
                        expected = _relative_join(oc_prefix, folder, safe_ref)
                        info_key = expected + "/contents/info.plist"
                        if info_key in entries:
                            info_bytes = _read_member(source, entries, info_key)
                            if info_bytes:
                                info_data, info_errors = load_plist_bytes(info_bytes)
                                if not info_errors and isinstance(info_data, dict):
                                    actual_id = info_data.get("CFBundleIdentifier")
                                    if isinstance(actual_id, str) and actual_id and actual_id != bundle_id:
                                        findings.append(
                                            Finding(
                                                "WARN",
                                                f"Kext identifier mismatch for {safe_ref}: config.plist and bundle Info.plist differ.",
                                                "KEXT_ID_MISMATCH",
                                            )
                                        )

    return findings


def check_efi_path(source: Path) -> tuple[list[Finding], dict[str, Any] | None]:
    entries, config_bytes, error = _read_source(source)
    if error:
        return [Finding("ERROR", error, "SOURCE_ERROR")], None
    if not entries:
        return [Finding("ERROR", "No files were found in the selected folder/archive.", "EMPTY_SOURCE")], None

    config_candidates = [
        (key, original) for key, original in entries.items()
        if key == "oc/config.plist" or key.endswith("/oc/config.plist")
    ]
    if not config_candidates:
        return [Finding("ERROR", "Could not find OC/config.plist. Select the EFI folder or a ZIP containing it.", "CONFIG_NOT_FOUND")], None

    config_key, config_original = sorted(config_candidates, key=lambda pair: len(pair[0]))[0]
    oc_prefix = config_key[: -len("config.plist")]
    efi_prefix = oc_prefix[: -len("oc/")]
    findings: list[Finding] = []

    expected_files = (
        (_relative_join(efi_prefix, "BOOT/BOOTx64.efi"), "BOOT/BOOTx64.efi"),
        (_relative_join(oc_prefix, "OpenCore.efi"), "OC/OpenCore.efi"),
        (config_key, "OC/config.plist"),
    )
    for key, label in expected_files:
        if key in entries:
            findings.append(Finding("PASS", f"Found {label}.", "REQUIRED_FILE_FOUND"))
        else:
            findings.append(Finding("ERROR", f"Missing required file: {label}.", "REQUIRED_FILE_MISSING"))

    if len(config_candidates) > 1:
        findings.append(Finding("WARN", f"Found {len(config_candidates)} config.plist files; checked the shortest matching path.", "MULTIPLE_CONFIGS"))
    if config_bytes is None:
        findings.append(Finding("ERROR", "The config.plist file could not be read.", "CONFIG_READ"))
        return findings, None

    config, parse_findings = load_plist_bytes(config_bytes)
    findings.extend(parse_findings)
    if not isinstance(config, dict):
        return findings, None

    findings.extend(validate_config_data(config))
    findings.extend(_check_config_rows(config, source, entries, oc_prefix))
    summary = {
        "source_name": source.name or "EFI",
        "files_scanned": len(entries),
        "config_path": config_original,
        "findings": [asdict(item) for item in findings],
    }
    return findings, summary


def check_config_file(path: Path) -> tuple[list[Finding], dict[str, Any] | None]:
    if not path.is_file():
        return [Finding("ERROR", f"File not found: {path}", "FILE_NOT_FOUND")], None
    try:
        config, parse_findings = load_plist_bytes(path.read_bytes())
    except OSError as exc:
        return [Finding("ERROR", f"Could not read file: {exc}", "FILE_READ")], None
    if parse_findings:
        return parse_findings, None
    if not isinstance(config, dict):
        return [Finding("ERROR", "The plist root must be a dictionary.", "PLIST_ROOT")], None
    findings = validate_config_data(config)
    findings.extend(_check_config_rows(config))
    return findings, {"file_name": path.name, "findings": [asdict(item) for item in findings]}


def _powershell_executable() -> str | None:
    return shutil.which("powershell") or shutil.which("pwsh")


def collect_hardware() -> dict[str, Any]:
    if platform.system() == "Darwin":
        return collect_hardware_macos()
    if platform.system() != "Windows":
        raise RuntimeError("Hardware inventory is currently supported on Windows and macOS.")
    executable = _powershell_executable()
    if not executable:
        raise RuntimeError("PowerShell was not found. It is normally included with Windows.")

    script = r"""
$ErrorActionPreference = 'Stop'
$cpu = Get-CimInstance Win32_Processor | Select-Object -First 1
$gpus = @(Get-CimInstance Win32_VideoController | ForEach-Object {
  [pscustomobject]@{ Name = $_.Name; DriverVersion = $_.DriverVersion; AdapterRAM_GB = if ($_.AdapterRAM) { [math]::Round($_.AdapterRAM / 1GB, 1) } else { $null } }
})
$board = Get-CimInstance Win32_BaseBoard | Select-Object -First 1
$bios = Get-CimInstance Win32_BIOS | Select-Object -First 1
$os = Get-CimInstance Win32_OperatingSystem | Select-Object -First 1
$computer = Get-CimInstance Win32_ComputerSystem | Select-Object -First 1
$disks = @(Get-CimInstance Win32_DiskDrive | ForEach-Object {
  [pscustomobject]@{ Model = $_.Model; InterfaceType = $_.InterfaceType; SizeGB = if ($_.Size) { [math]::Round($_.Size / 1GB, 1) } else { $null } }
})
$classes = @('Display','Net','Bluetooth','MEDIA','SCSIAdapter','HDC','System')
$devices = @(Get-CimInstance Win32_PnPEntity | Where-Object {
  ($_.PNPClass -in $classes) -or ($_.HardwareID -match 'PCI\\VEN_')
} | ForEach-Object {
  $ids = @($_.HardwareID | Where-Object { $_ } | Select-Object -First 4)
  [pscustomobject]@{
    Name = $_.Name
    Class = $_.PNPClass
    Manufacturer = $_.Manufacturer
    Status = $_.Status
    DriverVersion = $_.DriverVersion
    HardwareIDs = $ids
  }
} | Sort-Object Class, Name)
[pscustomobject]@{
  HostPlatform = 'Windows'
  Architecture = $env:PROCESSOR_ARCHITECTURE
  ComputerModel = $computer.Model
  CPU = $cpu.Name
  CPUCores = $cpu.NumberOfCores
  LogicalProcessors = $cpu.NumberOfLogicalProcessors
  GPUs = $gpus
  Motherboard = (($board.Manufacturer, $board.Product) -join ' ').Trim()
  BIOS = (($bios.Manufacturer, $bios.SMBIOSBIOSVersion) -join ' ').Trim()
  Windows = $os.Caption
  OS = $os.Caption
  WindowsVersion = $os.Version
  MemoryGB = [math]::Round($computer.TotalPhysicalMemory / 1GB, 1)
  Disks = $disks
  Devices = $devices
} | ConvertTo-Json -Depth 6 -Compress
"""
    process = subprocess.run(
        [executable, "-NoProfile", "-NonInteractive", "-Command", script],
        capture_output=True, text=True, encoding="utf-8", errors="replace",
        timeout=60, check=False,
    )
    if process.returncode != 0:
        detail = (process.stderr or process.stdout).strip()
        raise RuntimeError(f"PowerShell hardware query failed: {detail or 'unknown error'}")
    output = process.stdout.strip().lstrip("\ufeff")
    if not output:
        raise RuntimeError("PowerShell returned an empty hardware report.")
    try:
        result = json.loads(output)
    except json.JSONDecodeError as exc:
        raise RuntimeError(f"Could not parse PowerShell JSON output: {exc}") from exc
    if not isinstance(result, dict):
        raise RuntimeError("PowerShell returned an unexpected hardware data format.")
    result["Devices"] = _as_list(result.get("Devices"))
    result["GPUs"] = _as_list(result.get("GPUs"))
    result["Disks"] = _as_list(result.get("Disks"))
    result["macOSCompatibility"] = estimate_macos_compatibility(result)
    return result



def _mac_walk_dicts(value: Any) -> Iterable[dict[str, Any]]:
    if isinstance(value, dict):
        yield value
        for child in value.values():
            yield from _mac_walk_dicts(child)
    elif isinstance(value, list):
        for child in value:
            yield from _mac_walk_dicts(child)


def _mac_scalar(node: dict[str, Any], *keys: str) -> str | None:
    wanted = {key.casefold() for key in keys}
    for key, value in node.items():
        if key.casefold() in wanted and isinstance(value, (str, int, float)):
            rendered = str(value).strip()
            if rendered:
                return rendered
    return None


def _mac_memory_gb(value: Any) -> float | None:
    if isinstance(value, (int, float)):
        number = float(value)
        return round(number / (1024 ** 3), 1) if number > 1024 ** 2 else round(number, 1)
    if not isinstance(value, str):
        return None
    match = re.search(r"(\d+(?:\.\d+)?)\s*(TB|GB|MB)", value, re.IGNORECASE)
    if not match:
        return None
    number = float(match.group(1))
    unit = match.group(2).upper()
    if unit == "TB":
        number *= 1024
    elif unit == "MB":
        number /= 1024
    return round(number, 1)


def _mac_hex(value: str | None) -> str | None:
    if value is None:
        return None
    match = re.search(r"(?:0x)?([0-9a-f]{4})", value, re.IGNORECASE)
    return match.group(1).upper() if match else None


def parse_macos_profiler_data(
    profiler: dict[str, Any],
    os_version: str = "",
    architecture: str | None = None,
) -> dict[str, Any]:
    """Convert system_profiler JSON into a small privacy-conscious hardware report."""
    hardware = next(iter(_as_list(profiler.get("SPHardwareDataType"))), {})
    if not isinstance(hardware, dict):
        hardware = {}

    cpu = _mac_scalar(
        hardware,
        "chip_type", "cpu_type", "processor_name", "machine_processor",
    ) or "Not reported"
    model_name = _mac_scalar(hardware, "machine_name", "machine_model") or "Mac"
    model_id = _mac_scalar(hardware, "machine_model", "model_identifier")
    computer_model = f"{model_name} ({model_id})" if model_id and model_id not in model_name else model_name
    core_value = _mac_scalar(hardware, "total_number_cores", "number_of_cores", "processor_cores")
    core_match = re.search(r"\d+", core_value or "")
    cores = int(core_match.group()) if core_match else None
    memory = _mac_scalar(hardware, "physical_memory", "memory", "machine_memory")

    gpu_names: list[str] = []
    gpus: list[dict[str, Any]] = []
    for node in _mac_walk_dicts(profiler.get("SPDisplaysDataType", [])):
        name = _mac_scalar(node, "sppci_model", "spdisplays_model", "chipset_model", "_name")
        if not name:
            continue
        if not re.search(r"intel|amd|radeon|nvidia|geforce|uhd|iris|graphics|apple\s*m\d", name, re.IGNORECASE):
            continue
        if name.casefold() in {existing.casefold() for existing in gpu_names}:
            continue
        gpu_names.append(name)
        gpus.append({
            "Name": name,
            "DriverVersion": _mac_scalar(node, "spdisplays_driver", "driver_version"),
            "AdapterRAM_GB": _mac_memory_gb(_mac_scalar(node, "spdisplays_vram", "vram")),
        })

    devices: list[dict[str, Any]] = []
    seen_devices: set[tuple[str, str]] = set()
    for node in _mac_walk_dicts(profiler.get("SPPCIDataType", [])):
        name = _mac_scalar(node, "sppci_model", "spci_model", "model", "_name", "name")
        if not name:
            continue
        vendor = _mac_hex(_mac_scalar(node, "vendor-id", "sppci_vendor-id", "spci_vendor-id", "vendor_id"))
        device_id = _mac_hex(_mac_scalar(node, "device-id", "sppci_device-id", "spci_device-id", "device_id"))
        hardware_ids = [f"VEN_{vendor}&DEV_{device_id}"] if vendor and device_id else []
        key = (name.casefold(), hardware_ids[0] if hardware_ids else "")
        if key in seen_devices:
            continue
        seen_devices.add(key)
        devices.append({
            "Name": name,
            "Class": "Display" if re.search(r"graphics|display|radeon|geforce|uhd|iris", name, re.IGNORECASE) else "PCI",
            "Manufacturer": _mac_scalar(node, "sppci_vendor", "spci_vendor", "vendor"),
            "Status": "Reported by system_profiler",
            "HardwareIDs": hardware_ids,
        })

    for gpu in gpus:
        if not any(device.get("Name", "").casefold() == gpu["Name"].casefold() for device in devices):
            devices.append({
                "Name": gpu["Name"],
                "Class": "Display",
                "Manufacturer": None,
                "Status": "Reported by system_profiler",
                "HardwareIDs": [],
            })

    disks: list[dict[str, Any]] = []
    seen_disks: set[str] = set()
    for node in _mac_walk_dicts(profiler.get("SPStorageDataType", [])):
        name = _mac_scalar(node, "physical_drive", "device_model", "spstorage_name", "spsata_model", "spnvme_model", "_name")
        if not name:
            continue
        if not any(key.casefold() in {"bsd_name", "physical_drive", "device_model", "spstorage_name", "spsata_model", "spnvme_model"} for key in node):
            continue
        if name.casefold() in seen_disks:
            continue
        seen_disks.add(name.casefold())
        capacity = _mac_scalar(node, "size", "capacity", "physical_drive_size", "spstorage_capacity")
        disks.append({"Model": name, "InterfaceType": "Apple storage", "SizeGB": _mac_memory_gb(capacity)})

    firmware = _mac_scalar(hardware, "boot_rom_version", "system_firmware_version", "firmware_version")
    return {
        "HostPlatform": "Darwin",
        "Architecture": architecture or platform.machine(),
        "ComputerModel": computer_model,
        "CPU": cpu,
        "CPUCores": cores,
        "LogicalProcessors": None,
        "GPUs": gpus,
        "Motherboard": "Apple logic board",
        "BIOS": firmware or "Apple firmware (version not reported)",
        "OS": os_version or platform.mac_ver()[0] or "macOS version not reported",
        "MemoryGB": _mac_memory_gb(memory),
        "Disks": disks,
        "Devices": devices,
    }


def collect_hardware_macos() -> dict[str, Any]:
    if platform.system() != "Darwin":
        raise RuntimeError("macOS hardware inventory can only run on macOS.")
    profiler = shutil.which("system_profiler")
    if not profiler:
        raise RuntimeError("system_profiler was not found. It is included with macOS.")

    try:
        types_result = subprocess.run(
            [profiler, "-listDataTypes"],
            capture_output=True, text=True, encoding="utf-8", errors="replace",
            timeout=30, check=False,
        )
        available = set(re.findall(r"\bSP[A-Za-z0-9]+DataType\b", types_result.stdout or ""))
        wanted = [
            "SPHardwareDataType", "SPDisplaysDataType", "SPPCIDataType",
            "SPStorageDataType", "SPNVMeDataType", "SPMemoryDataType",
        ]
        data_types = [item for item in wanted if item in available]
        if not data_types:
            data_types = ["SPHardwareDataType", "SPDisplaysDataType"]

        result = subprocess.run(
            [profiler, "-json", "-detailLevel", "mini", *data_types],
            capture_output=True, text=True, encoding="utf-8", errors="replace",
            timeout=120, check=False,
        )
    except (OSError, subprocess.TimeoutExpired) as exc:
        raise RuntimeError(f"Could not run system_profiler: {exc}") from exc

    if result.returncode != 0:
        detail = (result.stderr or result.stdout).strip()
        raise RuntimeError(f"system_profiler failed: {detail or 'unknown error'}")
    try:
        profile_data = json.loads(result.stdout.lstrip("\ufeff"))
    except json.JSONDecodeError as exc:
        raise RuntimeError(f"Could not parse system_profiler JSON: {exc}") from exc
    if not isinstance(profile_data, dict):
        raise RuntimeError("system_profiler returned an unexpected data format.")

    version = platform.mac_ver()[0]
    sw_vers = shutil.which("sw_vers")
    if sw_vers:
        try:
            version_result = subprocess.run(
                [sw_vers, "-productVersion"], capture_output=True,
                text=True, encoding="utf-8", errors="replace", timeout=10, check=False,
            )
            if version_result.returncode == 0 and version_result.stdout.strip():
                version = version_result.stdout.strip()
        except (OSError, subprocess.TimeoutExpired):
            pass

    data = parse_macos_profiler_data(profile_data, version, platform.machine())
    data["macOSCompatibility"] = estimate_macos_compatibility(data)
    return data

def _as_list(value: Any) -> list[Any]:
    if value is None:
        return []
    return value if isinstance(value, list) else [value]


def _pci_ids(hardware_ids: Any) -> list[str]:
    matches: list[str] = []
    for raw in _as_list(hardware_ids):
        if not isinstance(raw, str):
            continue
        match = re.search(r"VEN_([0-9A-F]{4})&DEV_([0-9A-F]{4})", raw, re.IGNORECASE)
        if match:
            value = f"VEN_{match.group(1).upper()}&DEV_{match.group(2).upper()}"
            if value not in matches:
                matches.append(value)
    return matches


def _device_category(device: dict[str, Any]) -> str:
    label = f"{device.get('Class', '')} {device.get('Name', '')}".casefold()
    if "bluetooth" in label:
        return "Bluetooth"
    if "net" in label or "wi-fi" in label or "wireless" in label or "ethernet" in label:
        return "Network"
    if "display" in label or "graphics" in label or "video" in label:
        return "Graphics"
    if "media" in label or "audio" in label or "sound" in label:
        return "Audio"
    if "scsi" in label or "storage" in label or "sata" in label or "nvme" in label:
        return "Storage"
    return "Other"




def estimate_macos_compatibility(data: dict[str, Any]) -> dict[str, Any]:
    """Return a conservative, evidence-linked macOS hardware estimate.

    This is a rules-based hint, not a bootability test. It intentionally only
    gives a version ceiling for Intel iGPUs covered by the cited community guide.
    """
    cpu_name = str(data.get("CPU") or "")
    if data.get("HostPlatform") == "Darwin":
        running_os = str(data.get("OS") or "macOS (version not reported)")
        apple_silicon = bool(re.search(r"Apple\s*M[1-9][^,;]*", cpu_name, re.IGNORECASE) or data.get("Architecture") == "arm64")
        return {
            "status": "native-mac",
            "target": f"{running_os} is currently running on this Mac",
            "confidence": "Confirmed current running OS only; maximum supported version is not inferred",
            "gpu_family": "; ".join(str(item.get("Name") or "") for item in _as_list(data.get("GPUs")) if isinstance(item, dict)) or "Graphics adapter not reported",
            "experimental_target": None,
            "notes": [
                "This is a Mac hardware inventory, so the report confirms the macOS version that is running now.",
                "It does not infer the maximum macOS release supported by the model identifier.",
                "Apple Silicon is identified." if apple_silicon else "Intel Mac hardware is identified; model-specific support limits require a separate Apple model lookup.",
            ],
            "sources": [
                {"title": "Apple — Use System Information to identify your Mac", "url": "https://support.apple.com/en-us/102767"},
            ],
            "limitations": [
                "This is not a maximum-version support check or an installer/boot test.",
                "The report avoids collecting serial number and hardware UUID fields from system_profiler.",
            ],
        }
    cpu_match = re.search(r"\bi[3579]\s*-\s*(\d{4,5})([A-Z]{1,3})?", cpu_name, re.IGNORECASE)
    generation: int | None = None
    if cpu_match:
        model_number = cpu_match.group(1)
        if model_number.startswith(("10", "11", "12", "13", "14", "15", "16")):
            generation = int(model_number[:2])
        else:
            generation = int(model_number[0])

    gpu_entries = [item for item in _as_list(data.get("GPUs")) if isinstance(item, dict)]
    gpu_names = [str(item.get("Name") or "") for item in gpu_entries if item.get("Name")]
    graphics_devices = [
        item for item in _as_list(data.get("Devices"))
        if isinstance(item, dict) and _device_category(item) == "Graphics"
    ]
    gpu_names.extend(str(item.get("Name") or "") for item in graphics_devices if item.get("Name"))
    gpu_names = list(dict.fromkeys(name for name in gpu_names if name.strip()))

    gpu_ids: list[str] = []
    for device in graphics_devices:
        for pci_id in _pci_ids(device.get("HardwareIDs")):
            if pci_id not in gpu_ids:
                gpu_ids.append(pci_id)
    device_codes = {item.split("&DEV_")[-1] for item in gpu_ids if "&DEV_" in item}
    gpu_text = " ".join(gpu_names).casefold()
    intel_detected = "intel" in gpu_text or any(item.startswith("VEN_8086&") for item in gpu_ids)

    sources = [
        {
            "title": "Dortania — Intel GPU Buyers Guide",
            "url": "https://dortania.github.io/GPU-Buyers-Guide/modern-gpus/intel-gpu.html",
        },
        {
            "title": "Dortania — OpenCore hardware limitations and CPU support",
            "url": "https://dortania.github.io/OpenCore-Install-Guide/macos-limits.html",
        },
        {
            "title": "Dortania — macOS 26 Tahoe notes",
            "url": "https://dortania.github.io/OpenCore-Install-Guide/extras/tahoe.html",
        },
    ]
    base = {
        "cpu": cpu_name or "Not reported",
        "cpu_generation": generation,
        "gpu_names": gpu_names,
        "gpu_pci_ids": gpu_ids,
        "sources": sources,
        "limitations": [
            "This estimate does not test booting or validate an EFI.",
            "Wi-Fi, Bluetooth, audio, sleep, USB mapping, storage, BIOS settings, ACPI and display routing need separate checks.",
            "A discrete GPU may change the result; the estimator does not automatically decide whether it can replace or disable an unsupported iGPU.",
        ],
    }

    unsupported_intel_gpu = bool(re.search(
        r"\b(?:UHD(?:\s+Graphics)?\s*(?:600|605|610)|HD(?:\s+Graphics)?\s*(?:400|405|500|505|510)|Iris\s*Xe(?:\s+Graphics)?|Intel\s*Arc|Arc\s+Graphics)\b",
        gpu_text,
        re.IGNORECASE,
    ))
    unsupported_intel_ids = bool(device_codes.intersection({"9B21", "3E90", "3E93", "3E99", "3E9C", "9BA5", "9BA8"}))
    supported_intel_names = bool(re.search(
        r"\b(?:UHD(?:\s+Graphics)?\s*(?:615|617|620|630)|HD(?:\s+Graphics)?\s*(?:515|520|530|615|620|630)|Iris(?:\s+Plus)?(?:\s+Graphics)?\s*(?:540|550|580|640|645|650|655))\b",
        gpu_text,
        re.IGNORECASE,
    ))
    supported_comet_ids = bool(device_codes.intersection({
        "9B41", "9BAC", "9BCA", "9BCC", "9BC8", "9BC5",
        "3E92", "3E9B", "3EA0", "3EA9", "3EA6", "3E94",
        "9BC6", "9BE6", "9BF6", "5917", "5926", "5927",
    }))

    if intel_detected and (unsupported_intel_gpu or unsupported_intel_ids):
        return {
            **base,
            "status": "limited",
            "target": "No reliable hardware-accelerated macOS target from this Intel iGPU alone",
            "confidence": "High for the detected iGPU limitation",
            "gpu_family": "Intel iGPU family listed as unsupported in the GPU Buyers Guide",
            "experimental_target": None,
            "notes": [
                "The detected Intel integrated GPU model or device ID is in a family that the cited GPU guide lists as unsupported.",
                "This does not assess a separate compatible discrete GPU.",
            ],
        }

    if intel_detected and (supported_intel_names or supported_comet_ids):
        if generation is not None and generation > 10:
            return {
                **base,
                "status": "limited",
                "target": "No conservative recommendation: CPU generation is newer than the cited Intel laptop support range",
                "confidence": "Medium",
                "gpu_family": "Intel integrated graphics identified, but the CPU generation is outside the guide's documented laptop range",
                "experimental_target": None,
                "notes": [
                    "The OpenCore guide documents Intel Core i/Xeon laptop CPUs through Ice Lake; newer CPU generations need separate, current evidence.",
                ],
            }

        family = "Intel integrated graphics (supported UHD/HD/Iris family)"
        if "9B41" in device_codes and generation == 10:
            family = "Intel Comet Lake-U UHD Graphics (PCI device 0x9B41)"
        elif re.search(r"UHD\s*630", gpu_text, re.IGNORECASE) or device_codes.intersection({"3E92", "3E9B", "9BC8", "9BC5"}):
            family = "Intel UHD Graphics 630 family"
        elif re.search(r"UHD\s*620", gpu_text, re.IGNORECASE) or device_codes.intersection({"3EA0", "3EA9", "5917"}):
            family = "Intel UHD Graphics 620 family"

        return {
            **base,
            "status": "documented-baseline",
            "target": "macOS Sequoia 15 (conservative documented graphics ceiling)",
            "confidence": "Medium — GPU family recognised; whole-system compatibility is not tested",
            "gpu_family": family,
            "experimental_target": "macOS Tahoe 26 — experimental; verify the exact model, SMBIOS and required patches separately",
            "notes": [
                "The Intel GPU guide lists the recognised UHD/HD/Iris families in this generation group through macOS Sequoia 15.",
                "The Intel laptop CPU guide includes supported generations through Ice Lake. Exact CPU family and laptop implementation still matter.",
                "Tahoe 26 is not counted as the conservative ceiling: its guide documents a narrower SMBIOS set and additional hardware caveats.",
                "Framebuffer configuration and Lilu/WhateverGreen requirements vary by GPU and laptop.",
            ],
        }

    if intel_detected and re.search(r"\bHD\s*4000\b", gpu_text, re.IGNORECASE):
        return {
            **base,
            "status": "documented-baseline",
            "target": "macOS Big Sur 11 (conservative graphics ceiling)",
            "confidence": "Medium — Intel HD 4000 family recognised",
            "gpu_family": "Intel HD Graphics 4000 / Ivy Bridge",
            "experimental_target": None,
            "notes": [
                "The cited GPU guide says Intel HD 4000 support was dropped starting with macOS Monterey.",
                "The CPU, audio, networking and remaining components still require separate checks.",
            ],
        }

    return {
        **base,
        "status": "unknown",
        "target": "Insufficient hardware data for a reliable macOS version estimate",
        "confidence": "Low",
        "gpu_family": "; ".join(gpu_names) if gpu_names else "Graphics adapter not identified",
        "experimental_target": None,
        "notes": [
            "The detected GPU does not match a sufficiently specific rule in this estimator.",
            "Check the exact GPU model and PCI device ID against the current GPU Buyers Guide before choosing a macOS version.",
        ],
    }


def hardware_report_markdown(data: dict[str, Any]) -> str:
    compatibility = data.get("macOSCompatibility")
    if not isinstance(compatibility, dict):
        compatibility = estimate_macos_compatibility(data)

    lines = [
        "# Simple Hackintosh — Hardware inventory",
        "",
        "> Generated locally. Serial numbers, MAC addresses, UUIDs, and account identifiers are not collected.",
        "",
        "| Component | Detected value |",
        "|---|---|",
    ]
    fields = (
        ("Computer model", data.get("ComputerModel")),
        ("CPU", data.get("CPU")),
        ("CPU cores / threads", f"{data.get('CPUCores', '?')} / {data.get('LogicalProcessors', '?')}"),
        ("Motherboard", data.get("Motherboard")),
        ("Firmware / BIOS", data.get("BIOS")),
        ("Operating system", data.get("OS") or data.get("Windows")),
        ("OS build", data.get("WindowsVersion")),
        ("Memory", f"{data.get('MemoryGB')} GB" if data.get("MemoryGB") is not None else None),
        ("GPUs", "; ".join(
            f"{gpu.get('Name', 'Unknown')} (driver {gpu.get('DriverVersion') or 'unknown'})"
            for gpu in data.get("GPUs", []) if isinstance(gpu, dict)
        )),
        ("Disks", "; ".join(
            f"{disk.get('Model', 'Unknown')} ({disk.get('SizeGB', '?')} GB, {disk.get('InterfaceType') or 'interface unknown'})"
            for disk in data.get("Disks", []) if isinstance(disk, dict)
        )),
    )
    for label, value in fields:
        text = str(value).strip() if value is not None else "Not reported"
        lines.append(f"| {label} | {text.replace('|', '/')} |")

    lines.extend([
        "",
        "## macOS compatibility estimate",
        "",
        f"- **Conservative target:** {compatibility.get('target', 'Unknown')}",
        f"- **Confidence:** {compatibility.get('confidence', 'Not available')}",
        f"- **Detected graphics family:** {compatibility.get('gpu_family', 'Unknown')}",
    ])
    if compatibility.get("experimental_target"):
        lines.append(f"- **Experimental possibility:** {compatibility['experimental_target']}")
    if compatibility.get("notes"):
        lines.extend(["", "**Why this estimate was made:**"])
        lines.extend(f"- {note}" for note in compatibility["notes"])
    if compatibility.get("limitations"):
        lines.extend(["", "**Not checked by this estimate:**"])
        lines.extend(f"- {note}" for note in compatibility["limitations"])
    if compatibility.get("sources"):
        lines.extend(["", "**Reference guides:**"])
        lines.extend(f"- [{item['title']}]({item['url']})" for item in compatibility["sources"])

    lines.extend(["", "## PCI and device inventory", "", "| Category | Device | PCI IDs | Status |", "|---|---|---|---|"])
    devices = [item for item in data.get("Devices", []) if isinstance(item, dict)]
    for item in devices:
        ids = ", ".join(_pci_ids(item.get("HardwareIDs"))) or "No PCI ID reported"
        name = str(item.get("Name") or "Unknown").replace("|", "/")
        status = str(item.get("Status") or "Unknown").replace("|", "/")
        lines.append(f"| {_device_category(item)} | {name} | {ids} | {status} |")
    if not devices:
        lines.append("| — | No matching Plug and Play devices were returned | — | — |")
    lines.extend([
        "",
        "This is a best-effort community-guide estimate for Windows-hosted hardware; on macOS hosts it confirms only the OS currently running, not the maximum supported release.",
        "",
    ])
    return "\n".join(lines)

def _print_findings(findings: Iterable[Finding]) -> tuple[int, int, int]:
    counts = {"PASS": 0, "WARN": 0, "ERROR": 0}
    for finding in findings:
        counts[finding.level] = counts.get(finding.level, 0) + 1
        suffix = f" ({finding.code})" if finding.code else ""
        print(f"[{finding.level}]{suffix} {finding.message}")
    print()
    print(f"Summary: {counts['PASS']} passed, {counts['WARN']} warning(s), {counts['ERROR']} error(s)")
    return counts["PASS"], counts["WARN"], counts["ERROR"]


def _emit_result(data: Any, output: str | None = None, as_json: bool = False) -> None:
    if as_json:
        rendered = json.dumps(data, indent=2, ensure_ascii=False, default=str)
    else:
        rendered = str(data)
    if output:
        destination = Path(output).expanduser()
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_text(rendered + ("" if rendered.endswith("\n") else "\n"), encoding="utf-8")
        print(f"Saved: {destination.resolve()}")
    else:
        print(rendered)


def command_doctor() -> int:
    print("Simple Hackintosh CLI")
    print(f"Version: {APP_VERSION}")
    print(f"OS: {platform.system()} {platform.release()} ({platform.machine()})")
    print(f"Python: {platform.python_version()}")
    print(f"PowerShell: {_powershell_executable() or 'not found'}")
    print(f"ocvalidate on PATH: {shutil.which('ocvalidate') or shutil.which('ocvalidate.exe') or 'not found (optional)'}")
    print("Dependencies: Python standard library only")
    if sys.version_info < (3, 10):
        print("[ERROR] Python 3.10 or newer is required.")
        return 1
    print("[PASS] Runtime requirements are satisfied for EFI/plist checks.")
    if platform.system() == "Windows" and _powershell_executable():
        print("[PASS] Windows hardware inventory tools are available.")
    elif platform.system() != "Windows":
        print("[INFO] Hardware inventory is Windows-only; EFI checks work here.")
    print("[INFO] All built-in operations are read-only.")
    return 0


HARDWARE_SNIFFER_RELEASE_API = "https://api.github.com/repos/lzhoang2801/Hardware-Sniffer/releases/latest"
HARDWARE_SNIFFER_ASSET_NAME = "Hardware-Sniffer.exe"
HARDWARE_SNIFFER_MAX_BYTES = 32 * 1024 * 1024


def _hardware_sniffer_cache_dir() -> Path:
    local_data = os.environ.get("LOCALAPPDATA")
    base = Path(local_data).expanduser() if local_data else Path.home() / ".cache"
    return base / "SimpleHackintosh" / "tools"


def _sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _download_hardware_sniffer() -> tuple[Path, str]:
    """Download the official interactive Hardware Sniffer release and verify its GitHub digest."""
    metadata_request = urllib.request.Request(
        HARDWARE_SNIFFER_RELEASE_API,
        headers={
            "Accept": "application/vnd.github+json",
            "User-Agent": f"Simple-Hackintosh/{APP_VERSION}",
        },
    )
    try:
        with urllib.request.urlopen(metadata_request, timeout=25) as response:
            metadata_bytes = response.read(2 * 1024 * 1024 + 1)
        if len(metadata_bytes) > 2 * 1024 * 1024:
            raise RuntimeError("Hardware Sniffer release metadata is unexpectedly large.")
        release = json.loads(metadata_bytes.decode("utf-8"))
    except (urllib.error.URLError, TimeoutError, OSError, UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise RuntimeError(f"Could not retrieve the official Hardware Sniffer release information: {exc}") from exc

    if not isinstance(release, dict) or release.get("draft") or release.get("prerelease"):
        raise RuntimeError("GitHub did not return a published stable Hardware Sniffer release.")
    tag = str(release.get("tag_name") or "").strip()
    if not re.fullmatch(r"[A-Za-z0-9._-]{1,40}", tag):
        raise RuntimeError("The Hardware Sniffer release has an invalid version tag.")

    assets = release.get("assets")
    if not isinstance(assets, list):
        raise RuntimeError("The Hardware Sniffer release did not include an asset list.")
    asset = next(
        (item for item in assets if isinstance(item, dict) and item.get("name") == HARDWARE_SNIFFER_ASSET_NAME),
        None,
    )
    if asset is None:
        raise RuntimeError(f"The official release does not contain {HARDWARE_SNIFFER_ASSET_NAME}.")

    digest_value = str(asset.get("digest") or "")
    digest_match = re.fullmatch(r"sha256:([0-9a-fA-F]{64})", digest_value)
    if not digest_match:
        raise RuntimeError("GitHub did not provide a valid SHA-256 digest for Hardware Sniffer.")
    expected_digest = digest_match.group(1).lower()

    download_url = str(asset.get("browser_download_url") or "")
    parsed_url = urllib.parse.urlparse(download_url)
    if (
        parsed_url.scheme != "https"
        or parsed_url.netloc.lower() != "github.com"
        or not parsed_url.path.startswith("/lzhoang2801/Hardware-Sniffer/releases/download/")
    ):
        raise RuntimeError("The Hardware Sniffer download URL is not an official GitHub release asset.")

    expected_size = asset.get("size")
    if not isinstance(expected_size, int) or expected_size <= 0 or expected_size > HARDWARE_SNIFFER_MAX_BYTES:
        raise RuntimeError("The Hardware Sniffer release asset has an unexpected file size.")

    cache_dir = _hardware_sniffer_cache_dir()
    cache_dir.mkdir(parents=True, exist_ok=True)
    destination = cache_dir / f"Hardware-Sniffer-{tag}.exe"
    if destination.is_file():
        try:
            if _sha256_file(destination) == expected_digest:
                return destination, tag
        except OSError:
            pass
        destination.unlink(missing_ok=True)

    download_request = urllib.request.Request(
        download_url,
        headers={
            "Accept": "application/octet-stream",
            "User-Agent": f"Simple-Hackintosh/{APP_VERSION}",
        },
    )
    temporary_path: Path | None = None
    digest = hashlib.sha256()
    total_bytes = 0
    try:
        with urllib.request.urlopen(download_request, timeout=120) as response:
            with tempfile.NamedTemporaryFile(
                prefix=".hardware-sniffer-",
                suffix=".download",
                dir=cache_dir,
                delete=False,
            ) as temporary:
                temporary_path = Path(temporary.name)
                while True:
                    chunk = response.read(1024 * 1024)
                    if not chunk:
                        break
                    total_bytes += len(chunk)
                    if total_bytes > HARDWARE_SNIFFER_MAX_BYTES or total_bytes > expected_size:
                        raise RuntimeError("The Hardware Sniffer download exceeded its declared size.")
                    digest.update(chunk)
                    temporary.write(chunk)
        if total_bytes != expected_size:
            raise RuntimeError(
                f"Hardware Sniffer download size mismatch: expected {expected_size}, received {total_bytes}."
            )
        if digest.hexdigest() != expected_digest:
            raise RuntimeError("Hardware Sniffer SHA-256 verification failed; the executable was not installed.")
        os.replace(temporary_path, destination)
        temporary_path = None
    except (urllib.error.URLError, TimeoutError, OSError) as exc:
        raise RuntimeError(f"Could not download Hardware Sniffer: {exc}") from exc
    finally:
        if temporary_path is not None:
            temporary_path.unlink(missing_ok=True)

    return destination, tag


def _verify_hardware_sniffer_signature(executable: Path) -> str:
    """Require a valid Windows Authenticode signature before launching the downloaded helper."""
    powershell = _powershell_executable()
    if not powershell:
        raise RuntimeError("PowerShell is required to verify the Hardware Sniffer digital signature.")
    environment = os.environ.copy()
    environment["SIMPLE_HACKINTOSH_SNIFFER_PATH"] = str(executable.resolve())
    script = (
        "$ErrorActionPreference = 'Stop'; "
        "$signature = Get-AuthenticodeSignature -LiteralPath $env:SIMPLE_HACKINTOSH_SNIFFER_PATH; "
        "if ($signature.Status -ne 'Valid') { "
        "[Console]::Error.WriteLine('Authenticode status: ' + $signature.Status); exit 1 }; "
        "[Console]::Out.WriteLine($signature.SignerCertificate.Subject)"
    )
    try:
        result = subprocess.run(
            [powershell, "-NoProfile", "-NonInteractive", "-Command", script],
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            timeout=25,
            check=False,
            env=environment,
        )
    except (OSError, subprocess.TimeoutExpired) as exc:
        raise RuntimeError(f"Could not verify the Hardware Sniffer digital signature: {exc}") from exc
    signer = result.stdout.strip()
    if result.returncode != 0 or not signer:
        detail = (result.stderr or result.stdout).strip()
        raise RuntimeError(
            "The downloaded Hardware Sniffer did not pass Windows Authenticode verification. "
            + (detail or "The executable was not launched.")
        )
    return signer


def command_hardware_sniffer(output_dir: str = "HardwareReport") -> int:
    """Launch the official interactive Hardware Sniffer to export Report.json on Windows."""
    if platform.system() != "Windows":
        raise RuntimeError(
            "OpCore-Simplify's Hardware Sniffer is not supported on macOS. "
            "Use the built-in 'hardware' command there for a system_profiler report."
        )

    executable, release_tag = _download_hardware_sniffer()
    signer = _verify_hardware_sniffer_signature(executable)

    working_dir = Path(output_dir).expanduser().resolve()
    report_path = working_dir / "SysReport" / "Report.json"
    if report_path.exists():
        raise RuntimeError(
            f"A report already exists at {report_path}. Rename it or choose a new --output-dir."
        )
    working_dir.mkdir(parents=True, exist_ok=True)

    print(f"Launching official Hardware Sniffer {release_tag}.")
    print(f"Verified publisher: {signer}")
    print("")
    print("In Hardware Sniffer, choose H to export the hardware report, then Q to quit.")
    print("Do not choose A unless you explicitly want to dump ACPI tables.")
    print(f"Expected report location: {report_path}")
    print("")

    try:
        process = subprocess.run([str(executable)], cwd=str(working_dir), check=False)
    except OSError as exc:
        raise RuntimeError(f"Could not launch Hardware Sniffer: {exc}") from exc

    if not report_path.is_file():
        print("[ERROR] Report.json was not created. In Hardware Sniffer, press H to export, then Q to quit.", file=sys.stderr)
        return process.returncode or 1
    try:
        if report_path.stat().st_size > 8 * 1024 * 1024:
            raise RuntimeError("The generated Report.json is unexpectedly large.")
        with report_path.open("r", encoding="utf-8-sig") as stream:
            report = json.load(stream)
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise RuntimeError(f"Hardware Sniffer created an unreadable Report.json: {exc}") from exc
    if not isinstance(report, dict) or not report:
        raise RuntimeError("Hardware Sniffer Report.json does not contain a hardware report object.")

    print("")
    print(f"[PASS] OpCore-Simplify hardware report saved to: {report_path}")
    print("Review the report before sharing it; device names and IDs can identify your hardware.")
    return 0


def command_hardware(output: str | None, as_json: bool = False) -> int:
    data = collect_hardware()
    rendered = json.dumps(data, indent=2, ensure_ascii=False) if as_json else hardware_report_markdown(data)
    if output:
        destination = Path(output).expanduser()
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_text(rendered + "\n", encoding="utf-8")
        print(f"Hardware report saved to: {destination.resolve()}")
    else:
        print(rendered)
    return 0


def command_efi(path: str, output: str | None = None, as_json: bool = False) -> int:
    findings, summary = check_efi_path(Path(path).expanduser())
    if as_json:
        _emit_result({"summary": summary, "findings": [asdict(x) for x in findings]}, output, True)
    else:
        _print_findings(findings)
        if summary:
            print(f"Scanned {summary['files_scanned']} files; configuration: {summary['config_path']}")
        if output:
            _emit_result({"summary": summary, "findings": [asdict(x) for x in findings]}, output, True)
    if summary is None:
        return 2
    return 1 if any(item.level in ("WARN", "ERROR") for item in findings) else 0


def command_config(path: str, output: str | None = None, as_json: bool = False) -> int:
    findings, summary = check_config_file(Path(path).expanduser())
    if as_json:
        _emit_result({"summary": summary, "findings": [asdict(x) for x in findings]}, output, True)
    else:
        _print_findings(findings)
        print("This structural review does not certify that the EFI will boot.")
        if output:
            _emit_result({"summary": summary, "findings": [asdict(x) for x in findings]}, output, True)
    return 2 if summary is None else (1 if any(x.level in ("WARN", "ERROR") for x in findings) else 0)


def _file_digests(source: Path, entries: dict[str, str]) -> dict[str, str]:
    """Calculate SHA-256 for files in a folder or ZIP without extracting it."""
    digests: dict[str, str] = {}
    if source.is_dir():
        for key, original in entries.items():
            path = source / Path(*original.split("/"))
            digest = hashlib.sha256()
            with path.open("rb") as stream:
                for chunk in iter(lambda: stream.read(1024 * 1024), b""):
                    digest.update(chunk)
            digests[key] = digest.hexdigest()
        return digests

    with zipfile.ZipFile(source) as archive:
        for key, original in entries.items():
            digest = hashlib.sha256()
            with archive.open(original, "r") as stream:
                for chunk in iter(lambda: stream.read(1024 * 1024), b""):
                    digest.update(chunk)
            digests[key] = digest.hexdigest()
    return digests


def command_compare(first: str, second: str, output: str | None = None, as_json: bool = False) -> int:
    left_source = Path(first).expanduser()
    right_source = Path(second).expanduser()
    left, _, left_error = _read_source(left_source)
    right, _, right_error = _read_source(right_source)
    if left_error or right_error:
        message = left_error or right_error
        print(f"[ERROR] {message}", file=sys.stderr)
        return 2

    left_paths, right_paths = set(left), set(right)
    common = left_paths & right_paths
    left_hashes = _file_digests(left_source, left)
    right_hashes = _file_digests(right_source, right)
    changed = sorted(path for path in common if left_hashes[path] != right_hashes[path])
    result = {
        "first_name": left_source.name,
        "second_name": right_source.name,
        "only_in_first": sorted(left_paths - right_paths),
        "only_in_second": sorted(right_paths - left_paths),
        "different_contents": changed,
        "identical_files": len(common) - len(changed),
        "same_file_list": left_paths == right_paths,
        "byte_identical": not (left_paths - right_paths or right_paths - left_paths or changed),
        "hash_algorithm": "SHA-256",
    }
    if as_json:
        _emit_result(result, output, True)
    else:
        print(f"Compare: {result['first_name']}  <->  {result['second_name']}")
        print(f"Only in first: {len(result['only_in_first'])}")
        for item in result["only_in_first"][:100]:
            print(f"  - {item}")
        print(f"Only in second: {len(result['only_in_second'])}")
        for item in result["only_in_second"][:100]:
            print(f"  + {item}")
        print(f"Same path, different content: {len(changed)}")
        for item in changed[:100]:
            print(f"  * {item}")
        print(f"Identical files: {result['identical_files']}")
        print("The two EFI copies are byte-identical." if result["byte_identical"] else "The two EFI copies differ.")
        if output:
            _emit_result(result, output, True)
    return 0


def command_backup_efi(path: str, output: str) -> int:
    source = Path(path).expanduser()
    destination = Path(output).expanduser()
    if not source.is_dir():
        print("[ERROR] Backup source must be an extracted EFI folder, not a ZIP file.", file=sys.stderr)
        return 2
    entries, _, error = _read_source(source)
    if error:
        print(f"[ERROR] {error}", file=sys.stderr)
        return 2
    if not entries:
        print("[ERROR] The selected folder contains no files.", file=sys.stderr)
        return 2

    source_root = source.resolve()
    destination_parent = destination.parent.resolve()
    try:
        destination_parent.relative_to(source_root)
    except ValueError:
        pass
    else:
        print("[ERROR] Save the backup outside the source EFI folder.", file=sys.stderr)
        return 2

    destination.parent.mkdir(parents=True, exist_ok=True)
    try:
        with zipfile.ZipFile(destination, "x", compression=zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
            for original in sorted(entries.values()):
                item = source / Path(*original.split("/"))
                try:
                    item.resolve().relative_to(source_root)
                except (OSError, ValueError):
                    continue
                archive.write(item, arcname=original)
    except FileExistsError:
        print(f"[ERROR] Backup already exists; refusing to overwrite: {destination}", file=sys.stderr)
        return 2
    except (OSError, zipfile.BadZipFile) as exc:
        try:
            destination.unlink(missing_ok=True)
        except OSError:
            pass
        raise RuntimeError(f"Could not create EFI backup: {exc}") from exc

    size = destination.stat().st_size
    print(f"[PASS] Backup created: {destination.resolve()}")
    print(f"Files copied: {len(entries)}")
    print(f"Archive size: {size:,} bytes")
    print("The source EFI folder was not modified.")
    return 0



def command_create_efi(release_zip: str, output: str = "EFI") -> int:
    """Create a starter EFI directory from a user-supplied official OpenCore release ZIP.

    The resulting config.plist is OpenCore's Sample.plist, not a hardware-configured
    configuration. The function never writes to a disk or EFI System Partition.
    """
    source = Path(release_zip).expanduser()
    destination = Path(output).expanduser()

    if not source.is_file() or not zipfile.is_zipfile(source):
        print(f"[ERROR] OpenCore release ZIP not found or invalid: {source}", file=sys.stderr)
        return 2
    if destination.exists():
        print(f"[ERROR] Output already exists; refusing to overwrite: {destination}", file=sys.stderr)
        return 2

    try:
        with zipfile.ZipFile(source) as archive:
            infos = archive.infolist()
            if len(infos) > 30000:
                print("[ERROR] The ZIP contains too many entries to process safely.", file=sys.stderr)
                return 2
            total_size = sum(info.file_size for info in infos if not info.is_dir())
            if total_size > 2 * 1024 * 1024 * 1024:
                print("[ERROR] The uncompressed archive is larger than the 2 GiB safety limit.", file=sys.stderr)
                return 2

            safe_infos: list[tuple[zipfile.ZipInfo, str]] = []
            roots: dict[str, list[str]] = {}
            for info in infos:
                if info.is_dir():
                    continue
                safe = _safe_archive_name(info.filename)
                if safe is None:
                    continue
                mode = (info.external_attr >> 16) & 0o170000
                if mode == 0o120000:
                    continue
                safe_infos.append((info, safe))
                parts = safe.split("/")
                for index in range(len(parts) - 2):
                    if (
                        parts[index].casefold() == "efi"
                        and parts[index + 1].casefold() == "boot"
                        and parts[index + 2].casefold() == "bootx64.efi"
                    ):
                        root_parts = parts[:index + 1]
                        root_key = "/".join(root_parts).casefold()
                        roots[root_key] = root_parts
                        break

            if len(roots) != 1:
                print(
                    "[ERROR] Could not identify one unambiguous X64/EFI tree containing "
                    "BOOT/BOOTx64.efi. Use the official OpenCore release package ZIP.",
                    file=sys.stderr,
                )
                return 2

            root_parts = next(iter(roots.values()))
            root_key = "/".join(root_parts).casefold()
            prefix = root_key + "/"
            efi_members: dict[str, tuple[str, zipfile.ZipInfo]] = {}
            for info, safe in safe_infos:
                safe_key = safe.casefold()
                if safe_key.startswith(prefix):
                    relative = safe[len("/".join(root_parts)) + 1:]
                    if relative:
                        efi_members.setdefault(relative.casefold(), (relative, info))

            required = ("boot/bootx64.efi", "oc/opencore.efi")
            missing = [item for item in required if item not in efi_members]
            if missing:
                print("[ERROR] OpenCore ZIP is missing required EFI file(s): " + ", ".join(missing), file=sys.stderr)
                return 2

            sample_candidates = [
                (safe, info) for info, safe in safe_infos
                if safe.casefold().endswith("/docs/sample.plist")
                or safe.casefold() == "docs/sample.plist"
            ]
            if not sample_candidates:
                sample_candidates = [
                    (safe, info) for info, safe in safe_infos
                    if safe.casefold().endswith("/sample.plist")
                    or safe.casefold() == "sample.plist"
                ]
            if not sample_candidates:
                print("[ERROR] Sample.plist was not found in the OpenCore release ZIP.", file=sys.stderr)
                return 2
            sample_candidates.sort(key=lambda item: (
                0 if item[0].casefold().endswith("/docs/sample.plist") else 1,
                len(item[0]),
            ))
            sample_info = sample_candidates[0][1]
            if sample_info.file_size > 16 * 1024 * 1024:
                print("[ERROR] Sample.plist is unexpectedly large.", file=sys.stderr)
                return 2
            sample_bytes = archive.read(sample_info)
            try:
                sample_data = plistlib.loads(sample_bytes)
            except Exception as exc:
                print(f"[ERROR] Sample.plist could not be parsed: {exc}", file=sys.stderr)
                return 2
            if not isinstance(sample_data, dict):
                print("[ERROR] Sample.plist does not contain a property-list dictionary.", file=sys.stderr)
                return 2

            destination.parent.mkdir(parents=True, exist_ok=True)
            stage = Path(tempfile.mkdtemp(
                prefix=f".{destination.name or 'EFI'}.staging-",
                dir=str(destination.parent.resolve()),
            ))
            try:
                for relative, info in efi_members.values():
                    if relative.casefold() == "oc/config.plist":
                        continue
                    target = stage / Path(*relative.split("/"))
                    target.parent.mkdir(parents=True, exist_ok=True)
                    with archive.open(info, "r") as src, target.open("wb") as dst:
                        shutil.copyfileobj(src, dst)

                for folder in (
                    "OC/ACPI", "OC/Drivers", "OC/Kexts", "OC/Tools", "OC/Resources",
                ):
                    (stage / folder).mkdir(parents=True, exist_ok=True)

                (stage / "OC" / "config.plist").write_bytes(sample_bytes)
                (stage / "SETUP_REQUIRED.txt").write_text(
                    "EFI starter created from an OpenCore release ZIP.\n"
                    "\n"
                    "IMPORTANT: This is not a hardware-configured or ready-to-boot EFI.\n"
                    "- OC/config.plist is the upstream Sample.plist.\n"
                    "- Add only ACPI tables, drivers, and kexts appropriate for your exact hardware.\n"
                    "- Configure every relevant config.plist section for the target computer.\n"
                    "- Generate your own SMBIOS identity values; do not reuse another person's values.\n"
                    "- Validate config.plist with ocvalidate from this exact OpenCore release.\n"
                    "- This utility only creates a folder. It does not mount, format, or write to a disk.\n",
                    encoding="utf-8",
                )

                if destination.exists():
                    raise FileExistsError(f"Output appeared while creating EFI: {destination}")
                stage.rename(destination)
            except Exception:
                shutil.rmtree(stage, ignore_errors=True)
                raise

    except (OSError, zipfile.BadZipFile, RuntimeError) as exc:
        print(f"[ERROR] Could not create EFI starter: {exc}", file=sys.stderr)
        return 2

    copied = len(efi_members) - (1 if "oc/config.plist" in efi_members else 0)
    print(f"[PASS] EFI starter created: {destination.resolve()}")
    print(f"Files copied from OpenCore release: {copied}")
    print("Sample.plist copied to OC/config.plist.")
    print("[WARN] This is a starter only. Hardware-specific configuration and validation are still required.")
    print("No disk or EFI System Partition was modified.")
    return 0

def command_ocvalidate(config_path: str, tool_path: str, expected_version: str | None = None) -> int:
    config = Path(config_path).expanduser()
    tool = Path(tool_path).expanduser()
    if not config.is_file():
        print(f"[ERROR] config.plist not found: {config}", file=sys.stderr)
        return 2
    if not tool.is_file():
        print(f"[ERROR] ocvalidate executable not found: {tool}", file=sys.stderr)
        return 2
    try:
        version_result = subprocess.run(
            [str(tool), "--version"], capture_output=True, text=True,
            encoding="utf-8", errors="replace", timeout=20, check=False,
        )
    except (OSError, subprocess.TimeoutExpired) as exc:
        print(f"[ERROR] Could not start ocvalidate: {exc}", file=sys.stderr)
        return 2
    version_text = (version_result.stdout + "\n" + version_result.stderr).strip()
    if version_text:
        print("ocvalidate version information:")
        print(version_text)
    else:
        print("[WARN] ocvalidate did not return version information.")
    if expected_version and expected_version.casefold() not in version_text.casefold():
        print("[ERROR] The validator version does not match the version you specified.")
        print("No validation was run. Choose the ocvalidate shipped with the same OpenCore release as this EFI.")
        return 3
    print("\nRunning the official validator against the selected config.plist...\n")
    try:
        result = subprocess.run(
            [str(tool), str(config)], capture_output=True, text=True,
            encoding="utf-8", errors="replace", timeout=90, check=False,
        )
    except (OSError, subprocess.TimeoutExpired) as exc:
        print(f"[ERROR] Could not run ocvalidate: {exc}", file=sys.stderr)
        return 2
    output = (result.stdout + "\n" + result.stderr).strip()
    print(output or "(ocvalidate returned no text output)")
    if result.returncode == 0:
        print("\n[PASS] ocvalidate returned success.")
        return 0
    print(f"\n[WARN] ocvalidate returned exit code {result.returncode}.")
    return 1



def command_macrecovery(arguments: list[str] | None = None) -> int:
    """Run the bundled upstream OpenCore macrecovery utility."""
    script = Path(__file__).resolve().parent / "macrecovery" / "macrecovery.py"
    if not script.is_file():
        print(f"[ERROR] Bundled macrecovery.py was not found: {script}", file=sys.stderr)
        return 2

    forwarded = list(arguments or ["--help"])
    command = [sys.executable, str(script), *forwarded]
    print("[INFO] Running the bundled OpenCore macrecovery utility.")
    try:
        return subprocess.run(command, check=False).returncode
    except OSError as exc:
        print(f"[ERROR] Could not start macrecovery: {exc}", file=sys.stderr)
        return 2

def build_support_report(efi_path: str, include_hardware: bool = True) -> dict[str, Any]:
    findings, summary = check_efi_path(Path(efi_path).expanduser())
    report: dict[str, Any] = {
        "application": {"name": "Simple Hackintosh CLI", "version": APP_VERSION},
        "efi": {
            "source_name": Path(efi_path).name,
            "files_scanned": summary.get("files_scanned") if summary else 0,
            "config_path": Path(summary["config_path"]).name if summary else None,
            "findings": [asdict(item) for item in findings],
        },
        "privacy": "No serial numbers, UUIDs, MAC addresses, account identifiers, or raw config.plist contents are included.",
    }
    if include_hardware:
        if platform.system() == "Windows":
            try:
                report["hardware"] = collect_hardware()
            except (RuntimeError, OSError, subprocess.TimeoutExpired) as exc:
                report["hardware_error"] = str(exc)
        else:
            report["hardware_note"] = "Hardware inventory is available only when this report is generated on Windows."
    return report


def _support_markdown(report: dict[str, Any]) -> str:
    lines = [
        "# Simple Hackintosh — Support report",
        "",
        f"- Utility version: {report['application']['version']}",
        f"- EFI folder/archive: {report['efi']['source_name']}",
        f"- Files scanned: {report['efi']['files_scanned']}",
        f"- Config file: {report['efi']['config_path'] or 'Not found'}",
        "",
        "## EFI findings",
    ]
    for item in report["efi"]["findings"]:
        lines.append(f"- **{item['level']}** {item['message']}")
    if "hardware" in report:
        lines.extend(["", "## Hardware inventory", "", hardware_report_markdown(report["hardware"])])
    elif "hardware_error" in report:
        lines.extend(["", "## Hardware inventory", "", f"Not available: {report['hardware_error']}"])
    elif "hardware_note" in report:
        lines.extend(["", "## Hardware inventory", "", report["hardware_note"]])
    lines.extend([
        "",
        "## Privacy",
        report["privacy"],
        "Raw config.plist data and SMBIOS identity values are not included.",
        "",
    ])
    return "\n".join(lines)


def command_support(efi_path: str, output: str | None = None, as_json: bool = False, no_hardware: bool = False) -> int:
    report = build_support_report(efi_path, include_hardware=not no_hardware)
    rendered = json.dumps(report, indent=2, ensure_ascii=False, default=str) if as_json else _support_markdown(report)
    default_name = "hackintosh-support-report.json" if as_json else "hackintosh-support-report.md"
    destination = Path(output or default_name).expanduser()
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text(rendered + "\n", encoding="utf-8")
    print(f"Support report saved to: {destination.resolve()}")
    levels = [item["level"] for item in report["efi"]["findings"]]
    print(f"EFI findings: {levels.count('PASS')} passed, {levels.count('WARN')} warning(s), {levels.count('ERROR')} error(s)")
    print("Review the report before sharing it. Although identifying firmware values are excluded, check device names and file paths for personal information.")
    return 2 if report["efi"]["config_path"] is None else (1 if any(x in ("WARN", "ERROR") for x in levels) else 0)




def _ansi_enabled() -> bool:
    if "NO_COLOR" in os.environ or os.environ.get("TERM", "").casefold() == "dumb":
        return False
    try:
        if not sys.stdout.isatty():
            return False
        if os.name == "nt":
            import ctypes
            handle = ctypes.windll.kernel32.GetStdHandle(-11)
            mode = ctypes.c_uint()
            if not ctypes.windll.kernel32.GetConsoleMode(handle, ctypes.byref(mode)):
                return False
            return bool(ctypes.windll.kernel32.SetConsoleMode(handle, mode.value | 0x0004))
        return True
    except (AttributeError, OSError, ValueError):
        return False


def _paint(text: str, style: str, enabled: bool) -> str:
    palette = {
        "border": "36",
        "title": "96;1",
        "subtitle": "90",
        "heading": "93;1",
        "item": "97",
        "muted": "90",
        "prompt": "96;1",
        "success": "92;1",
        "danger": "91;1",
    }
    if not enabled:
        return text
    code = palette.get(style)
    return f"\033[{code}m{text}\033[0m" if code else text


def render_menu(use_color: bool | None = None) -> str:
    enabled = _ansi_enabled() if use_color is None else use_color
    width = max(64, min(shutil.get_terminal_size((88, 24)).columns, 100))
    inner = width - 2
    border = "+" + ("=" * inner) + "+"

    def boxed(text: str, style: str = "item") -> str:
        visible = text[:inner]
        padded = visible.ljust(inner)
        return _paint("|", "border", enabled) + _paint(padded, style, enabled) + _paint("|", "border", enabled)

    def menu_row(number: str, label: str, description: str) -> str:
        available = max(12, inner - 33)
        desc = textwrap.shorten(description, width=available, placeholder="...")
        row = f" {number:>2}   {label:<25} {desc}"
        return boxed(row, "item")

    system_line = f"  {platform.system()} {platform.release()}  |  Python {platform.python_version()}  |  v{APP_VERSION}"
    lines = [
        _paint(border, "border", enabled),
        boxed(f"  SIMPLE HACKINTOSH                                      v{APP_VERSION}", "title"),
        boxed("  HARDWARE INTELLIGENCE  /  EFI WORKSHOP  /  RECOVERY", "subtitle"),
        boxed(system_line, "muted"),
        _paint(border, "border", enabled),
        boxed("  HARDWARE & DIAGNOSTICS", "heading"),
        menu_row("01", "Hardware report", "Windows: export OpCore-Simplify Report.json; macOS: native inventory"),
        menu_row("02", "Scan EFI", "Inspect an EFI folder or ZIP without changing it"),
        menu_row("03", "Validate config.plist", "Check plist structure and common entry mistakes"),
        menu_row("04", "Run ocvalidate", "Use the validator matching your OpenCore release"),
        "",
        boxed("  EFI WORKBENCH", "heading"),
        menu_row("05", "Compare EFI copies", "Find added, removed and changed files"),
        menu_row("06", "Support report", "Create a privacy-conscious troubleshooting report"),
        menu_row("08", "Back up EFI", "Create a ZIP backup without overwriting existing files"),
        menu_row("10", "Create EFI starter", "Build a starter folder from an OpenCore release ZIP"),
        "",
        boxed("  RECOVERY & SYSTEM", "heading"),
        menu_row("09", "Download Recovery", "Fetch and verify Apple Recovery files"),
        menu_row("07", "Program diagnostics", "Check Python, Windows tools and runtime"),
        _paint(border, "border", enabled),
        boxed("  00  Exit", "danger"),
        boxed("  All checks run locally. Disk and firmware changes are not performed.", "subtitle"),
        _paint(border, "border", enabled),
    ]
    return "\n".join(lines)


def interactive_menu() -> int:
    while True:
        use_color = _ansi_enabled()
        if use_color:
            print("\033[2J\033[H", end="")
        print(render_menu(use_color))
        try:
            choice = input(_paint("\n  Select an option [0-10] > ", "prompt", use_color)).strip()
            if choice == "0":
                print(_paint("\n  Goodbye.", "success", use_color))
                return 0
            if choice == "1":
                try:
                    if platform.system() == "Windows":
                        output_dir = input("Report output folder [HardwareReport]: ").strip().strip('"') or "HardwareReport"
                        command_hardware_sniffer(output_dir)
                    else:
                        command_hardware(None)
                except (RuntimeError, OSError, subprocess.TimeoutExpired) as exc:
                    print(f"[ERROR] {exc}")
            elif choice == "2":
                path = input("EFI folder or ZIP path: ").strip().strip('"')
                if path:
                    command_efi(path)
            elif choice == "3":
                path = input("Full path to config.plist: ").strip().strip('"')
                if path:
                    command_config(path)
            elif choice == "4":
                config = input("Full path to config.plist: ").strip().strip('"')
                tool = input("Full path to matching ocvalidate executable: ").strip().strip('"')
                expected = input("Expected OpenCore version (optional, e.g. 1.0.5): ").strip() or None
                if config and tool:
                    command_ocvalidate(config, tool, expected)
            elif choice == "5":
                first = input("First EFI folder/ZIP: ").strip().strip('"')
                second = input("Second EFI folder/ZIP: ").strip().strip('"')
                if first and second:
                    command_compare(first, second)
            elif choice == "6":
                path = input("EFI folder or ZIP path: ").strip().strip('"')
                output = input("Report filename [hackintosh-support-report.md]: ").strip() or "hackintosh-support-report.md"
                if path:
                    command_support(path, output)
            elif choice == "7":
                command_doctor()
            elif choice == "8":
                path = input("EFI folder to back up: ").strip().strip('"')
                output = input("Backup ZIP filename [EFI-backup.zip]: ").strip() or "EFI-backup.zip"
                if path:
                    command_backup_efi(path, output)
            elif choice == "9":
                print("This downloads Apple's Recovery image and chunklist to a folder.")
                print("It does not erase, format, or write to a USB drive.")
                version_choice = input("Recovery version [1=latest, 2=default] (1): ").strip().lower()
                if version_choice in ("", "1", "latest"):
                    os_type = "latest"
                elif version_choice in ("2", "default"):
                    os_type = "default"
                else:
                    print("Please choose 1 (latest) or 2 (default).")
                    continue
                output = input("Output folder [com.apple.recovery.boot]: ").strip().strip('"') or "com.apple.recovery.boot"
                command_macrecovery(["download", "--os-type", os_type, "--outdir", output])
            elif choice == "10":
                print("Creates an EFI starter folder from an OpenCore release ZIP.")
                print("The generated config.plist is a sample and is NOT hardware-configured.")
                release_zip = input("Path to the official OpenCore release ZIP: ").strip().strip('"')
                output = input("New EFI folder path [EFI]: ").strip().strip('"') or "EFI"
                if release_zip:
                    command_create_efi(release_zip, output)
            else:
                print(_paint("  Invalid option. Choose a number from 0 to 10.", "danger", use_color))
        except (KeyboardInterrupt, EOFError):
            print("\nReturning to the prompt.")
            return 0
        try:
            input("\nPress Enter to return to the main menu...")
        except (KeyboardInterrupt, EOFError):
            return 0

def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        prog="simple-hackintosh",
        description="A local, read-only assistant for OpenCore EFI checks and Windows hardware inventory.",
        epilog="No browser, account, upload, or third-party Python package is required.",
    )
    parser.add_argument("--version", action="version", version=f"%(prog)s {APP_VERSION}")
    commands = parser.add_subparsers(dest="command")

    commands.add_parser("wizard", help="Open the beginner-friendly interactive menu.")
    commands.add_parser("doctor", help="Check Python, Windows tools, and optional ocvalidate.")

    hardware = commands.add_parser("hardware", aliases=["inspect-hardware"], help="Detect hardware and PCI device IDs.")
    hardware.add_argument("--output", "-o", help="Save a report.")
    hardware.add_argument("--json", action="store_true", help="Print/save JSON instead of Markdown.")

    sniffer = commands.add_parser(
        "hardware-sniffer",
        help="Launch the official OpCore-Simplify Hardware Sniffer to export Report.json (Windows only).",
    )
    sniffer.add_argument(
        "--output-dir",
        default="HardwareReport",
        help="Working/output folder. Report.json is saved under SysReport inside it.",
    )

    efi = commands.add_parser("efi-check", aliases=["scan-efi"], help="Scan an EFI folder or ZIP.")
    efi.add_argument("path", help="EFI folder or ZIP file.")
    efi.add_argument("--output", "-o", help="Save machine-readable JSON findings.")
    efi.add_argument("--json", action="store_true", help="Print JSON findings.")

    config = commands.add_parser("config-check", aliases=["lint"], help="Check config.plist structure and entry fields.")
    config.add_argument("path", help="Path to config.plist.")
    config.add_argument("--output", "-o", help="Save machine-readable JSON findings.")
    config.add_argument("--json", action="store_true", help="Print JSON findings.")

    official = commands.add_parser("ocvalidate", help="Run the official validator supplied with an OpenCore release.")
    official.add_argument("config", help="Path to config.plist.")
    official.add_argument("--tool", required=True, help="Path to the matching ocvalidate executable.")
    official.add_argument("--expect-version", help="Expected OpenCore version; validation is stopped if it does not match.")

    macrecovery = commands.add_parser(
        "macrecovery",
        help="Run the bundled OpenCore macrecovery utility (downloads Apple Recovery files).",
    )
    macrecovery.add_argument(
        "arguments", nargs=argparse.REMAINDER,
        help="Arguments passed to macrecovery.py; use -h to see its options.",
    )

    create_efi = commands.add_parser(
        "create-efi",
        help="Create an EFI starter folder from a user-supplied OpenCore release ZIP.",
    )
    create_efi.add_argument("release_zip", help="Official OpenCore release ZIP containing X64/EFI and Docs/Sample.plist.")
    create_efi.add_argument("--output", "-o", default="EFI", help="New EFI output folder; existing folders are never overwritten.")

    backup = commands.add_parser("backup-efi", help="Create a new ZIP backup from an EFI folder without changing the source.")
    backup.add_argument("path", help="Extracted EFI folder to back up.")
    backup.add_argument("--output", "-o", default="EFI-backup.zip", help="New ZIP path; existing files are never overwritten.")

    compare = commands.add_parser("compare-efi", help="Compare file lists and SHA-256 content in two EFI folders or ZIPs.")
    compare.add_argument("first", help="First EFI folder or ZIP.")
    compare.add_argument("second", help="Second EFI folder or ZIP.")
    compare.add_argument("--output", "-o", help="Save comparison as JSON.")
    compare.add_argument("--json", action="store_true", help="Print JSON comparison.")

    support = commands.add_parser("support-report", help="Create a privacy-conscious report for troubleshooting.")
    support.add_argument("efi", help="EFI folder or ZIP.")
    support.add_argument("--output", "-o", help="Report file path (defaults to Markdown or JSON name).")
    support.add_argument("--json", action="store_true", help="Save JSON instead of Markdown.")
    support.add_argument("--no-hardware", action="store_true", help="Do not collect the Windows hardware inventory.")
    return parser


def main(argv: list[str] | None = None) -> int:
    actual_argv = sys.argv[1:] if argv is None else argv
    if not actual_argv:
        return interactive_menu()
    parser = build_parser()
    args = parser.parse_args(actual_argv)
    try:
        if args.command == "wizard":
            return interactive_menu()
        if args.command == "doctor":
            return command_doctor()
        if args.command in ("hardware", "inspect-hardware"):
            return command_hardware(args.output, args.json)
        if args.command == "hardware-sniffer":
            return command_hardware_sniffer(args.output_dir)
        if args.command in ("efi-check", "scan-efi"):
            return command_efi(args.path, args.output, args.json)
        if args.command in ("config-check", "lint"):
            return command_config(args.path, args.output, args.json)
        if args.command == "ocvalidate":
            return command_ocvalidate(args.config, args.tool, args.expect_version)
        if args.command == "macrecovery":
            return command_macrecovery(args.arguments)
        if args.command == "create-efi":
            return command_create_efi(args.release_zip, args.output)
        if args.command == "backup-efi":
            return command_backup_efi(args.path, args.output)
        if args.command == "compare-efi":
            return command_compare(args.first, args.second, args.output, args.json)
        if args.command == "support-report":
            return command_support(args.efi, args.output, args.json, args.no_hardware)
    except (OSError, RuntimeError, zipfile.BadZipFile, subprocess.TimeoutExpired) as exc:
        print(f"[ERROR] {exc}", file=sys.stderr)
        return 2
    parser.error("Unknown command.")
    return 2


if __name__ == "__main__":
    raise SystemExit(main())
