# 00 — Read this first

This project explains a complicated boot process. It is not a one-click installation recipe, and it cannot guarantee that a given computer will run macOS.

## Important risks

- **Data loss:** formatting a USB, erasing a disk, or changing partitions can destroy files. Keep a separate backup and verify the target by model and capacity.
- **Boot problems:** an incorrect EFI or firmware setting can stop the operating system from starting. Keep a recovery path and record original firmware settings.
- **Compatibility:** a CPU being recognized does not mean the GPU, Wi-Fi, Bluetooth, audio, sleep, or power management will work.
- **Updates:** macOS and OpenCore updates can change driver and Kext behavior. Do not update a working setup without a recovery plan.
- **Privacy:** EFI folders and diagnostic reports can contain unique identifiers. Redact them before sharing.

## Legal and licensing note

Apple's macOS software license and local law may restrict installation or use on non-Apple hardware. Read the applicable terms before proceeding. This repository is educational material, not legal advice. Do not redistribute macOS installers, Apple firmware, or third-party binaries unless their licenses explicitly permit it. The MIT license for this repository applies only to its original documentation.

## Prerequisites

1. A computer whose exact hardware can be identified.
2. A separate USB drive whose contents can be erased.
3. A verified backup of personal files and existing boot files.
4. Access to current official OpenCore and Dortania documentation.
5. Enough time to troubleshoot; do not begin disk migration when you urgently need the computer.

## A safe order of work

1. Inventory the hardware.
2. Check each component for the target macOS release.
3. Prepare a Recovery USB.
4. Assemble an EFI from the correct OpenCore release.
5. Validate the configuration before booting it.
6. Test the USB boot path before changing an installed operating system.
7. Install only after identifying the target disk carefully.
8. Test all devices before considering the setup complete.

## Stop conditions

Stop and re-check the official guide if the exact GPU is unsupported, a required driver has no maintained support, the EFI contains files you cannot identify, the validator reports unresolved errors, or the installation asks you to erase a disk you have not positively identified.

## Next chapter

Continue with [01 — Hardware inventory](01-hardware-inventory.md).
