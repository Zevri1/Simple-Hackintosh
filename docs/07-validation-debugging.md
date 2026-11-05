# 07 — Validation and debugging

Validation catches configuration problems before the first boot. It does not prove that hardware is supported or that the computer will start successfully.

## 1. Use the matching validator

1. Open the Utilities folder from the same OpenCorePkg release used to build the EFI.
2. Locate ocvalidate for the host operating system.
3. Run it against the config.plist you intend to boot.
4. Read the full output.
5. Correct errors, then run it again.

Do not use an ocvalidate binary from a different OpenCore release.

## 2. Review the EFI manually

Check that:

- BOOT and OpenCore files come from the same release;
- config.plist is based on the matching Sample.plist;
- every enabled ACPI file, driver, and Kext exists;
- Kext dependencies and ordering match official guidance;
- SMBIOS identifiers are generated for this setup and kept private;
- no unrelated or duplicate configuration entries remain.

## 3. Enable diagnostic output carefully

For troubleshooting, consult the current OpenCore debugging guide and the documentation for your release. A debug build can generate useful logs, but the exact debug drivers, flags, and configuration vary by release.

Keep a copy of the last known-good EFI before changing debugging settings.

## 4. Change one category at a time

If the system stops booting after a change:

1. Identify the last change.
2. Restore the previous known-good EFI.
3. Confirm the computer starts again.
4. Reapply only the change being investigated.
5. Record the result.

Changing many unrelated settings at once makes the cause harder to identify.

## 5. Prepare a safe report

Use [templates/problem-report.md](../templates/problem-report.md). Include the macOS version, OpenCore release, exact hardware model, validator output, and the stage at which the failure occurs.

Redact usernames, serial numbers, MLB, SystemUUID, ROM, MAC addresses, account details, and personal paths before posting logs.

## Next chapter

When validation is clean and the hardware path is understood, continue to [08 — Installation](08-installation.md).
