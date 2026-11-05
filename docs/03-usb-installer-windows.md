# 03 — Prepare a macOS Recovery USB in Windows

This chapter describes the Recovery-media workflow. It does not provide or redistribute a macOS installer image.

## What you need

- A USB drive whose contents can be erased.
- A Windows account with permission to run the required tools.
- The official OpenCorePkg release that you plan to use.
- The current Dortania Windows installer instructions.
- A separate backup of all important data on the USB drive.

## 1. Download the tools from official sources

1. Open the [OpenCorePkg releases page](https://github.com/acidanthera/OpenCorePkg/releases).
2. Select one release and download its official package.
3. Read the matching documentation. Keep this release fixed while preparing the EFI.
4. Follow Dortania's current [Windows installer guide](https://dortania.github.io/OpenCore-Install-Guide/installer-guide/windows-install.html) and [Recovery guide](https://dortania.github.io/OpenCore-Install-Guide/installer-guide/mac-install-recovery.html).
5. Use the macrecovery utility included with the package and the exact command documented for the target macOS release. Check the utility's help output if the syntax has changed.

Do not download Recovery images from random file-sharing sites.

## 2. Identify the USB drive before formatting

1. Plug in the USB drive.
2. Open File Explorer and note its label and capacity.
3. Cross-check the physical drive in Disk Management.
4. Disconnect other removable drives if that helps prevent selecting the wrong one.
5. Confirm that all files on the selected USB have been backed up.

**Warning:** formatting the wrong disk can destroy data. Do not continue if the device identity or capacity is unclear.

## 3. Format the USB

Use the current Dortania instructions and a trusted formatting utility such as Rufus. The required partition scheme and file-system settings can depend on the current workflow and firmware requirements. Do not blindly reuse settings from an old screenshot.

After formatting, confirm that Windows shows the expected USB volume.

## 4. Download Recovery

Use the macrecovery utility from the selected OpenCore package, following Dortania's current instructions for the exact macOS version. The Recovery download is version-specific. Do not substitute files from a different release or an unrelated download.

If the download fails, verify the command, network connection, free space, and official instructions before retrying.

## 5. Place the Recovery files

Follow the official guide for the expected Recovery directory and filenames. The image and its matching chunklist must remain together when the method requires both. Do not rename files unless the current guide says to.

The Recovery files and the OpenCore EFI serve different purposes. A USB can contain Recovery media without yet containing a working EFI.

## 6. Verify the result

- Confirm that the expected Recovery files are present.
- Confirm that the EFI folder is at the location required by Dortania.
- Confirm that the USB is readable in Windows.
- Keep a note of which OpenCore release was used.

## Next chapter

Build the EFI structure in [04 — EFI structure](04-efi-structure.md), then follow [05 — ACPI, Kexts, and drivers](05-acpi-kexts-drivers.md).
