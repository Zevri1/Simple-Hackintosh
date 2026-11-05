<div align="center">

# Simple Hackintosh

### A practical, illustrated guide to understanding OpenCore

![Simple Hackintosh repository artwork](social-preview.jpg)

**Identify hardware → Check compatibility → Prepare Recovery → Build EFI → Install → Validate**

</div>

---

> [!IMPORTANT]
> This is an educational guide, not a one-click installer and not a guarantee that macOS will work on a particular computer. Compatibility depends on the exact CPU, graphics processor, firmware, wireless card, storage controller, and macOS release. Never copy another computer's EFI folder without understanding and adapting every component.

## About this project

A **Hackintosh** is a non-Apple computer configured to run macOS with the help of a boot loader and carefully selected, hardware-specific components. It is not the same as installing an ordinary operating system on a standard PC. The firmware, ACPI tables, graphics, storage, networking, audio, and power-management behavior of a computer can differ significantly from the hardware macOS was designed for. Because of these differences, a successful setup requires research, a deliberate configuration, and careful testing—not simply copying an EFI folder from a computer that looks similar.

**Simple Hackintosh** is a learning resource that explains this process from the ground up. It is intended to help readers understand what each part of an OpenCore setup does, how to identify the hardware in their own computer, how to evaluate compatibility before installation, and how to troubleshoot problems methodically. The guide follows the complete journey: researching hardware, preparing macOS Recovery media, building an EFI, configuring OpenCore, installing macOS, checking device functionality, and maintaining the installation over time.

The project focuses on understanding rather than guesswork. Instead of presenting one universal EFI or a list of settings that supposedly works on every computer, it explains how to find the information needed for a particular platform. A setting that is correct for one CPU generation, graphics device, or firmware implementation may be incorrect for another. Every recommendation must therefore be checked against the exact hardware and the macOS and OpenCore versions being used.

## What is OpenCore, and why is it needed?

**OpenCore** is a boot loader and platform-initialization environment. On a compatible UEFI-based system, it helps prepare the machine for macOS by loading selected firmware drivers, ACPI tables, and kernel extensions (Kexts), and by applying the options defined in the OpenCore configuration file. These components help bridge differences between the computer's firmware and the environment expected by macOS.

OpenCore is not an operating system, a driver that supports every device, or an automatic compatibility patch. It cannot create graphics acceleration for an unsupported GPU or guarantee that an unsupported wireless chipset will work. Its configuration must be built from the actual hardware inventory and the documentation for the target macOS release. That is why this guide treats hardware research and compatibility checking as essential steps, not optional preparation.

## What you will learn

By following the chapters in order, you will learn how to:

- **Identify your hardware accurately:** collect exact CPU, GPU, network, audio, storage, and firmware information instead of relying on a computer's marketing name.
- **Evaluate compatibility:** determine which components have a documented support path for the macOS version you are considering, and recognize limitations before making changes.
- **Prepare installation media:** understand the role of macOS Recovery, prepare a USB drive using current official instructions, and distinguish Recovery files from the OpenCore EFI.
- **Build an EFI directory:** understand the purpose of BOOT, OC, ACPI, Drivers, Kexts, Resources, Tools, and the configuration file.
- **Configure OpenCore carefully:** use the matching Sample.plist, select only required components, review dependencies, and keep configuration entries synchronized with the files on disk.
- **Validate and troubleshoot:** use the matching ocvalidate tool, understand common failure stages, and make changes one at a time.
- **Check the installed system:** test graphics acceleration, networking, Bluetooth, audio, USB, sleep, restart, and other relevant hardware features.
- **Maintain the setup:** keep backups, track component versions, and plan macOS and OpenCore updates without losing the only known-good boot configuration.

The chapters are written as a connected learning path. You can use an individual page as a reference, but readers who are new to OpenCore should begin with the first chapter and move forward in sequence.

## Who this guide is for

This guide is intended for readers who want to understand the technical process, are willing to identify their hardware precisely, and can follow detailed instructions carefully. Basic familiarity with computer hardware, UEFI firmware, files and folders, and operating-system installation is helpful. Some chapters also use terms such as ACPI, Kext, PCI ID, SMBIOS, and NVRAM; the glossary explains these terms.

You do not need to know every OpenCore setting before beginning. You do need to be prepared to research unfamiliar components, compare official documentation, keep notes, and stop when a device or instruction is not clearly understood. The purpose of the guide is to make that process easier to follow—not to remove the need for hardware-specific research.

## What this project does not provide

This repository is documentation, not an installer or a ready-to-boot EFI collection. It does **not** distribute macOS installers, Apple firmware, third-party binary packages, prebuilt device-specific EFI folders, personal SMBIOS identifiers, or guaranteed fixes for particular hardware. It also does not claim that every PC can run every macOS release.

Obtain OpenCore, Kexts, utilities, and other tools from their official project sources. Follow the licenses and distribution terms that apply to each component. The MIT license in this repository covers only the original documentation written for this project; it does not relicense Apple's software, third-party tools, screenshots, or external documentation.

## Important compatibility and safety principles

- **Compatibility is component-specific.** A supported CPU does not guarantee a supported GPU, Wi-Fi card, audio codec, storage controller, or sleep implementation.
- **Versions matter.** OpenCore files, Sample.plist, utilities, and ocvalidate should come from the same OpenCore release. Kexts and other dependencies must also be checked for the target macOS version.
- **Backups come first.** Formatting a USB drive, changing partitions, or modifying an EFI can cause data loss or make an existing operating system temporarily unbootable. Back up important data and existing boot files before proceeding.
- **Private identifiers must stay private.** Never publish serial numbers, MLB, SystemUUID, ROM, MAC addresses, passwords, account information, or an unredacted personal EFI.
- **Check the applicable terms.** Apple's software license and local law may restrict installing or running macOS on non-Apple hardware. Read the applicable terms yourself; this repository is not legal advice.

The safest approach is to research first, build a minimal and understandable configuration, validate it, test from a recoverable boot path, and document every change. If a file, setting, or disk target is unclear, stop and verify it rather than guessing.

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
