# Simple Hackintosh — Windows Desktop App

A native Windows Forms (.NET 10) desktop companion for the Simple Hackintosh field guide. It is a graphical application, not a CMD menu.

## Included in preview 0.5.0

- **EFI Checker:** inspects a selected EFI directory or ZIP archive without extracting it, checks the basic OpenCore layout, parses XML config.plist, and tests enabled ACPI, Kext, and UEFI driver file references.
- **Config Validator:** parses XML property lists securely, checks common OpenCore sections and value container types, and can download the official Windows `ocvalidate.exe` from a selected stable Acidanthera release after verifying the enclosing ZIP digest. A release picker supports older OpenCore versions. It can also import a local official RELEASE ZIP for offline validation after matching its SHA-256 against the digest published for that exact GitHub release asset. It checks the validator's embedded OpenCore version before running it and reports upstream findings.
- **Hardware Inspector:** uses read-only Windows CIM queries to collect CPU, graphics, motherboard, BIOS, storage, network and audio details, then lets the user copy or save a Markdown report.
- **macOS Downloader:** reads Apple's public full-installer catalog, lists available macOS releases, and streams the Apple-hosted `InstallAssistant.pkg` directly to a folder or external drive selected in the GUI. Shows progress and supports cancellation.
- **OpenCore Builder:** discovers the latest stable release from the official Acidanthera GitHub repository, checks the release asset SHA-256 digest, and creates an x64 EFI starter with the matching `Sample.plist`. ZIP paths, links, archive size and output collisions are checked before creating the final `EFI` folder.
- **Hardware Profile v1:** exports detected Windows hardware as JSON compatible with the shared schema and imports/validates schema-v1 profiles. It preserves PCI IDs and exact HDAUDIO codec vendor/device IDs when Windows exposes them, while omitting device instance paths, serial numbers, UUIDs, SMBIOS identifiers and network MAC addresses.
- **Kext Manager and Official Kext Library:** provide two curated views of official Acidanthera Kext releases. Downloads require the GitHub-published SHA-256 digest, verify the ZIP, reject unsafe archive paths, and extract only Kext bundles into a new folder. The expanded library also shows dependency notes, upstream macOS compatibility statements, and direct documentation links. These notes do not replace checking the exact hardware and target macOS; no Kext is injected into an existing EFI or enabled automatically. The library also offers a local Hardware-to-Kext Review that cross-checks the detected PCI IDs, adapters, GPU, HDAUDIO codec IDs and storage against known upstream evidence, while explicitly marking unknowns and review-only matches. It does not modify `config.plist`, partitions, or firmware.
- **Official Kext Library:** checks stable releases from a curated set of upstream Acidanthera repositories, requires a GitHub-published SHA-256 digest before downloading, verifies the archive, and extracts only `.kext` bundles into a new staging folder. Existing EFI folders and `config.plist` files are never changed.
- **Config Editor:** edits existing String, Integer, Real and Boolean values in an XML config.plist, supports search by property path, and saves to a new file without overwriting the source. Arrays, dictionaries, Data and Date values remain read-only; run version-matched `ocvalidate` after editing.

## Remaining Windows roadmap

1. Expand the scalar Config Editor into a complete schema-guided editor using the exact schema shipped with the selected OpenCore version. The current editor deliberately leaves arrays, dictionaries, Data and Date values read-only.
2. Expand compatibility analysis by adding versioned device-ID and macOS support data with automated refresh/tests. The current Hardware-to-Kext Review is deliberately conservative and still requires manual review.
3. Design a separate, heavily guarded removable-USB workflow; it must show exact disk identity and require a second erase confirmation. Internal disks remain out of scope.
4. Improve Windows hardware inventory and PCI/ACPI identification without exporting sensitive identifiers.

The official validator can use a selected stable Acidanthera release or a local official RELEASE ZIP. Offline imports require the SHA-256 digest published for that exact asset on GitHub; the app hashes the archive before extraction and refuses a mismatched file. The selected `config.plist` must belong to the same OpenCore version as the validator. If the official release archive does not include a Windows `ocvalidate.exe`, the app refuses to substitute an unofficial binary.

Windows can download Apple's full installer package and prepare EFI assets. For macOS 11 Big Sur and newer, it cannot use the traditional gibMacOS Windows `MakeInstall` flow to create a complete bootable installer USB; the final media-creation step uses Apple's `createinstallmedia` on macOS. The Windows GUI should therefore support a clear handoff: download in Windows, then finish USB creation from macOS.

## Privacy and safety

All current file inspection takes place locally on the Windows computer. The app does not upload EFI folders, configuration files, or hardware reports. Hardware inspection performs read-only CIM queries and does not require administrator privileges. The builder creates a new starter folder only at the destination selected by the user; it never overwrites an existing EFI, edits an existing config.plist, changes firmware settings, partitions disks, or modifies boot entries.

The EFI starter uses the upstream `Sample.plist`; it does not replace the Dortania guide or a hardware-specific compatibility review. Official `ocvalidate` checks the config against its own matching OpenCore release, but a clean report is not proof that an EFI will boot.

## Build

Requirements: Windows 10/11 and .NET 10 SDK.

Run the automated safety and profile tests first:

```powershell
dotnet test .\app\windows.tests\SimpleHackintosh.Tests.csproj -c Release
```

Start the GUI:

```powershell
dotnet run --project .\app\windows\SimpleHackintosh.App.csproj
```

Publish a self-contained x64 single-file app:

```powershell
dotnet publish .\app\windows\SimpleHackintosh.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\artifacts\windows-x64
```

The Windows GitHub Actions workflow builds the app and stores the portable executable as a workflow artifact.

## Current target

Windows x64, version 0.5.0 (preview).
