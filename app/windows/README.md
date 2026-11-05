# Simple Hackintosh — Windows Desktop App

A native Windows utility companion for the Simple Hackintosh field guide.

## First release scope

- **EFI Checker:** inspects a selected EFI directory or ZIP archive without extracting it, checks the basic OpenCore layout, parses XML config.plist, and tests enabled ACPI, Kext, and UEFI driver file references.
- **Config Validator:** parses XML property lists securely and checks common OpenCore sections and value container types.
- **Hardware Inspector:** uses read-only Windows CIM queries to collect CPU, graphics, motherboard, BIOS, storage, network and audio details, then lets the user copy or save a Markdown report.

## Privacy and safety

All file inspection takes place locally on the Windows computer. The app does not upload EFI folders, configuration files, or hardware reports. The hardware inspection performs read-only CIM queries and does not require administrator privileges. The app never modifies an EFI, config.plist, firmware setting, partition, or boot entry.

The first release is a structural helper. It does not replace OpenCore's official `ocvalidate` tool, the Dortania guide, or a hardware-specific compatibility review. A clean report is not proof that an EFI will boot.

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

Windows x64, version 0.1.0.
