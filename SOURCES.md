# Primary technical sources

This guide prioritizes primary project documentation. Version-sensitive instructions must be checked again before every installation or update.

## OpenCore and installation

- OpenCorePkg releases: https://github.com/acidanthera/OpenCorePkg/releases
- OpenCorePkg documentation: https://github.com/acidanthera/OpenCorePkg/tree/master/Docs
- Dortania OpenCore Install Guide: https://dortania.github.io/OpenCore-Install-Guide/
- Windows installer instructions: https://dortania.github.io/OpenCore-Install-Guide/installer-guide/windows-install.html
- macOS Recovery instructions: https://dortania.github.io/OpenCore-Install-Guide/installer-guide/mac-install-recovery.html
- config.plist guide: https://dortania.github.io/OpenCore-Install-Guide/config.plist/
- Dortania OpenCore Post-Install Guide: https://dortania.github.io/OpenCore-Post-Install/

## Hardware and post-installation

- ACPI guide: https://dortania.github.io/Getting-Started-With-ACPI/
- GPU Buyers Guide: https://dortania.github.io/GPU-Buyers-Guide/
- Wireless Buyers Guide: https://dortania.github.io/Wireless-Buyers-Guide/
- USB mapping guide: https://dortania.github.io/OpenCore-Post-Install/usb/
- Audio guide: https://dortania.github.io/OpenCore-Post-Install/universal/audio.html
- macOS 26 / Tahoe notes: https://dortania.github.io/OpenCore-Install-Guide/extras/tahoe.html

## Official project tools

- ProperTree: https://github.com/corpnewt/ProperTree
- GenSMBIOS: https://github.com/corpnewt/GenSMBIOS
- SSDTTime: https://github.com/corpnewt/SSDTTime
- OpenCorePkg: https://github.com/acidanthera/OpenCorePkg

## Version note

Checked on 2026-11-05. The OpenCorePkg GitHub releases page identified 1.0.8 as the latest release when checked. The Dortania install-guide banner may show an earlier supported version. This is not a reason to combine files: use the package's own Sample.plist, binaries, tools, documentation, and ocvalidate together, and check the current guide before proceeding.

External sites can change. If a page's instructions conflict with the documentation included with the exact OpenCore release, stop and investigate the difference before applying a setting.
