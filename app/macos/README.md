# Simple Hackintosh Studio — macOS app

A native SwiftUI desktop front-end for Simple Hackintosh. The first macOS GUI milestone focuses on three practical workflows: full macOS installer downloads, OpenCore EFI starter creation, and local hardware/EFI inspection.

## Requirements

- macOS 13 Ventura or later.
- Internet access for Apple installer catalogs and official OpenCore release downloads.
- An administrator account for the installer download and bootable-USB steps. macOS displays its own authorization prompt; the app does not collect or store the password.
- Universal app target: Intel x86_64 and Apple Silicon arm64.

## Features

### Full macOS installer — not Recovery

The app bundles the upstream mist-cli helper (v2.3, pinned to commit d67e9172aa21c54420568fed60306955a56aa416). It lists full installer products from Apple's standard software update catalog and creates the selected installer application in the folder the user chooses. The temporary download workspace is also placed on that selected volume, so the large payload does not need to be stored in the system Downloads folder.

This is a full installer workflow, not the OpenCore Recovery downloader. The app asks macOS for administrator authorization through a native system prompt before running the installer helper.

### Create a bootable USB

After downloading the installer, connect a removable USB drive and refresh the volume list. The app offers Apple's createinstallmedia tool from inside the downloaded installer. It shows a destructive confirmation dialog with the volume name and mount path before starting. The selected volume is erased by Apple's tool.

### OpenCore EFI starter

The app queries the official OpenCorePkg latest-release metadata, downloads the RELEASE ZIP, verifies its size and SHA-256 digest against GitHub's release metadata, then extracts the x64 EFI and upstream Sample.plist into a new EFI folder. It refuses to overwrite an existing EFI folder.

**Important:** this output is a starter, not a hardware-configured or boot-ready EFI. The sample config must be adapted for the target PC, and validated using ocvalidate from the same OpenCore release.

### Hardware and EFI inspection

- Local hardware summary via system_profiler, sw_vers, and uname.
- EFI layout checks for BOOTx64.efi, OpenCore.efi, and config.plist.
- XML/binary plist parsing and checks for enabled ACPI, Kext, and UEFI-driver path references.
- The inspection does not upload the EFI or hardware report.

## Build locally

Requirements: Xcode Command Line Tools / Swift 6.3.1 and the pinned mist-cli source.

    git clone https://github.com/Zevri1/Simple-Hackintosh.git
    git clone https://github.com/ninxsoft/mist-cli.git
    cd mist-cli
    git checkout d67e9172aa21c54420568fed60306955a56aa416
    swift build --configuration release --arch arm64 --arch x86_64
    cd ../Simple-Hackintosh
    chmod +x app/macos/build-app.sh
    app/macos/build-app.sh ./dist/macos ../mist-cli/.build/apple/Products/release/mist

The GitHub Actions workflow builds the universal .app ZIP and publishes it as a workflow artifact. The build is ad-hoc signed for bundle integrity but is not Developer ID signed or notarized; macOS may display its normal first-open security warning.

## Scope and limitations

This is the first native GUI milestone, not feature parity with OpenCore Legacy Patcher. It does not automatically select every ACPI table, Kext, firmware quirk, device property, or SMBIOS value for an arbitrary PC. Those decisions need the exact target hardware and current OpenCore documentation. The app also does not install macOS automatically or modify firmware settings.
