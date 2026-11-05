# 08 — Boot Recovery and install macOS

This chapter outlines the installation stages. Follow the current Dortania instructions for your exact platform and macOS release; do not treat this overview as a substitute for them.

## Before booting

- Verify the USB device and its contents.
- Keep a separate backup of personal files and the current EFI.
- Confirm that the EFI was built for the exact hardware.
- Confirm that the matching ocvalidate run has no unresolved errors.
- Record the original firmware settings.
- Confirm which physical disk is the intended installation target.

## 1. Review firmware settings

Firmware options differ by motherboard, CPU platform, and vendor. Use the matching Dortania platform guide to decide which options need to change.

Do not apply an Intel-specific list to AMD hardware or vice versa. Do not change settings you cannot identify. Record every change so that it can be reversed.

## 2. Boot the USB through UEFI

1. Insert the prepared USB.
2. Open the computer's one-time UEFI boot menu.
3. Select the USB's UEFI entry.
4. Confirm that the OpenCore picker appears.
5. Select the Recovery entry described by the official guide.

If OpenCore does not appear, stop and re-check the USB's EFI structure and firmware boot mode. Do not erase a disk to troubleshoot a missing boot entry.

## 3. Use Disk Utility carefully

The installation process may require formatting the intended target for the selected macOS version. Disk Utility can show multiple disks and volumes.

1. Confirm the target by physical drive model and capacity.
2. Use the current Apple and Dortania instructions for the target macOS release.
3. Do not erase a disk that contains Windows, personal files, or an existing data volume unless you have independently backed it up and intend to erase it.
4. If the target is unclear, cancel and identify the disks from a trusted environment first.

## 4. Complete the installer

Follow the prompts and allow the installer to restart as required. At each restart, select the correct OpenCore boot entry and then the installer or installed system entry described by the current guide.

Do not remove the USB until the installation and the first boot path are understood.

## 5. Verify the first boot

After setup, confirm that the system boots consistently. Check graphics acceleration, storage, network, audio, USB, sleep/wake, restart, and shutdown. A desktop appearing once is not a complete compatibility test.

## Next chapter

Use [09 — Post-installation](09-post-installation.md) to test the system before relying on it.
