# 16 — Visual walkthrough

These screenshots help identify common screens and folder layouts. They are hosted by the Dortania OpenCore Install Guide and are linked here rather than republished. Some examples may reflect older versions of the tools. Treat them as visual references only; follow the current instructions for your target macOS and OpenCore release.

## 1. Format the USB

![Rufus USB formatting example](https://dortania.github.io/OpenCore-Install-Guide/assets/img/format-usb-rufus.43feba9e.png)

Source: [Dortania — Making the installer in Windows](https://dortania.github.io/OpenCore-Install-Guide/installer-guide/windows-install.html).

Formatting erases the selected device. Verify its identity and capacity before starting.

## 2. Download Recovery

![macrecovery download example](https://dortania.github.io/OpenCore-Install-Guide/assets/img/macrecovery-done.1b0960bc.png)

Source: [Dortania — Recovery download](https://dortania.github.io/OpenCore-Install-Guide/installer-guide/mac-install-recovery.html).

Use the command documented for the target macOS release. The screenshot is not a substitute for current command syntax.

## 3. Check the Recovery files

![Recovery image and chunklist example](https://dortania.github.io/OpenCore-Install-Guide/assets/img/basesystem-example.93778929.png)

Source: [Dortania — Windows installer](https://dortania.github.io/OpenCore-Install-Guide/installer-guide/windows-install.html).

Keep matching Recovery files together when required by the selected method.

## 4. Check the USB root

![USB root folder example](https://dortania.github.io/OpenCore-Install-Guide/assets/img/com-efi-done.a6fb730e.png)

Source: [Dortania — Windows installer](https://dortania.github.io/OpenCore-Install-Guide/installer-guide/windows-install.html).

The Recovery directory and EFI folder have separate roles and must be placed as directed by the guide.

## 5. Understand the EFI folder

![Example populated EFI folder](https://dortania.github.io/OpenCore-Install-Guide/assets/img/populated-efi.8d46cc52.png)

Source: [Dortania — config.plist setup](https://dortania.github.io/OpenCore-Install-Guide/config.plist/).

This is a layout example, not a ready-to-use EFI. Hardware-specific files must be researched separately.

## 6. Locate Sample.plist

![Sample.plist location example](https://dortania.github.io/OpenCore-Install-Guide/assets/img/sample-location.9e091fb2.png)

Source: [Dortania — config.plist setup](https://dortania.github.io/OpenCore-Install-Guide/config.plist/).

Use the Sample.plist from the same OpenCorePkg release as the EFI binaries and validator.

## 7. Place config.plist

![config.plist location example](https://dortania.github.io/OpenCore-Install-Guide/assets/img/renamed.9b06868d.png)

Source: [Dortania — config.plist setup](https://dortania.github.io/OpenCore-Install-Guide/config.plist/).

The configuration file belongs in the OpenCore directory at the path described by the current guide.

## 8. ProperTree Clean Snapshot

Before:

![ProperTree before Clean Snapshot](https://dortania.github.io/OpenCore-Install-Guide/assets/img/before-snapshot.f2dccade.png)

After:

![ProperTree after Clean Snapshot](https://dortania.github.io/OpenCore-Install-Guide/assets/img/after-snapshot.0dfaaf2b.png)

Source: [Dortania — config.plist setup](https://dortania.github.io/OpenCore-Install-Guide/config.plist/).

Clean Snapshot updates file entries. It does not select compatible Kexts, write correct quirks, or validate a hardware configuration.

## Attribution and licensing

The images above are hosted by Dortania and remain subject to the terms applicable to that project and its documentation. They are not claimed as original Simple Hackintosh artwork. Consult the linked source pages for current instructions and attribution details.
