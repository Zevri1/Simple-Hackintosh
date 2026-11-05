# 12 — Dual boot and protecting existing systems

Dual boot introduces additional failure points because multiple operating systems may share disks, EFI System Partitions, and firmware boot entries.

## Before making changes

1. Make a verified backup of personal files.
2. Create and test a recovery path for the existing operating system.
3. Record the existing EFI partition and boot configuration.
4. Identify each physical disk by model and capacity.
5. Understand which disk and partition each installer will modify.

If a disk's identity is uncertain, stop. Do not use capacity alone when multiple disks have similar sizes.

## Separate disks where practical

Keeping macOS and an existing operating system on separate physical disks can make recovery and troubleshooting easier. It does not eliminate all risks: firmware boot entries and shared boot menus can still be affected.

## Protect the existing EFI

Do not overwrite, rename, or delete existing Microsoft boot files. Keep a verified copy of the original EFI System Partition before changing boot files. Do not format an existing system partition just because an installer offers it.

## Choose the boot method deliberately

OpenCore can be selected through the firmware's one-time boot menu. A permanent default boot order is optional and should be changed only after both systems are independently recoverable.

## After setup

- Test each operating system from the firmware boot menu.
- Confirm that updates to one system do not remove the other system's boot path.
- Keep separate backups of both boot configurations.
- Re-check the boot order after firmware updates.

## Recovery

If one system disappears from the boot menu, do not immediately reinstall or erase anything. Identify which physical disk and EFI partition are still present, then follow the operating system vendor's official recovery procedure.

## Next chapter

See [13 — Maintenance](13-maintenance.md) for safe update planning.
