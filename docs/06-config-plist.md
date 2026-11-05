# 06 — Configure config.plist

config.plist tells OpenCore which ACPI files, drivers, Kexts, and boot settings to use. It is hardware-specific and release-specific.

## 1. Use the matching Sample.plist

1. Open the OpenCorePkg archive you selected.
2. Locate the Sample.plist in the package's Docs area.
3. Make a copy and name it config.plist.
4. Keep the original sample untouched for reference.
5. Read the matching OpenCore Configuration.pdf or current documentation.

Never start from an unrelated computer's config.plist.

## 2. Use a plist editor

ProperTree is a commonly used plist editor. Download it only from its [official repository](https://github.com/corpnewt/ProperTree). Open your config.plist and use its Clean Snapshot function after your ACPI, Drivers, and Kexts folders are populated.

Clean Snapshot synchronizes file entries; it does **not** decide which files are correct, choose the proper quirks, or make unsupported hardware work.

## 3. Configure by section

The exact values must come from the current guide for the platform.

- ACPI: enable only the intended tables and patches.
- Booter: use quirks appropriate to the firmware and CPU platform.
- DeviceProperties: add only documented device properties for the exact hardware.
- Kernel: configure Kexts, dependencies, patches, and kernel quirks.
- Misc: configure the boot picker, security settings, and debugging behavior.
- NVRAM: use only documented variables and boot arguments.
- PlatformInfo: set a supported SMBIOS and generated identifiers.
- UEFI: configure the selected drivers and firmware behavior.

Do not paste a complete set of settings from a different hardware generation.

## 4. Treat SMBIOS values as private

PlatformInfo contains identifiers that should be unique to the installation. Generate them using a trusted tool and the current guide. Never copy another person's serial number, MLB, ROM, or SystemUUID.

Do not publish these values in screenshots, issue reports, or a public EFI.

## 5. Check every configured path

For each enabled ACPI item, driver, and Kext, verify that the file exists and the path and enabled state match the actual folder. Check dependencies and order against the official project documentation.

## 6. Validate before booting

Run the ocvalidate tool shipped with the same OpenCore release. Resolve every error. Understand warnings before proceeding; do not hide or ignore an issue merely to make the report appear clean.

## Next chapter

Follow [07 — Validation and debugging](07-validation-debugging.md).
