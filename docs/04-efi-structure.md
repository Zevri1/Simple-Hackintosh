# 04 — Understand the EFI structure

The EFI System Partition (ESP) is a small FAT-formatted partition used by UEFI firmware to locate boot files. OpenCore's files must be arranged in the expected directory structure.

## Typical directory layout

A standard x64 OpenCore layout looks like this:

- EFI/BOOT/BOOTx64.efi — UEFI fallback loader.
- EFI/OC/OpenCore.efi — OpenCore boot loader.
- EFI/OC/config.plist — main configuration.
- EFI/OC/ACPI/ — selected ACPI tables.
- EFI/OC/Drivers/ — required UEFI drivers.
- EFI/OC/Kexts/ — kernel extensions.
- EFI/OC/Resources/ — optional picker resources.
- EFI/OC/Tools/ — optional boot-time utilities.

Not every directory needs files. Do not add files just to make the layout look full.

## 1. Start from one release

Download a single OpenCorePkg release. Use its own X64/EFI files, Sample.plist, utilities, and documentation. Mixing versions can create confusing validation errors and boot failures.

## 2. Understand the main file groups

### ACPI

ACPI tables describe hardware and firmware interfaces. A patched table may correct a specific firmware behavior, but an unnecessary or incorrect table can introduce new problems. See [05 — ACPI, Kexts, and drivers](05-acpi-kexts-drivers.md).

### Drivers

UEFI drivers run before the macOS kernel. Add only what the chosen boot workflow requires. More drivers do not automatically mean better compatibility.

### Kexts

Kexts extend or modify macOS kernel functionality. Dependencies and load order matter. Keep each Kext's official documentation with your notes.

### Resources and Tools

Resources are used by the OpenCore picker for optional graphics and language assets. Tools are optional utilities. Do not copy every sample resource or utility into an EFI without a reason.

## 3. Keep the folder and config synchronized

Every item enabled in config.plist must exist at the configured path, and every loaded item should be enabled deliberately. An item that exists on disk but is not configured may not load.

## 4. Keep a clean backup

Before each meaningful change, copy the whole working EFI to a separate location. Label each copy with its OpenCore release and date. Do not overwrite the only known-good EFI.

## 5. Avoid prebuilt EFI folders

A prebuilt EFI from a similar-looking computer may contain incorrect ACPI tables, device properties, SMBIOS values, or platform settings. Use it only as a learning reference unless it has been audited and adapted for the exact hardware.

## Next chapter

Select only the required components in [05 — ACPI, Kexts, and drivers](05-acpi-kexts-drivers.md).
