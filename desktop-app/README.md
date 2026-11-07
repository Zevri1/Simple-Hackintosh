# Simple Hackintosh — Desktop App

Cross-platform desktop application foundation for **Windows and macOS**.

## Current status: UI preview (0.1.0)

This release is a graphical scaffold—not an installer yet. It includes navigation for macOS downloads, EFI generation, USB creation, installation, validation, and logs. System detection currently reports basic OS/architecture details.

## Run from source

Python 3.10+:

```bash
python -m venv .venv
# Windows: .venv\\Scripts\\activate
# macOS: source .venv/bin/activate
python -m pip install -r requirements.txt
python app/main.py
```

## Planned implementation

1. Windows/macOS hardware inventory (PCI IDs, CPU, firmware, ACPI).
2. Apple Software Update catalog + verified macOS installer acquisition.
3. OpenCore EFI generation with hardware-specific ACPI/kext/driver selection.
4. EFI validation (plist, OpenCore schema, required files).
5. USB creation and installation flows with explicit disk identity and destructive-action confirmation.
6. Packaging for Windows (.exe) and macOS (.app).

## Safety & compatibility

- Never format, partition, or write an EFI without explicit user confirmation.
- Always show the exact target disk/device and a backup step.
- OpenCore configuration is hardware-specific; a universal EFI is unsafe.
- macOS licensing and hardware compatibility must be respected. This tool does not bypass Apple's licensing or security protections.
- OCLP targets supported legacy Apple Macs; this project is a separate PC Hackintosh workflow, not a replacement/fork of OCLP.

## License

See the repository's root LICENSE.
