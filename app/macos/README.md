# Simple Hackintosh Studio — macOS Preview

**Status:** early preview (0.1.0), not a full OpenCore Legacy Patcher replacement.

This is the first native SwiftUI desktop front end for the Simple Hackintosh project. It is a graphical workspace rather than a terminal menu.

## Included in this preview

- Native macOS sidebar interface with Overview, macOS Downloader, EFI Builder, and Hardware screens.
- Full-installer discovery through Apple's `softwareupdate --list-full-installers`.
- Full macOS installer download through Apple's `softwareupdate --fetch-full-installer`, followed by copying the completed installer app to the folder selected in Studio.
- Hardware inventory through `system_profiler`; serial numbers, UUIDs and common MAC-address fields are redacted before display and export.
- Starter x64 EFI assembled from the official Acidanthera OpenCore RELEASE archive.
- SHA-256 verification against the digest published in official GitHub release metadata.
- No-overwrite rule: EFI Builder stops if the selected output already contains an EFI folder.

## macOS installer download behavior

Apple's supported full-installer command downloads the installer app into `/Applications`. Studio then copies that completed `.app` bundle into the folder you selected. This avoids Recovery images and does not require the user to work in Terminal, but the internal system disk still needs enough free space for Apple's temporary download. The app does not claim that Apple's `softwareupdate` command can stream directly to an arbitrary external path.

The list shows only full-installer versions currently returned by Apple Software Update for the host system. A version not listed by Apple cannot be selected in this preview.

## EFI safety boundary

The EFI Builder creates a **starter folder**, not a ready-to-boot EFI for any computer. It copies the official x64 OpenCore EFI files and the matching `Sample.plist`. It does not yet select or configure ACPI, Kexts, SMBIOS, NVRAM, Kernel quirks, or device-specific UEFI drivers. Those settings must be created for the exact target hardware and validated with the `ocvalidate` shipped in the same OpenCore release.

Do not use this starter to replace an existing internal EFI. Keep an existing EFI backup and test a hardware-specific configuration from a removable drive first.

## Build

Requirements: macOS 13 or newer and Swift 5.9 or newer.

```bash
swift test --package-path app/macos
swift build --package-path app/macos --configuration release
bash app/macos/scripts/package-app.sh
```

The generated app ZIP is written to `app/macos/dist/Simple-Hackintosh-Studio-macos.zip`.

## Planned next milestones

1. Import the existing Windows Hardware Sniffer `Report.json` and map it to a target hardware profile.
2. Add a guided hardware-profile editor for CPU, GPU, Wi-Fi, Ethernet, audio, storage and firmware.
3. Add version-matched Kext, ACPI and driver selection from official upstream release feeds.
4. Generate and validate a target-specific `config.plist`; block export while required values remain unresolved.
5. Add a guarded USB-installer workflow with explicit disk-erasure confirmation and matching OpenCore validation.
6. Create the Windows GUI using the same profile schema and feature definitions.

## Licensing and downloads

The app downloads OpenCore from the official [Acidanthera OpenCorePkg releases](https://github.com/acidanthera/OpenCorePkg/releases). OpenCore and all third-party Kexts or drivers retain their upstream licenses. This repository does not bundle Apple installers or third-party binaries.
