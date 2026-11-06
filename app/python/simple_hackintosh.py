#!/usr/bin/env python3
"""Simple Hackintosh command-line diagnostics.

Uses only the Python standard library. It does not modify disks, EFI files,
firmware settings, boot entries, or Windows configuration.
"""
from __future__ import annotations

import argparse
import json
import platform
import plistlib
import shutil
import subprocess
import sys
import zipfile
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Any, Iterable

APP_VERSION = "1.0.0"
SECTIONS = (
    "ACPI", "Booter", "DeviceProperties", "Kernel",
    "Misc", "NVRAM", "PlatformInfo", "UEFI",
)

EXPECTED_TYPES: dict[str, dict[str, type]] = {
    "ACPI": {"Add": list, "Delete": list, "Patch": list, "Quirks": dict},
    "Booter": {"MmioWhitelist": list, "Quirks": dict},
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
    "PlatformInfo": {"Automatic": bool, "Generic": dict, "UpdateDataHub": bool},
    "UEFI": {"APFS": dict, "Audio": dict, "Drivers": list, "Input": dict, "Output": dict, "Quirks": dict},
}


@dataclass
class Finding:
    level: str
    message: str


def _safe_archive_name(name: str) -> str | None:
    """Return a normalized relative ZIP path, rejecting traversal/absolute paths."""
    value = name.replace("\\", "/")
    if value.startswith("/") or (len(value) >= 2 and value[1] == ":"):
        return None
    parts = [part for part in value.split("/") if part not in ("", ".")]
    if any(part == ".." for part in parts):
        return None
    return "/".join(parts)


def validate_config_data(config: Any) -> list[Finding]:
    findings: list[Finding] = []
    if not isinstance(config, dict):
        return [Finding("ERROR", "The plist root must be a dictionary.")]

    for section in SECTIONS:
        if section not in config:
            findings.append(Finding("ERROR", f"Missing top-level section: {section}"))
            continue
        if not isinstance(config[section], dict):
            findings.append(Finding("ERROR", f"{section} must be a dictionary."))
            continue
        for key, expected in EXPECTED_TYPES.get(section, {}).items():
            if key in config[section] and not isinstance(config[section][key], expected):
                findings.append(
                    Finding(
                        "ERROR",
                        f"{section}.{key} must be {expected.__name__}, "
                        f"not {type(config[section][key]).__name__}.",
                    )
                )

    if not findings:
        findings.append(
            Finding(
                "PASS",
                "The plist is readable and its main OpenCore sections have the expected basic types.",
            )
        )
    return findings


def load_plist_bytes(data: bytes) -> tuple[Any | None, list[Finding]]:
    try:
        return plistlib.loads(data), []
    except (plistlib.InvalidFileException, ValueError, TypeError) as exc:
        return None, [Finding("ERROR", f"Could not parse config.plist: {exc}")]


def _read_source(source: Path) -> tuple[dict[str, str], bytes | None, str | None]:
    """Return case-folded path -> original path, optional plist bytes, and error."""
    if not source.exists():
        return {}, None, f"Path does not exist: {source}"
    entries: dict[str, str] = {}
    config_bytes: bytes | None = None

    if source.is_dir():
        for item in source.rglob("*"):
            if not item.is_file():
                continue
            try:
                rel = item.relative_to(source).as_posix()
            except ValueError:
                continue
            safe = _safe_archive_name(rel)
            if safe is None:
                continue
            entries[safe.casefold()] = safe
        config_candidates = [
            original for key, original in entries.items()
            if key == "oc/config.plist" or key.endswith("/oc/config.plist")
        ]
        if config_candidates:
            chosen = sorted(config_candidates, key=len)[0]
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
                    if safe is None:
                        continue
                    entries[safe.casefold()] = info.filename
                config_candidates = [
                    (key, original) for key, original in entries.items()
                    if key == "oc/config.plist" or key.endswith("/oc/config.plist")
                ]
                if config_candidates:
                    _, chosen = sorted(config_candidates, key=lambda pair: len(pair[0]))[0]
                    config_bytes = archive.read(chosen)
        except (OSError, zipfile.BadZipFile, KeyError) as exc:
            return {}, None, f"Could not read ZIP archive: {exc}"
        return entries, config_bytes, None

    return {}, None, "Choose an EFI folder or a ZIP archive. Other archive formats are not supported."


def _relative_join(*parts: str) -> str:
    return "/".join(part.strip("/") for part in parts if part.strip("/")).casefold()


def check_efi_path(source: Path) -> tuple[list[Finding], dict[str, Any] | None]:
    entries, config_bytes, error = _read_source(source)
    if error:
        return [Finding("ERROR", error)], None
    if not entries:
        return [Finding("ERROR", "No files were found in the selected folder/archive.")], None

    config_candidates = [
        (key, original) for key, original in entries.items()
        if key == "oc/config.plist" or key.endswith("/oc/config.plist")
    ]
    if not config_candidates:
        return [Finding("ERROR", "Could not find OC/config.plist. Select the EFI folder or a ZIP containing it.")], None

    config_key, config_original = sorted(config_candidates, key=lambda pair: len(pair[0]))[0]
    oc_prefix = config_key[: -len("oc/config.plist")]
    efi_prefix = oc_prefix

    findings: list[Finding] = []
    expected_files = (
        (_relative_join(efi_prefix, "BOOT/BOOTx64.efi"), "BOOT/BOOTx64.efi"),
        (_relative_join(efi_prefix, "OC/OpenCore.efi"), "OC/OpenCore.efi"),
        (config_key, "OC/config.plist"),
    )
    for key, label in expected_files:
        if key in entries:
            findings.append(Finding("PASS", f"Found {label}."))
        else:
            findings.append(Finding("ERROR", f"Missing required file: {label}."))

    if len(config_candidates) > 1:
        findings.append(Finding("WARN", f"Found {len(config_candidates)} config.plist files; checked {config_original}."))

    if config_bytes is None:
        findings.append(Finding("ERROR", "The config.plist file could not be read."))
        return findings, None

    config, parse_findings = load_plist_bytes(config_bytes)
    findings.extend(parse_findings)
    if not isinstance(config, dict):
        return findings, None
    findings.extend(validate_config_data(config))

    reference_groups = (
        ("ACPI", "Add", "Path", "ACPI"),
        ("Kernel", "Add", "BundlePath", "Kexts"),
        ("UEFI", "Drivers", "Path", "Drivers"),
        ("Misc", "Tools", "Path", "Tools"),
    )
    for section, key, field, folder in reference_groups:
        rows = config.get(section, {}).get(key, [])
        if not isinstance(rows, list):
            continue
        for index, item in enumerate(rows):
            if not isinstance(item, dict) or item.get("Enabled") is False:
                continue
            ref = item.get(field)
            if not isinstance(ref, str) or not ref.strip():
                continue
            safe_ref = _safe_archive_name(ref)
            if safe_ref is None:
                findings.append(Finding("ERROR", f"{section}.{key}[{index}] contains an unsafe path: {ref}"))
                continue
            expected = _relative_join(oc_prefix, folder, safe_ref)
            if expected in entries:
                findings.append(Finding("PASS", f"{section}: found referenced {ref}."))
            else:
                findings.append(Finding("WARN", f"{section}: referenced file/folder is missing: {folder}/{ref}."))

    summary = {
        "source": str(source),
        "files_scanned": len(entries),
        "config_path": config_original,
        "findings": [asdict(item) for item in findings],
    }
    return findings, summary


def _powershell_executable() -> str | None:
    return shutil.which("powershell") or shutil.which("pwsh")


def collect_hardware() -> dict[str, Any]:
    if platform.system() != "Windows":
        raise RuntimeError("Hardware inventory currently requires Windows PowerShell and CIM.")
    executable = _powershell_executable()
    if not executable:
        raise RuntimeError("PowerShell was not found. Windows PowerShell is normally included with Windows.")

    script = r"""
$ErrorActionPreference = 'Stop'
$cpu = (Get-CimInstance Win32_Processor | Select-Object -First 1 -ExpandProperty Name)
$gpus = @(Get-CimInstance Win32_VideoController | ForEach-Object { $_.Name } | Where-Object { $_ })
$board = Get-CimInstance Win32_BaseBoard | Select-Object -First 1
$bios = Get-CimInstance Win32_BIOS | Select-Object -First 1
$os = Get-CimInstance Win32_OperatingSystem | Select-Object -First 1
$computer = Get-CimInstance Win32_ComputerSystem | Select-Object -First 1
$disks = @(Get-CimInstance Win32_DiskDrive | ForEach-Object {
  [pscustomobject]@{ Model = $_.Model; SizeGB = [math]::Round($_.Size / 1GB, 1) }
})
$network = @(Get-CimInstance Win32_NetworkAdapter | Where-Object {
  $_.PhysicalAdapter -eq $true -and $_.Name
} | Select-Object -ExpandProperty Name -Unique)
[pscustomobject]@{
  ComputerModel = $computer.Model
  CPU = $cpu
  GPUs = $gpus
  Motherboard = (($board.Manufacturer, $board.Product) -join ' ').Trim()
  BIOS = (($bios.Manufacturer, $bios.SMBIOSBIOSVersion) -join ' ').Trim()
  Windows = $os.Caption
  WindowsVersion = $os.Version
  MemoryGB = [math]::Round($computer.TotalPhysicalMemory / 1GB, 1)
  Disks = $disks
  NetworkAdapters = $network
} | ConvertTo-Json -Depth 5 -Compress
"""
    process = subprocess.run(
        [executable, "-NoProfile", "-NonInteractive", "-Command", script],
        capture_output=True, text=True, encoding="utf-8", errors="replace",
        timeout=45, check=False,
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
    return result


def _as_list(value: Any) -> list[Any]:
    if value is None:
        return []
    return value if isinstance(value, list) else [value]


def hardware_report_markdown(data: dict[str, Any]) -> str:
    lines = [
        "# Simple Hackintosh — Hardware report",
        "",
        "> Generated locally. Serial numbers, MAC addresses, and account identifiers are not collected.",
        "",
        "| Component | Detected value |",
        "|---|---|",
    ]
    fields = (
        ("Computer model", data.get("ComputerModel")),
        ("CPU", data.get("CPU")),
        ("GPU", ", ".join(str(x) for x in _as_list(data.get("GPUs")))),
        ("Motherboard", data.get("Motherboard")),
        ("BIOS", data.get("BIOS")),
        ("Windows", data.get("Windows")),
        ("Windows version", data.get("WindowsVersion")),
        ("Memory", f"{data.get('MemoryGB')} GB" if data.get("MemoryGB") is not None else None),
        ("Disks", "; ".join(
            f"{item.get('Model', 'Unknown')} ({item.get('SizeGB', '?')} GB)"
            for item in _as_list(data.get("Disks")) if isinstance(item, dict)
        )),
        ("Network adapters", ", ".join(str(x) for x in _as_list(data.get("NetworkAdapters")))),
    )
    for label, value in fields:
        text = str(value).strip() if value is not None else "Not reported"
        lines.append(f"| {label} | {text.replace('|', '/')} |")
    lines.extend(["", "This is an inventory report, not a macOS compatibility verdict.", ""])
    return "\n".join(lines)


def _print_findings(findings: Iterable[Finding]) -> tuple[int, int, int]:
    counts = {"PASS": 0, "WARN": 0, "ERROR": 0}
    for finding in findings:
        counts[finding.level] = counts.get(finding.level, 0) + 1
        print(f"[{finding.level}] {finding.message}")
    print()
    print(f"Summary: {counts['PASS']} passed, {counts['WARN']} warning(s), {counts['ERROR']} error(s)")
    return counts["PASS"], counts["WARN"], counts["ERROR"]


def command_doctor() -> int:
    print("Simple Hackintosh CLI")
    print(f"Version: {APP_VERSION}")
    print(f"Operating system: {platform.system()} {platform.release()}")
    print(f"Python: {platform.python_version()}")
    print(f"PowerShell: {_powershell_executable() or 'not found'}")
    print("Dependency status: Python standard library only")
    if sys.version_info < (3, 10):
        print("[WARN] Python 3.10 or newer is recommended.")
        return 1
    print("[PASS] Python version is supported.")
    print("[INFO] This command does not change system settings.")
    return 0


def command_hardware(output_path: str | None) -> int:
    data = collect_hardware()
    report = hardware_report_markdown(data)
    print(report)
    if output_path:
        destination = Path(output_path).expanduser()
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_text(report, encoding="utf-8")
        print(f"Saved report: {destination.resolve()}")
    return 0


def command_efi(path: str) -> int:
    findings, summary = check_efi_path(Path(path).expanduser())
    _print_findings(findings)
    if summary:
        print(f"Scanned {summary['files_scanned']} files; config: {summary['config_path']}")
    return 1 if any(item.level in ("WARN", "ERROR") for item in findings) else 0


def command_config(path: str) -> int:
    source = Path(path).expanduser()
    if not source.is_file():
        print(f"[ERROR] File not found: {source}", file=sys.stderr)
        return 2
    try:
        config = plistlib.loads(source.read_bytes())
    except (OSError, plistlib.InvalidFileException, ValueError, TypeError) as exc:
        print(f"[ERROR] Could not read plist: {exc}", file=sys.stderr)
        return 2
    findings = validate_config_data(config)
    _print_findings(findings)
    print("Note: this is a basic structural check, not the official OpenCore ocvalidate.")
    return 1 if any(item.level == "ERROR" for item in findings) else 0


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        prog="simple-hackintosh",
        description="Local OpenCore EFI checks and Windows hardware inventory.",
        epilog="No browser, account, upload, or third-party Python package is required.",
    )
    parser.add_argument("--version", action="version", version=f"%(prog)s {APP_VERSION}")
    commands = parser.add_subparsers(dest="command", required=True)
    commands.add_parser("doctor", help="Show runtime and dependency status.")
    hardware = commands.add_parser("hardware", help="Collect a local Windows hardware inventory.")
    hardware.add_argument("--output", "-o", help="Save the report as a Markdown file.")
    efi = commands.add_parser("efi-check", help="Check an EFI folder or ZIP and its enabled file references.")
    efi.add_argument("path", help="Path to the EFI folder or ZIP archive.")
    config = commands.add_parser("config-check", help="Parse and inspect a config.plist.")
    config.add_argument("path", help="Path to config.plist (XML and binary plist are supported).")
    return parser


def main(argv: list[str] | None = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)
    try:
        if args.command == "doctor":
            return command_doctor()
        if args.command == "hardware":
            return command_hardware(args.output)
        if args.command == "efi-check":
            return command_efi(args.path)
        if args.command == "config-check":
            return command_config(args.path)
    except (OSError, RuntimeError, zipfile.BadZipFile, subprocess.TimeoutExpired) as exc:
        print(f"[ERROR] {exc}", file=sys.stderr)
        return 2
    parser.error("Unknown command.")
    return 2


if __name__ == "__main__":
    raise SystemExit(main())
