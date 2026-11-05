# 15 — Glossary

| Term | Meaning |
|---|---|
| ACPI | Firmware-provided tables describing hardware devices and power-management interfaces |
| APFS | Apple's file system used by modern macOS installations |
| BIOS / UEFI | Firmware that initializes hardware and starts an operating system; UEFI is the modern firmware interface |
| Boot argument | A value passed to macOS during startup |
| config.plist | OpenCore's property-list configuration file |
| EFI System Partition (ESP) | A small partition containing UEFI boot files |
| EFI folder | The directory structure containing OpenCore and its configuration |
| Kext | Kernel extension; a macOS driver or kernel-level extension |
| Lilu | A dependency Kext used by several other Kext projects |
| MLB | A system identifier used in SMBIOS data; keep it private |
| NVRAM | Non-volatile firmware variables used by the system and boot loader |
| OpenCore | A UEFI boot loader and platform-preparation environment |
| PCI ID | Vendor/device identifier used to identify a PCI or PCIe device |
| Plist | Property List, a structured configuration file format |
| ProperTree | A cross-platform property-list editor commonly used for OpenCore |
| Recovery | Apple's recovery environment used to install or repair macOS |
| SMBIOS | System-management information used to identify a Mac model to macOS |
| SSDT | Secondary System Description Table, an ACPI table |
| SystemUUID | A unique system identifier; do not publish it |
| USB map | A configuration describing physical USB ports and their controller mapping |
| UEFI driver | A driver loaded by firmware/OpenCore before the macOS kernel |
| Validator | A tool that checks an OpenCore configuration for structural and version-related errors |

## Related chapters

- [EFI structure](04-efi-structure.md)
- [ACPI, Kexts, and drivers](05-acpi-kexts-drivers.md)
- [config.plist](06-config-plist.md)
