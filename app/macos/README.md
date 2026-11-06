# Simple Hackintosh Studio — macOS Preview

**Status:** early preview (0.1.0). This is not yet a complete OpenCore Legacy Patcher replacement.  
**UI:** native SwiftUI. **Minimum OS:** macOS 13 Ventura.

Simple Hackintosh Studio is the native macOS desktop companion to the existing Python CLI and Windows desktop utility.

## Included

- Native macOS sidebar with Overview, macOS Downloader, EFI Builder and Hardware screens.
- Apple public software-update catalog discovery for full macOS installers.
- Direct streaming download of Apple's `InstallAssistant.pkg` to the directory selected in the app. The file is written to the selected volume as it downloads; it is not staged in `/Applications` and no Recovery image is used.
- Partial-file cleanup and downloaded-byte-count verification against the size in Apple's catalog.
- Official OpenCore x64 RELEASE archive download with GitHub's published SHA-256 digest checked before extraction.
- Starter EFI creation using the matching OpenCore `Sample.plist`.
- Hardware inventory through `system_profiler`; common serial, UUID and MAC-address fields are redacted before display and export.

## Apple installer download

The app reads Apple's public software-update catalog, filters products marked as full InstallAssistant installers, then selects the `InstallAssistant.pkg` package URL. Only HTTPS URLs under Apple domains are accepted. The package is streamed directly to a user-selected folder, including external drives.

The output is a full Apple installer package, not a Recovery image. Once downloaded, opening the package in Finder runs Apple's Installer and installs the macOS installer application (normally into `/Applications`). The downloaded `.pkg` remains on the selected drive.

The version list depends on products Apple currently publishes in the public catalog. Studio does not promise that all historical releases remain available.

## EFI safety boundary

The EFI Builder creates an **OpenCore starter folder**, not a ready-to-boot EFI for every PC. It fetches the official x64 RELEASE archive, checks its SHA-256 digest against GitHub release metadata, copies the official EFI folder and places the matching `Sample.plist` at `OC/config.plist`.

It does not yet choose or configure target-specific ACPI, Kexts, SMBIOS, NVRAM, Kernel quirks or UEFI drivers. These must be selected for the exact target hardware and validated with `ocvalidate` from the same OpenCore release.

The builder never overwrites an existing EFI. Back up any existing EFI and test a fully configured result from removable media before changing internal boot files.

## Build

Requirements: macOS 13 or newer, Xcode Command Line Tools, and Swift 5.9 or newer.

```bash
swift test --package-path app/macos
swift build --package-path app/macos --configuration release
bash app/macos/scripts/package-app.sh
```

The GitHub Actions workflow builds both Apple Silicon and Intel executables, combines them into a universal app, and uploads:

`app/macos/dist/Simple-Hackintosh-Studio-macos.zip`

The preview is not Developer ID signed or notarized. A public release should be signed and notarized before broad distribution.

## Next milestones

1. Import the Windows hardware report and build a shared target-hardware profile.
2. Select Kexts, ACPI tables and UEFI drivers from official upstream release feeds.
3. Generate a hardware-specific `config.plist` and validate it with the matching `ocvalidate`.
4. Add a guarded USB Builder using Apple's `createinstallmedia`, with the selected disk model/capacity shown before a separate erase confirmation.
5. Add a shared hardware-profile editor and installer-catalog downloader to the Windows GUI.

## Upstream sources

- [Apple macOS software downloads](https://support.apple.com/102662)
- [Apple bootable installer guide](https://support.apple.com/101578)
- [OpenCorePkg official releases](https://github.com/acidanthera/OpenCorePkg/releases)
- [gibMacOS catalog and package workflow](https://github.com/corpnewt/gibMacOS)
