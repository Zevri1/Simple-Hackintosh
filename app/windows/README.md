# Simple Hackintosh — Windows Desktop App

A native Windows Forms (.NET 10) desktop companion for the Simple Hackintosh field guide. It is a graphical application, not a CMD menu.

## Included in preview 0.1.0

- **EFI Checker:** inspects a selected EFI directory or ZIP archive without extracting it, checks the basic OpenCore layout, parses XML config.plist, and tests enabled ACPI, Kext, and UEFI driver file references.
- **Config Validator:** parses XML property lists securely and checks common OpenCore sections and value container types.
- **Hardware Inspector:** uses read-only Windows CIM queries to collect CPU, graphics, motherboard, BIOS, storage, network and audio details, then lets the user copy or save a Markdown report.
- **macOS Downloader:** reads Apple's public full-installer catalog, lists available macOS releases, and streams the Apple-hosted `InstallAssistant.pkg` directly to a folder or external drive selected in the GUI. Shows progress and supports cancellation.

## Windows roadmap

The next Windows desktop iteration should add:

1. An OpenCore release downloader that checks the official GitHub SHA-256 digest before extracting files.
3. An EFI starter builder and a profile-driven hardware-specific EFI workflow.
4. A config editor and official, version-matched `ocvalidate` integration.
5. Import/export of the shared [Hardware Profile JSON Schema v1](../../schemas/hardware-profile-v1.schema.json).

Windows can download Apple's full installer package and prepare EFI assets. For macOS 11 Big Sur and newer, it cannot use the traditional gibMacOS Windows `MakeInstall` flow to create a complete bootable installer USB; the final media-creation step uses Apple's `createinstallmedia` on macOS. The Windows GUI should therefore support a clear handoff: download in Windows, then finish USB creation from macOS.

## Privacy and safety

All current file inspection takes place locally on the Windows computer. The app does not upload EFI folders, configuration files, or hardware reports. Hardware inspection performs read-only CIM queries and does not require administrator privileges. The app never modifies an EFI, config.plist, firmware setting, partition, or boot entry.

The current release is a structural helper. It does not replace OpenCore's official `ocvalidate` tool, the Dortania guide, or a hardware-specific compatibility review. A clean report is not proof that an EFI will boot.

## Build

Requirements: Windows 10/11 and .NET 10 SDK.

```powershell
dotnet run --project .\app\windows\SimpleHackintosh.App.csproj
```

Publish a self-contained x64 single-file app:

```powershell
dotnet publish .\app\windows\SimpleHackintosh.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\artifacts\windows-x64
```

The Windows GitHub Actions workflow builds the app and stores the portable executable as a workflow artifact.

## Current target

Windows x64, version 0.2.0 (preview).
