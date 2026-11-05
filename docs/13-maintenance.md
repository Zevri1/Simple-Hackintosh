# 13 — Maintenance and updates

An OpenCore installation is a collection of versioned components. Updating only one part without checking the rest can break a previously working configuration.

## 1. Keep a change log

Record:

- macOS version and build;
- OpenCore release;
- Kext names and versions;
- UEFI drivers and ACPI files;
- SMBIOS model choice (not private identifiers);
- hardware-specific settings;
- last known-good EFI location and date.

## 2. Back up first

Before changing macOS, OpenCore, Kexts, ACPI, or firmware settings:

1. Copy the complete working EFI to a separate location.
2. Confirm the backup can be opened and contains the expected files.
3. Keep access to a recovery USB.
4. Make only one planned group of changes at a time.

## 3. Update OpenCore as a coordinated set

When updating OpenCore:

1. Read the new release notes.
2. Read the matching configuration documentation.
3. Compare the new Sample.plist with the configuration in use.
4. Update the boot files, drivers, tools, and validator from the same package.
5. Review all Kext and ACPI dependencies.
6. Revalidate the configuration.
7. Test from a removable or otherwise recoverable boot path before replacing the only working EFI.

Do not overwrite config.plist with a new Sample.plist and assume that the machine-specific settings will be preserved.

## 4. Update macOS cautiously

Before a major or minor macOS update, check every relevant Kext, GPU path, storage device, wireless driver, SMBIOS choice, and OpenCore release. Wait until you understand known issues and have a tested recovery route.

## 5. Keep versions consistent

The EFI binaries, drivers, tools, Sample.plist, documentation, and ocvalidate should come from the same OpenCore release. Record component versions rather than relying on memory.

## 6. Remove obsolete files carefully

Do not remove an ACPI table, Kext, or driver only because its name looks unfamiliar. Identify its role and dependencies first, then test the change with a backup available.

## Next chapter

Use [14 — Troubleshooting](14-troubleshooting.md) when a change causes a failure.
