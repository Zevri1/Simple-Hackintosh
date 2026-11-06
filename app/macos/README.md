# Simple Hackintosh — macOS Desktop App

A native SwiftUI companion for the Simple Hackintosh project. This first desktop build focuses on full macOS installers, a verified OpenCore EFI starter, and local hardware reporting.

## Features in 0.1.0

- **Full macOS installer catalog:** reads the versions exposed by Apple's softwareupdate --list-full-installers command.
- **Download inside the app:** runs Apple's softwareupdate --fetch-full-installer --full-installer-version command. macOS downloads the installer app into /Applications; the app then copies that installer app to the folder or external drive selected by the user. This is a full installer workflow, not a Recovery image download.
- **Bootable USB creation:** invokes the selected installer app's createinstallmedia tool. macOS presents its administrator-authorization dialog. The selected external, ejectable volume is erased; the app requires a separate confirmation and refuses to erase a volume that contains the installer app itself.
- **OpenCore EFI starter:** downloads the latest stable OpenCore-*-RELEASE.zip from the official acidanthera/OpenCorePkg GitHub releases, verifies the downloaded bytes against the SHA-256 digest in GitHub release metadata, copies the x64 EFI folder, installs Docs/Sample.plist as EFI/OC/config.plist, and runs the matching ocvalidate executable when the release contains it.
- **Hardware Inspector:** uses macOS system_profiler and sw_vers locally. Serial-number and hardware-UUID lines are redacted in the displayed report.

## Important EFI limitation

The generated EFI is a **starter**, not a ready-to-boot configuration for a particular PC or Mac. It does not guess ACPI tables, Kexts, device properties, CPU/GPU quirks, SMBIOS identity, or USB mappings. Those must be selected and reviewed for the exact hardware using the official OpenCore documentation. The app does not overwrite an existing EFI or output folder.

## Requirements

- macOS 13 or newer to run the GUI.
- macOS Catalina 10.15 or newer for Apple's full-installer download workflow.
- Internet access for the Apple software catalog and official OpenCore release download.
- An external, ejectable volume for bootable USB creation. The volume is erased by Apple's createinstallmedia tool. Keep the downloaded installer app on another drive.

## Build and test

Requires Xcode Command Line Tools and Swift 5.10 or newer:

    swift test --package-path app/macos
    swift build -c release --package-path app/macos
    bash app/macos/scripts/package-app.sh

The package script creates app/macos/.build/packaged/Simple Hackintosh.app and an architecture-specific ZIP. The GitHub Actions workflow builds separate native downloads for Intel x86_64 and Apple Silicon arm64. The app is ad-hoc signed for local testing, not Developer ID signed or notarized. Gatekeeper may require Control-click then Open on first launch.

## Windows counterpart

The repository's Windows desktop GUI is in app/windows and is built with .NET 10 / Windows Forms. It can share EFI inspection, configuration checks, hardware inventory, and OpenCore release downloads. Windows can also download Apple's macOS packages via a catalog-based downloader, but current macOS full-installer packages are not converted into the same official bootable USB workflow by Apple's createinstallmedia tool on Windows. For a full offline installer USB for modern macOS, complete the media-creation step on a Mac; keep the Windows Recovery-media path as an optional fallback, not the primary workflow.

## Official sources

- Apple: https://support.apple.com/en-euro/102662
- Apple: https://support.apple.com/en-ie/101578
- OpenCorePkg releases: https://github.com/acidanthera/OpenCorePkg/releases
- OpenCore documentation: https://github.com/acidanthera/OpenCorePkg/tree/master/Docs
