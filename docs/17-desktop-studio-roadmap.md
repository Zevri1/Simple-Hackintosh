# Desktop Studio roadmap: macOS and Windows

Simple Hackintosh is evolving from a learning guide and CLI into a graphical desktop workflow. The native macOS preview lives in [app/macos](../app/macos/README.md).

## Platform plan

| Capability | macOS app | Windows app |
|---|---|---|
| Hardware inventory | Native `system_profiler` scan; redacted export | Existing Python Hardware Sniffer/PowerShell inventory, exposed through a future GUI |
| Full macOS installer download | Apple's `softwareupdate` full-installer catalog and download | Download Apple-hosted installer packages using Apple's public software-update catalog |
| Save installer files | Copy the completed installer app to the selected folder | Save downloaded package/components directly to the selected folder |
| OpenCore package | Official release download and SHA-256 verification | Same release feed and digest verification |
| EFI starter | Yes | Yes |
| Target-specific EFI | Planned; requires imported or manually completed hardware profile | Planned; reuse the same hardware-profile schema |
| Create a modern bootable macOS USB | Planned native `createinstallmedia` flow | Finish media creation on macOS for modern releases |

## Recommended Windows implementation

Use a native desktop GUI built around the existing Windows hardware-inventory code, not another CMD menu. The preferred architecture is a .NET/Avalonia front end with a shared, versioned JSON hardware-profile format. It can provide drive selection, Apple catalog downloads, OpenCore release verification, EFI assembly, config editing and validation without requiring a terminal window.

The Windows app can download Apple's installer packages and save them to the selected drive. That is different from creating a complete modern bootable installer USB: macOS 11 and newer use the InstallAssistant package workflow, and the upstream gibMacOS project documents that its Windows MakeInstall path cannot build Big Sur-or-newer Internet Recovery USB media. A Mac is therefore the reliable finalization step for a modern full-installer USB. See [gibMacOS](https://github.com/corpnewt/gibMacOS) and Apple's [bootable-installer guide](https://support.apple.com/101578).

## Scope and safety

- Never overwrite an existing EFI without making a verified backup.
- Never show a ready-to-boot status while required hardware fields remain unknown.
- Do not include SMBIOS serials, MLB, ROM, UUIDs or MAC addresses in public support reports.
- Keep Apple installer downloads separate from the OpenCore EFI package.
- Require a second, explicit confirmation before any operation that erases a selected USB volume.
