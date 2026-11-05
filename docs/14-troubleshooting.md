# 14 — Troubleshooting

Troubleshooting works best when you identify the exact stage of failure and change one thing at a time.

## Identify the stage

First decide which stage fails:

1. Firmware does not show the USB.
2. OpenCore does not appear.
3. OpenCore appears but Recovery is missing.
4. macOS starts loading and then stops.
5. The installer runs but does not complete.
6. The system installs but the first internal boot fails.
7. macOS starts, but one hardware feature does not work.

Each stage points to a different set of files and settings.

## Common symptoms

| Symptom | First checks |
|---|---|
| USB not listed in UEFI menu | USB format, port, firmware boot mode, and EFI fallback file |
| OpenCore picker does not appear | EFI directory layout, BOOT file, firmware boot entry, and USB readability |
| Recovery entry is missing | Recovery files, directory, matching image/chunklist, and current guide |
| Validator reports an error | OpenCore/config version match, missing paths, plist structure, and unsupported keys |
| Boot stops early | Exact last log line, ACPI scope, driver/Kext version, and platform-specific quirks |
| Graphics work only in basic mode | Exact GPU support, acceleration path, and device properties |
| Wi-Fi or Bluetooth missing | Exact chipset and matching current driver support |
| Audio output missing | Codec, layout support, loaded dependencies, and macOS version |
| USB ports behave inconsistently | Controller layout and USB map |
| Update breaks boot | Restore known-good EFI and compare changed components |

These are starting points, not guaranteed fixes.

## A disciplined recovery sequence

1. Stop repeated boot attempts if the failure began after a known change.
2. Restore the last known-good EFI.
3. Confirm the previous setup works again.
4. Compare the changed files and settings.
5. Check current official project notes for the exact error.
6. Apply one change and test again.

## What to include in a support report

Use [templates/problem-report.md](../templates/problem-report.md). Include the exact hardware model, macOS build, OpenCore release, stage of failure, validator output, and relevant redacted logs.

Never post serial numbers, MLB, SystemUUID, ROM, MAC addresses, passwords, Apple account details, or a complete unredacted EFI.

## When to stop

If you cannot identify the target disk, a configuration file's origin, or a firmware option's purpose, do not guess. Restore the known-good state and research the exact hardware and release first.
