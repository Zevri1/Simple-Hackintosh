<div align="center">

# Simple Hackintosh

### A practical, illustrated guide to understanding OpenCore

![Simple Hackintosh repository artwork](social-preview.jpg)

**Identify hardware → Check compatibility → Prepare Recovery → Build EFI → Install → Validate**

[**Open the live field guide ↗**](https://zevri1.github.io/Simple-Hackintosh/)

</div>

---

> [!IMPORTANT]
> This is an educational guide, not a one-click installer and not a guarantee that macOS will work on a particular computer. Compatibility depends on the exact CPU, graphics processor, firmware, wireless card, storage controller, and macOS release. Never copy another computer's EFI folder without understanding and adapting every component.

## Python command-line assistant

The repository includes a cross-platform Python assistant (v3.1.0) with a PowerShell-native menu on Windows, a double-clickable macOS Terminal launcher, and hardware inventory on both systems. It opens an interactive numbered menu when launched without arguments and requires no third-party Python packages; the optional Windows OpCore-Simplify report uses the official Hardware Sniffer release after GitHub SHA-256 verification (Authenticode is checked when present), preserves its original JSON, and creates a source-linked, per-component Compatibility Checker in Markdown and text reports, while checking both CLI and Hardware Sniffer releases at startup.

- [Python CLI source, beginner guide, commands, and tests](app/python/README.md)
- [Python CLI test workflow](.github/workflows/python-cli.yml)

Features include Windows/macOS hardware inventory, a Windows OpCore-Simplify-style `Report.json` export using the official Hardware Sniffer (downloaded with a verified GitHub SHA-256 digest and optional Authenticode verification, then driven automatically without an ACPI dump), EFI and Kext reference checks, config.plist linting, official version-matched `ocvalidate` integration, the bundled OpenCore `macrecovery` utility, EFI starter-folder creation, safe backups, SHA-256 comparison, and privacy-conscious support reports.

### Run from a downloaded release

1. Download and extract the [latest CLI ZIP](https://github.com/Zevri1/Simple-Hackintosh/releases/latest).
2. Open the extracted folder; `run.bat`, `run.ps1`, `run.command`, and `simple_hackintosh.py` are together at its top level.
3. On Windows, double-click `run.bat`, or open PowerShell in that folder and run `./run.ps1`. If PowerShell blocks the script, run `Set-ExecutionPolicy -Scope Process Bypass` in the same window, then retry.
4. On macOS, Control-click `run.command` and choose **Open**, or open Terminal in that folder and run `chmod +x ./run.command && ./run.command`.

Python 3.10 or newer is required. From a repository checkout rather than the release ZIP, run `cd app/python` first. The Windows Hardware Sniffer report option is Windows-only; macOS uses the native `system_profiler` inventory. The Windows report is exported automatically; macOS uses its native inventory. See [the CLI guide](app/python/README.md) for details.

## Native macOS desktop app (preview)

The native SwiftUI app has a graphical workspace for Apple's full-installer catalog, direct InstallAssistant package downloads to a chosen drive, hardware inventory, and a checksum-verified OpenCore starter EFI. The EFI result is deliberately labelled as a starter, not a ready-to-boot configuration.

- [macOS Studio preview and build instructions](app/macos/README.md)
- [Desktop app roadmap and shared hardware profile](docs/17-desktop-studio-roadmap.md)
- [Hardware profile JSON Schema v1](schemas/hardware-profile-v1.schema.json)

The Windows desktop app is also present as a native .NET 10 Windows Forms utility with EFI Checker, Config Validator, and Hardware Inspector screens. The planned next step is to add the same Apple catalog downloader and EFI builder to Windows.

## What this project is

Simple Hackintosh teaches the concepts and workflow behind installing and maintaining macOS on non-Apple x86-64 hardware with OpenCore. The goal is to explain *why* each part of an EFI exists, how to check whether hardware is supported, and how to validate a configuration before relying on it.

This repository does **not** distribute macOS installers, Apple firmware, third-party binary packages, prebuilt device-specific EFI folders, or personal SMBIOS identifiers. Obtain tools and Kexts from their official project sources and respect their licenses.

## The learning path

| Stage | Goal | Chapter |
|---|---|---|
| 0 | Understand risks, requirements, and legal notes | [Read first](docs/00-read-first.md) |
| 1 | Collect exact hardware names and PCI IDs | [Hardware inventory](docs/01-hardware-inventory.md) |
| 2 | Check every important component for compatibility | [Compatibility](docs/02-compatibility.md) |
| 3 | Prepare a macOS Recovery USB from Windows | [Windows USB installer](docs/03-usb-installer-windows.md) |
| 4 | Understand the EFI folder and its required structure | [EFI structure](docs/04-efi-structure.md) |
| 5 | Select ACPI tables, Kexts, and UEFI drivers | [ACPI, Kexts, and drivers](docs/05-acpi-kexts-drivers.md) |
| 6 | Configure OpenCore for the actual hardware | [config.plist](docs/06-config-plist.md) |
| 7 | Validate the EFI and collect useful logs | [Validation and debugging](docs/07-validation-debugging.md) |
| 8 | Boot Recovery and install macOS carefully | [Installation](docs/08-installation.md) |
| 9 | Test hardware after the first boot | [Post-installation](docs/09-post-installation.md) |
| 10 | Map USB ports correctly | [USB mapping](docs/10-usb-mapping.md) |
| 11 | Review macOS 26-specific caveats | [Tahoe 26](docs/11-macos-tahoe-26.md) |
| 12 | Protect existing systems and boot files | [Dual boot](docs/12-dual-boot.md) |
| 13 | Maintain OpenCore and prepare updates | [Maintenance](docs/13-maintenance.md) |
| 14 | Diagnose common startup and hardware problems | [Troubleshooting](docs/14-troubleshooting.md) |
| 15 | Learn the terminology | [Glossary](docs/15-glossary.md) |
| 16 | See annotated screenshots of key stages | [Visual walkthrough](docs/16-visual-walkthrough.md) |

## Before starting

1. Read [Read first](docs/00-read-first.md), including backup and legal notes.
2. Fill in the [hardware report template](templates/hardware-report.md).
3. Check the exact CPU, GPU, Wi-Fi/Bluetooth, Ethernet, audio codec, and storage controller. A product-family name alone is not enough.
4. Choose a macOS version only after checking the whole computer against current documentation.
5. Keep a verified backup of personal files and any existing boot configuration before changing firmware or disk partitions.

## How OpenCore fits together

OpenCore is a boot loader. It prepares the environment macOS expects by loading selected ACPI tables, drivers, and Kexts, and by applying settings from config.plist. It does not make unsupported hardware magically compatible. Each file must have a reason to exist and must match the computer and the OpenCore version in use.

A typical EFI contains:

- BOOT: the UEFI fallback boot file.
- OC: the OpenCore folder.
- ACPI: selected ACPI tables.
- Drivers: required UEFI drivers.
- Kexts: kernel extensions and their dependencies.
- Resources: optional picker assets.
- Tools: optional boot-time utilities.
- config.plist: the configuration that connects these parts.

See [EFI structure](docs/04-efi-structure.md) before assembling files.

## Visual learning

The [visual walkthrough](docs/16-visual-walkthrough.md) includes reference screenshots for USB preparation, Recovery files, EFI layout, and ProperTree. Those screenshots are illustrative; they are not a ready-made configuration. Always follow the current official documentation.

## Official references

- [Dortania OpenCore Install Guide](https://dortania.github.io/OpenCore-Install-Guide/)
- [OpenCorePkg releases](https://github.com/acidanthera/OpenCorePkg/releases)
- [OpenCorePkg documentation](https://github.com/acidanthera/OpenCorePkg/tree/master/Docs)
- [OpenCore Post-Install Guide](https://dortania.github.io/OpenCore-Post-Install/)
- [Getting Started with ACPI](https://dortania.github.io/Getting-Started-With-ACPI/)
- [Dortania macOS 26 / Tahoe notes](https://dortania.github.io/OpenCore-Install-Guide/extras/tahoe.html)
- [GPU Buyers Guide](https://dortania.github.io/GPU-Buyers-Guide/)
- [Wireless Buyers Guide](https://dortania.github.io/Wireless-Buyers-Guide/)

**Version rule:** Download one OpenCore release and use its matching EFI binaries, Sample.plist, tools, and ocvalidate. The generated Dortania guide may display a different supported-version banner from the newest GitHub release. Do not mix files from different releases; check both the guide and documentation shipped with the exact package.

## Repository layout

- docs/: the numbered learning chapters.
- templates/: blank hardware and troubleshooting report forms.
- social-preview.jpg: repository sharing artwork.
- social-preview-640.jpg: smaller preview image.
- SOURCES.md: primary technical references.
- CONTRIBUTING.md: documentation contribution rules.
- SUPPORT.md: how to prepare a useful, redacted support report.

## Safety, privacy, and licensing

Disk formatting and partition changes can erase data. Firmware changes can prevent a computer from starting until settings are restored. Back up first, verify the target disk by model and capacity, and do not change settings you do not understand.

Do not publish serial numbers, MLB, SystemUUID, ROM, MAC addresses, passwords, account details, or an unredacted EFI. Do not upload macOS installers, Apple firmware, or third-party binaries unless redistribution is explicitly permitted.

Apple's software license and local law may restrict installing or running macOS on non-Apple hardware. Read the applicable terms yourself; this project is not legal advice.

The original documentation is released under the MIT License. External software, screenshots, trademarks, and third-party documentation remain subject to their own licenses and terms. See [LICENSE](LICENSE).

## Project status

This is a learning reference. Hardware-specific instructions should state the exact scope and should be checked against current upstream documentation before use.
