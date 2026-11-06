# Simple Hackintosh — Windows Desktop App

A native Windows Forms (.NET 10) desktop companion for the Simple Hackintosh field guide. It is a graphical application, not a CMD menu.

## Included in preview 0.4.0

- **EFI Checker:** inspects a selected EFI directory or ZIP archive without extracting it, checks the basic OpenCore layout, parses XML config.plist, and tests enabled ACPI, Kext, and UEFI driver file references.
- **Config Validator:** parses XML property lists securely, checks common OpenCore sections and value container types, and can download the official Windows `ocvalidate.exe` from the latest stable Acidanthera release after verifying the enclosing ZIP digest. It checks the validator's embedded OpenCore version before running it and reports upstream findings.
- **Hardware Inspector:** uses read-only Windows CIM queries to collect CPU, graphics, motherboard, BIOS, storage, network and audio details, then lets the user copy or save a Markdown report.
- **macOS Downloader:** reads Apple's public full-installer catalog, lists available macOS releases, and streams the Apple-hosted `InstallAssistant.pkg` directly to a folder or external drive selected in the GUI. Shows progress and supports cancellation.
- **OpenCore Builder:** discovers the latest stable release from the official Acidanthera GitHub repository, checks the release asset SHA-256 digest, and creates an x64 EFI starter with the matching `Sample.plist`. ZIP paths, links, archive size and output collisions are checked before creating the final `EFI` folder.
- **Hardware Profile v1:** exports detected Windows hardware as JSON compatible with the shared schema and imports/validates schema-v1 profiles. PCI vendor/device IDs are retained, while serial numbers, UUIDs, SMBIOS identifiers and network MAC addresses are not exported.

## Remaining Windows roadmap

1. Improve Windows hardware inventory and make PCI/ACPI identification more complete without exporting sensitive identifiers.
2. Add a guided configuration editor using the exact schema shipped with the selected OpenCore version.
1. Add a guided configuration editor using the exact schema shipped with the selected OpenCore version.
2. Add official Kext/ACPI/driver release sources with compatibility and version matching.
3. Design a separate, heavily guarded removable-USB workflow; it must show exact disk identity and require a second erase confirmation. Internal disks remain out of scope.
4. Improve Windows hardware inventory and PCI/ACPI identification without exporting sensitive identifiers.

The official validator action uses the latest stable release available from Acidanthera. The selected `config.plist` must belong to that same OpenCore version. If the official release archive does not include a Windows `ocvalidate.exe`, the app stops and explains that it will not substitute an unofficial binary.

Windows can download Apple's full installer package and prepare EFI assets. For macOS 11 Big Sur and newer, it cannot use the traditional gibMacOS Windows `MakeInstall` flow to create a complete bootable installer USB; the final media-creation step uses Apple's `createinstallmedia` on macOS. The Windows GUI should therefore support a clear handoff: download in Windows, then finish USB creation from macOS.

## Privacy and safety

All current file inspection takes place locally on the Windows computer. The app does not upload EFI folders, configuration files, or hardware reports. Hardware inspection performs read-only CIM queries and does not require administrator privileges. The app never modifies an EFI, config.plist, firmware setting, partition, or boot entry.

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

Windows x64, version 0.4.0 (preview).
