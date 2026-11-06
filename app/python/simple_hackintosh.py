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
import zipfile
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Any, Iterable

APP_VERSION = "2.0.0"
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
    if platform.system() != "Windows":
        raise RuntimeError("Hardware inventory currently requires Windows PowerShell and CIM.")
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
  ComputerModel = $computer.Model
  CPU = $cpu.Name
  CPUCores = $cpu.NumberOfCores
  LogicalProcessors = $cpu.NumberOfLogicalProcessors
  GPUs = $gpus
  Motherboard = (($board.Manufacturer, $board.Product) -join ' ').Trim()
  BIOS = (($bios.Manufacturer, $bios.SMBIOSBIOSVersion) -join ' ').Trim()
  Windows = $os.Caption
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
    return result


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


def hardware_report_markdown(data: dict[str, Any]) -> str:
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
        ("BIOS", data.get("BIOS")),
        ("Windows", data.get("Windows")),
        ("Windows version", data.get("WindowsVersion")),
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
        "This is a hardware inventory, not a macOS compatibility verdict. Check exact device IDs against current upstream documentation.",
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


def interactive_menu() -> int:
    while True:
        print("\n" + "=" * 58)
        print("             SIMPLE HACKINTOSH — ASSISTANT")
        print("=" * 58)
        print("  1. Detect my hardware")
        print("  2. Check an EFI folder or ZIP")
        print("  3. Check a config.plist")
        print("  4. Run official OpenCore ocvalidate")
        print("  5. Compare two EFI folders/ZIPs")
        print("  6. Create a support report")
        print("  7. Check this program (doctor)")
        print("  8. Back up an EFI folder to ZIP")
        print("  0. Exit")
        try:
            choice = input("\nChoose an option: ").strip()
            if choice == "0":
                print("Goodbye.")
                return 0
            if choice == "1":
                try:
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
            else:
                print("Please choose one of the listed numbers.")
        except (KeyboardInterrupt, EOFError):
            print("\nReturning to the prompt.")
            return 0
        input("\nPress Enter to return to the menu...")


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
        if args.command in ("efi-check", "scan-efi"):
            return command_efi(args.path, args.output, args.json)
        if args.command in ("config-check", "lint"):
            return command_config(args.path, args.output, args.json)
        if args.command == "ocvalidate":
            return command_ocvalidate(args.config, args.tool, args.expect_version)
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
