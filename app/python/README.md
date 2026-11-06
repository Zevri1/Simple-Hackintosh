# Simple Hackintosh CLI

A standalone Python assistant for Windows Command Prompt (CMD) and PowerShell. It is a real local program, not a web page or browser wrapper.

**No third-party Python packages are required.** The program uses Python's standard library and Windows PowerShell/CIM for hardware inventory.

## Quick start

1. Download the repository ZIP and extract it.
2. Open the extracted folder, then open `app` and `python`.
3. Double-click `run.bat` or open CMD in this folder and run:

```cmd
run.bat
```

A beginner-friendly menu opens automatically. Choose a numbered task and follow the prompts.

To see all command-line options:

```cmd
run.bat --help
run.bat doctor
```

Requirements: Python 3.10 or newer. Windows 10/11 is required for automatic hardware inventory; EFI and plist checks also work on Linux/macOS.

## Beginner menu

| Menu item | What it does |
|---|---|
| Detect my hardware | Lists CPU, graphics, motherboard, BIOS, memory, disks, network/audio/storage devices, and available PCI IDs |
| Check an EFI folder or ZIP | Scans the folder/archive without extracting or modifying it |
| Check a config.plist | Checks plist syntax, required OpenCore sections, common entry fields, and duplicate entries |
| Run official ocvalidate | Runs the validator included with the user's matching OpenCore release |
| Compare two EFI folders/ZIPs | Compares file paths and SHA-256 content hashes to find additions, removals, and changed files |
| Back up an EFI folder | Creates a new ZIP backup without modifying or overwriting the source |
| Download macOS Recovery | Runs the bundled OpenCore macrecovery tool to download and verify Recovery files from Apple |\n| Create an EFI starter | Builds an EFI folder from a supplied OpenCore release ZIP and copies its Sample.plist as a starting point |
| Create a support report | Produces a shareable report without raw config.plist or SMBIOS identity values |

## CMD commands

### Runtime check

```cmd
run.bat doctor
```

### Hardware inventory

```cmd
run.bat hardware
run.bat hardware --output hardware-report.md
run.bat hardware --json --output hardware-report.json
```

Collects CPU model/core count, GPU names and driver versions, motherboard, BIOS, Windows version, memory, disks, and selected Plug and Play device classes. It extracts PCI Vendor/Device IDs where Windows reports them. It does not collect serial numbers, UUIDs, MAC addresses, or account identifiers.

### EFI scan

```cmd
run.bat efi-check "E:\EFI"
run.bat efi-check "C:\Users\YourName\Downloads\EFI.zip"
run.bat efi-check "E:\EFI" --json --output efi-findings.json
```

Checks the basic EFI layout, parses config.plist, and reviews enabled ACPI, Kext, UEFI driver, and tool references. It checks Kext bundle identifiers against their Info.plist when available. ZIP files are inspected in place and never extracted by this program.

### config.plist check

```cmd
run.bat config-check "E:\EFI\OC\config.plist"
run.bat config-check "E:\EFI\OC\config.plist" --json
```

Supports XML and binary property lists. Checks required top-level sections, common container types, entry fields, path values, and duplicate Kext identifiers. This built-in review is intentionally a preliminary lint, not a substitute for OpenCore's official validator.

### Official OpenCore validator

Get `ocvalidate` from the same OpenCore release as the EFI you are checking. Then run:

```cmd
run.bat ocvalidate "E:\EFI\OC\config.plist" --tool "C:\OpenCore\Utilities\ocvalidate\ocvalidate.exe" --expect-version 1.0.5
```

Replace the example version with the version you actually use. The program checks the validator's reported version before running when `--expect-version` is supplied. OpenCore's official documentation states that the validator must match the OpenCore release; a mismatched validator can produce misleading results.

### Create an EFI backup

```cmd
run.bat backup-efi "E:\EFI" --output "C:\Backups\EFI-before-changes.zip"
```

Creates a ZIP from an extracted EFI folder. The source is read-only, the destination must be outside that source folder, and an existing backup file is never overwritten. This backs up only the folder you select; it does not mount or locate hidden EFI system partitions.

### Create an EFI starter folder

Use an official OpenCore release ZIP downloaded from the OpenCore project. The tool builds a new EFI directory, copies the x64 boot files and OpenCore files, creates the usual ACPI/Drivers/Kexts/Tools folders, and places the release's `Docs/Sample.plist` at `EFI/OC/config.plist`.

```cmd
run.bat create-efi "C:\Users\YourName\Downloads\OpenCore-RELEASE.zip" --output "C:\Users\YourName\Desktop\EFI"
```

The interactive menu provides option **10**. The output folder must not already exist; existing files are never overwritten.

**Important:** this creates a starter folder, not a hardware-configured or ready-to-boot EFI. The sample config must be configured for the exact target hardware, with appropriate ACPI tables, drivers, and kexts, then checked with the matching `ocvalidate`. The program does not mount, format, or write to an EFI System Partition.

### Compare EFI backups

```cmd
run.bat compare-efi "E:\EFI" "F:\EFI-backup"
run.bat compare-efi "E:\EFI" "F:\EFI-backup" --json --output comparison.json
```

Compares file paths and SHA-256 hashes. It reports added, removed, and modified files without copying, replacing, deleting, or editing anything.

### Download Apple macOS Recovery

The repository includes the upstream **OpenCore macrecovery** utility in `macrecovery/`, with its original Python source, Windows launcher, board database, and BSD-3-Clause license notice.

From CMD:

```cmd
macrecovery\macrecovery.bat -h
run.bat macrecovery download --os-type latest --outdir com.apple.recovery.boot
```

Or run the bundled Windows launcher directly:

```cmd
macrecovery\macrecovery.bat download --os-type latest
```

The interactive menu also provides option **9**. The utility downloads the Recovery image and its chunklist from Apple's Recovery servers, then verifies the downloaded image against the chunklist. Its default output directory is `com.apple.recovery.boot`.

**Important:** this downloads Recovery files only. It does not create a bootable USB, partition or format a disk, install macOS, or configure a bootloader. The downloaded image is provided by Apple; it is not stored in this Git repository.

Upstream source: [acidanthera/OpenCorePkg — Utilities/macrecovery](https://github.com/acidanthera/OpenCorePkg/tree/master/Utilities/macrecovery). The copied source retains its original attribution and is accompanied by the upstream BSD-3-Clause license text in `macrecovery/LICENSE.txt`.

### Build a support report

```cmd
run.bat support-report "E:\EFI" --output hackintosh-support-report.md
run.bat support-report "E:\EFI" --json --output support-report.json
run.bat support-report "E:\EFI" --json
run.bat support-report "E:\EFI" --no-hardware
```

The report includes the EFI scan and, on Windows, a hardware inventory. Raw config.plist values and SMBIOS serial/UUID fields are not included. Review the generated report before sharing it; device names and file paths can still reveal personal information.

## Reading the results

- `[PASS]`: the specific check passed.
- `[WARN]`: review this item; it may be intentional or hardware/version-specific.
- `[ERROR]`: a required file, structure, or value is missing or invalid.
- Exit code `0`: no warnings/errors reported by that command.
- Exit code `1`: one or more warnings/errors were found.
- Exit code `2`: input or runtime error.
- Exit code `3`: the requested official validator version did not match.

A green result does **not** guarantee that macOS will boot. Compatibility and correct settings depend on the exact hardware and OpenCore release.

## Privacy and safety

The built-in operations are read-only. They do not format disks, modify partitions, assign drive letters, mount EFI system partitions, change boot entries, edit EFI files, flash firmware, or alter BIOS settings. The program does not upload data.

Never share an unredacted EFI or config.plist publicly. Always review generated reports before sharing. The support report omits sensitive SMBIOS identity fields, but still review the remaining hardware names and paths.

## Tests

From this directory:

```cmd
py -3 -m unittest discover -s tests -v
```

The automated tests use temporary sample files and do not inspect or change a real EFI partition.
