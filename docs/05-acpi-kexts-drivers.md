# 05 — ACPI, Kexts, and UEFI drivers

This chapter explains the role of each file group. It is not a universal shopping list: the correct files depend on the hardware and the macOS release.

## 1. ACPI tables

ACPI tables describe devices, power states, and firmware interfaces. Some platforms need custom SSDTs or patches to correct a specific firmware implementation.

A disciplined workflow is:

1. Inventory the hardware and firmware.
2. Read the relevant [Getting Started with ACPI](https://dortania.github.io/Getting-Started-With-ACPI/) material.
3. Identify the exact problem a table is intended to solve.
4. Generate or select the table using the appropriate platform-specific guide.
5. Review its source and device scope.
6. Add it to EFI/OC/ACPI and configure it in config.plist.
7. Test one logical change at a time.

Do not use a table simply because another system has a similarly named CPU. Avoid duplicate or conflicting patches.

## 2. Kexts

Kexts provide drivers or system extensions. Common project names include Lilu, VirtualSMC, WhateverGreen, AppleALC, and chipset-specific network or input drivers. Their relevance and support depend on the target macOS release and hardware.

For every Kext:

1. Download from the official project or its official release page.
2. Read the project's current compatibility and installation notes.
3. Check dependencies and required load order.
4. Keep the Kext version and its dependencies recorded.
5. Place only the required bundle in EFI/OC/Kexts.
6. Add its entry using the correct configuration tool and validate the result.

Do not download repackaged binaries from unknown mirrors. Do not assume that a Kext's existence proves that it supports a particular device.

## 3. UEFI drivers

UEFI drivers are loaded before the macOS kernel. Their jobs differ from Kexts. Select only the drivers required by the current OpenCore guide and chosen boot configuration.

Examples of driver roles include filesystem access, runtime services, and optional picker support. Do not enable every driver from the sample package.

## 4. Keep a component ledger

For each ACPI file, Kext, and driver, write down:

- exact filename and version;
- official project source;
- hardware or issue it addresses;
- dependency or load-order requirement;
- whether it is enabled in config.plist;
- date last tested.

This makes future troubleshooting and upgrades much easier.

## 5. Check upstream release notes

Before a macOS update, check whether each component is maintained for that release. Remove obsolete or conflicting components only after reading the relevant project documentation and backing up the EFI.

## Next chapter

Add the selected files to config.plist in [06 — Configuration](06-config-plist.md).
