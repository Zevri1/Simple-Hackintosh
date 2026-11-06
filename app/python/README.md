# Simple Hackintosh CLI

A cross-platform Python assistant with a native PowerShell front-end for Windows and a double-clickable Terminal launcher on macOS. Version 2.4.0 adds a verified OpCore-Simplify Hardware Sniffer export on Windows, a guided launch flow, and clearer Windows/macOS run instructions.

**No third-party Python packages are required.** The program uses Python's standard library. Windows hardware inventory uses PowerShell/CIM; macOS inventory uses the built-in system_profiler and sw_vers commands.

## How to run

### Option A — Download the ready-to-run release

1. Download the [latest CLI ZIP](https://github.com/kiwewe09-aa/Simple-Hackintosh/releases/latest).
2. Right-click the ZIP and choose **Extract All**.
3. Open the extracted `Simple-Hackintosh-CLI-v2.4.0` folder. In the release ZIP, the launchers are directly beside `simple_hackintosh.py`.
4. Start the matching launcher below.

| System | Exact way to start |
|---|---|
| Windows CMD | Double-click `run.bat` |
| Windows PowerShell | Open Terminal in this folder and run `./run.ps1` |
| macOS Finder | Control-click `run.command`, choose **Open**, then confirm |
| macOS Terminal | Open Terminal in this folder and run `chmod +x ./run.command && ./run.command` |
| macOS with PowerShell 7 | Run `pwsh ./run.ps1` |

**Windows PowerShell execution-policy message:** in that same PowerShell window, run `Set-ExecutionPolicy -Scope Process Bypass`, then run `./run.ps1` again. This changes the policy for that PowerShell process only.

### Option B — Run from a Git checkout

Open a terminal at the repository root, then enter the source folder:

```powershell
cd app/python
./run.ps1
```

On macOS:

```zsh
cd app/python
chmod +x ./run.command
./run.command
```

Requirements: Python 3.10 or newer. No third-party Python packages are required. Windows PowerShell 5.1 or PowerShell 7 can launch the Windows front-end; PowerShell 7 can also run on macOS. The macOS `run.command` launcher uses zsh and does not require PowerShell.
## Beginner menu

| Menu item | What it does |
|---|---|
| Export hardware report | On Windows, launches the official Hardware Sniffer and saves its OpCore-Simplify-style `Report.json`; on macOS, uses the native `system_profiler` inventory and compatibility summary |
| Check an EFI folder or ZIP | Scans the folder/archive without extracting or modifying it |
| Check a config.plist | Checks plist syntax, required OpenCore sections, common entry fields, and duplicate entries |
| Run official ocvalidate | Runs the validator included with the user's matching OpenCore release |
| Compare two EFI folders/ZIPs | Compares file paths and SHA-256 content hashes to find additions, removals, and changed files |
| Back up an EFI folder | Creates a new ZIP backup without modifying or overwriting the source |
| Download macOS Recovery | Runs the bundled OpenCore macrecovery tool to download and verify Recovery files from Apple |
| Create an EFI starter | Builds an EFI folder from a supplied OpenCore release ZIP and copies its Sample.plist as a starting point |
| Create a support report | Produces a shareable report without raw config.plist or SMBIOS identity values |



### OpCore-Simplify hardware report (Windows)

The interactive menu's **01 Hardware report** option launches the official [Hardware Sniffer](https://github.com/lzhoang2801/Hardware-Sniffer) used by [OpCore-Simplify](https://github.com/lzhoang2801/OpCore-Simplify). Simple Hackintosh downloads the official `Hardware-Sniffer.exe` release on demand, checks the SHA-256 digest published by GitHub, and requires a valid Windows Authenticode signature before launch. The executable is cached under your Windows local application data folder; it is not bundled in this repository or the CLI ZIP.

When its menu appears:
1. Press `H` to export the hardware report.
2. Press `Enter` to dismiss the save confirmation and return to the Hardware Sniffer menu.
3. Press `Q` to quit.
4. The report is saved to `HardwareReport/SysReport/Report.json` by default. Change the folder when prompted in the Simple Hackintosh menu.

You can launch this export directly from PowerShell with:

```powershell
./run.ps1 hardware-sniffer --output-dir HardwareReport
```

Do **not** choose `A` unless you intentionally want to dump ACPI tables. The report export does not require an ACPI dump. This upstream Hardware Sniffer is Windows-only; on macOS, Simple Hackintosh continues to use the built-in `system_profiler` hardware inventory instead. Review the generated report before sharing it.

## PowerShell and macOS launchers

### PowerShell

~~~powershell
./run.ps1
./run.ps1 doctor
./run.ps1 hardware --json
~~~

run.ps1 draws a PowerShell-native menu and forwards the selected operation to the shared Python engine. It works with Windows PowerShell 5.1 and PowerShell 7. PowerShell 7 can also run on macOS when installed.

### macOS Terminal

Double-click run.command in Finder, or use Terminal:

~~~zsh
./run.command
./run.command doctor
./run.command hardware --json
~~~

The launcher looks for Python 3.10 or newer and does not install packages or request administrator privileges. To save a macOS hardware report instead of only displaying it, run `./run.command hardware --output mac-hardware-report.md`; use `--json --output mac-hardware-report.json` for JSON. The macOS report uses `system_profiler -json -detailLevel mini` and `sw_vers`. Apple documents System Information and its hardware overview.


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

On Windows, the report collects CPU, GPU, motherboard, BIOS, memory, disks and Plug and Play device IDs through PowerShell/CIM. On macOS, it uses the built-in system_profiler JSON interface to collect the Mac model, chip/CPU, graphics, selected PCI devices, storage, memory and running macOS version. It deliberately does not retain serial numbers or hardware UUIDs.

For Windows-hosted PC hardware, the report includes a **macOS compatibility estimate**. It recognises selected Intel graphics families, provides a conservative version ceiling and references the guides used. Tahoe 26 is labelled experimental where appropriate. Unknown GPUs are reported as unknown rather than assigned a guessed version.

For a Mac host, the report confirms the macOS version currently running. It does not claim to know the maximum macOS version supported by that Mac model. Neither report is a boot test or an official Apple compatibility verdict.

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
