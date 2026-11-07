# Simple Hackintosh — Desktop Preview

Cross-platform Qt desktop tool for **Windows and macOS**. Preview release **0.2.0**.

## Implemented in this preview

### macOS Downloader
- Downloads the upstream [gibMacOS](https://github.com/corpnewt/gibMacOS) helper on first launch.
- Opens its native interface to browse Apple's software catalog and download installer packages.
- The app does not mirror Apple installers or promise every macOS version is available.

### OpenCore EFI Builder
- Fetches the latest official OpenCorePkg ZIP from Acidanthera GitHub Releases.
- Extracts the x64 EFI boot files and OpenCore directory into a chosen output folder.
- Creates a starter `config.plist`, lets you choose a SMBIOS model, and imports user-selected ACPI AML files and kext ZIP archives.
- Includes a basic EFI folder / plist validation screen.

## Important: generated EFI is a STARTER, not boot-ready
This release does **not** automatically identify all PCI devices, generate correct ACPI tables, select/configure every kext, build DeviceProperties, or validate the config against the exact OpenCore schema. You must tailor and validate the EFI for your exact computer. Never boot an unreviewed generated EFI or overwrite your working EFI. Keep a backup and test from a separate USB.

## Build locally

Python 3.11+:

```bash
python -m venv .venv
# Windows: .venv\\Scripts\\activate
# macOS: source .venv/bin/activate
python -m pip install -r requirements.txt
python app/main.py
```

## Build standalone executables

GitHub Actions → **Desktop Preview Release** → Run workflow.
- Version: `0.2.0`
- Publish release: checked (creates a prerelease)
- After both platform builds pass, the release includes Windows and macOS assets.

## Safety
- No disk formatting, partitioning, or USB writing is implemented in this preview.
- EFI output is written only to the chosen folder after confirmation.
- macOS licensing and hardware compatibility must be respected.
- This is not OpenCore Legacy Patcher and does not patch Apple legacy Macs.

## License
See the repository root LICENSE.
