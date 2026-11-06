# Desktop Studio: macOS and Windows

Simple Hackintosh now has two native desktop application projects beside the Python CLI. They share the same safety model and should converge on one privacy-conscious hardware-profile format.

## Current applications

| Platform | UI/runtime | Current preview capabilities |
|---|---|---|
| macOS | SwiftUI, macOS 13+ | Apple full-installer catalog and direct package download; OpenCore starter EFI with SHA-256 check; redacted system hardware inventory |
| Windows | Windows Forms, .NET 10, Windows 10/11 | EFI Checker; Config Validator; read-only Hardware Inspector |

## Shared hardware profile

The apps should exchange hardware facts through [Hardware Profile JSON Schema v1](../schemas/hardware-profile-v1.schema.json). It covers CPU, GPU, network, audio, storage and firmware. Serial numbers, SMBIOS identifiers, UUIDs and network MAC addresses are deliberately excluded.

This makes the workflow portable: scan hardware in Windows, export a profile, import it into macOS Studio, and use it to guide configuration choices.

## Installer download design

Both applications can read Apple's public software-update catalog, select only products marked as full InstallAssistant installers, and stream the Apple-hosted `InstallAssistant.pkg` to a user-selected directory. The macOS implementation is in the preview. The same catalog filtering and HTTPS host checks should be added to the Windows GUI with .NET `HttpClient`.

The package download is not Recovery. The downloaded `InstallAssistant.pkg` can later be opened in macOS to install the full installer application.

## Windows implementation plan

Keep the existing WinForms/.NET 10 desktop app; it already provides a native graphical shell and three diagnostic screens. Extend it with:

1. Apple installer browser and direct download-to-drive.
2. Official OpenCore release management with SHA-256 verification.
3. EFI starter creation and profile-driven configuration.
4. XML plist editing and matching official `ocvalidate` checks.
5. Shared hardware profile import/export and compatibility notes.

Do not create a parallel CMD or PowerShell front end. Keep downloads and file operations in cancellable background tasks so the UI remains responsive.

## Bootable USB boundary

Windows can download the Apple package and prepare EFI assets. For Big Sur (macOS 11) and newer, the legacy Windows `MakeInstall` flow in gibMacOS cannot create the complete bootable installer media. Apple's documented full-installer USB creation uses `createinstallmedia` inside macOS. See [gibMacOS](https://github.com/corpnewt/gibMacOS) and [Apple's bootable-installer guide](https://support.apple.com/101578).

The recommended dual-boot workflow is therefore:
1. Download `InstallAssistant.pkg` and build/inspect EFI in Windows.
2. Reboot into macOS.
3. Open the saved package to install the installer application.
4. Use the macOS Studio USB Builder (planned) to create the bootable USB, with explicit disk identity and erase confirmation.
5. Test EFI from removable media before changing internal boot files.

## Safety requirements

- Never overwrite an existing EFI without a verified backup.
- Never label a sample or generic EFI as ready to boot.
- Require explicit confirmation before erasing a USB disk or changing partitions.
- Display disk model, capacity and mount point before destructive operations.
- Keep SMBIOS identifiers and MAC addresses out of exported profiles.
- Do not bundle Apple installer packages or third-party binaries in releases.
