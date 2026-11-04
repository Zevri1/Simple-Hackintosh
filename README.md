# Simple-Hackintosh
A simple, step-by-step guide to installing macOS on non-Apple hardware using OpenCore.
# 🍎 Simple Hackintosh

### The Complete Step-by-Step Guide to Understanding, Preparing, Installing, and Configuring macOS with OpenCore

Welcome to **Simple Hackintosh**!

This repository is an educational guide that explains the process of preparing and configuring OpenCore and installing macOS on compatible non-Apple hardware.

This guide starts with identifying computer hardware and preparing an installation USB. It then explains how to organize the EFI folder, configure OpenCore, install macOS, configure hardware, and troubleshoot common problems.

The objective is not simply to get macOS installed. The objective is to understand how the entire system works.

---

# ⚠️ Important Information Before Starting

Please read this section before following the guide.

- Hackintosh configurations are hardware-specific.
- An EFI folder made for one computer should not automatically be used on another.
- Different macOS versions may require different configurations.
- Some hardware components may have limited functionality or no support.
- A BIOS setting that works on one computer may cause problems on another.
- Installing an operating system can cause data loss if the wrong disk is selected.
- Always maintain a backup of important data and a working recovery method.

**Apple's macOS Tahoe 26 software license agreement restricts installation and use on non-Apple-branded computers. Read the applicable license terms before proceeding.** This project is an independent educational resource and is not affiliated with Apple.

[Read Apple's macOS License Agreement](https://www.apple.com/legal/sla/)

---

# 📑 Table of Contents

1. [Understanding Hackintosh](#1-understanding-hackintosh)
2. [Understanding OpenCore](#2-understanding-opencore)
3. [Understanding the EFI Structure](#3-understanding-the-efi-structure)
4. [Requirements](#4-requirements)
5. [Identifying Your Hardware](#5-identifying-your-hardware)
6. [Checking Hardware Compatibility](#6-checking-hardware-compatibility)
7. [Preparing Windows](#7-preparing-windows)
8. [Downloading Required Tools](#8-downloading-required-tools)
9. [Downloading macOS Recovery](#9-downloading-macos-recovery)
10. [Creating the Installation USB](#10-creating-the-installation-usb)
11. [Preparing the OpenCore EFI Folder](#11-preparing-the-opencore-efi-folder)
12. [Understanding ACPI](#12-understanding-acpi)
13. [Choosing and Installing Kexts](#13-choosing-and-installing-kexts)
14. [Installing UEFI Drivers](#14-installing-uefi-drivers)
15. [Creating config.plist](#15-creating-configplist)
16. [Configuring OpenCore](#16-configuring-opencore)
17. [Configuring SMBIOS](#17-configuring-smbios)
18. [Checking and Validating EFI](#18-checking-and-validating-efi)
19. [Configuring BIOS/UEFI](#19-configuring-biosuefi)
20. [Booting OpenCore](#20-booting-opencore)
21. [Starting macOS Recovery](#21-starting-macos-recovery)
22. [Preparing the Installation Disk](#22-preparing-the-installation-disk)
23. [Installing macOS](#23-installing-macos)
24. [Completing Initial Setup](#24-completing-initial-setup)
25. [Configuring Graphics](#25-configuring-graphics)
26. [Configuring Audio](#26-configuring-audio)
27. [Configuring Wi-Fi and Bluetooth](#27-configuring-wi-fi-and-bluetooth)
28. [Configuring Ethernet](#28-configuring-ethernet)
29. [Configuring USB Ports](#29-configuring-usb-ports)
30. [Configuring Keyboard and Trackpad](#30-configuring-keyboard-and-trackpad)
31. [Battery and Power Management](#31-battery-and-power-management)
32. [Installing OpenCore on the Internal EFI](#32-installing-opencore-on-the-internal-efi)
33. [Updating OpenCore and macOS](#33-updating-opencore-and-macos)
34. [Troubleshooting](#34-troubleshooting)
35. [Final Checklist](#35-final-checklist)
36. [Useful Resources](#36-useful-resources)
37. [Contributing](#37-contributing)
38. [License](#38-license)

---

# 1. Understanding Hackintosh

## 1.1 What is Hackintosh?

Hackintosh is the name commonly used for a non-Apple computer configured to run macOS.

A normal PC does not contain the same firmware environment and hardware configuration as an Apple computer. Therefore, additional configuration is required before macOS can start properly.

OpenCore is commonly used to prepare the environment needed to start macOS.

## 1.2 How Does It Work?

The general boot process looks like this:

```text
Computer Power On
       |
       v
BIOS / UEFI Firmware
       |
       v
OpenCore Bootloader
       |
       v
OpenCore Configuration
       |
       v
ACPI Tables + Drivers + Kexts
       |
       v
Apple Bootloader
       |
       v
macOS Kernel
       |
       v
macOS System
       |
       v
Login Screen / Desktop
```

Each stage has a different responsibility.

### BIOS / UEFI

The computer firmware initializes the hardware and searches for a bootable device.

### OpenCore

OpenCore loads its configuration and prepares the environment required by macOS.

### ACPI

ACPI tables describe hardware devices and power-management information.

### Kexts

Kernel extensions provide additional functionality or compatibility for supported hardware.

### macOS Kernel

The kernel starts the operating system and initializes its components.

---

# 2. Understanding OpenCore

OpenCore is an open-source bootloader maintained by the Acidanthera project.

Official repository:

https://github.com/acidanthera/OpenCorePkg

## 2.1 What Does OpenCore Do?

OpenCore is responsible for:

1. Loading the boot configuration.
2. Applying configured ACPI changes.
3. Loading required UEFI drivers.
4. Injecting configured kernel extensions.
5. Applying supported device properties.
6. Preparing the boot environment.
7. Presenting available operating systems in the boot picker.
8. Starting the selected operating system.

## 2.2 OpenCore and Clover

Clover and OpenCore are different bootloader projects.

OpenCore uses a structured configuration system and has its own documentation, drivers, tools, and configuration requirements.

This guide focuses on OpenCore.

Do not mix files from different bootloader projects unless a specific procedure explicitly requires it.

## 2.3 OpenCore Versions

Always download OpenCore from its official repository:

https://github.com/acidanthera/OpenCorePkg/releases

When selecting a release:

1. Check the release number.
2. Read the release notes.
3. Download the appropriate package.
4. Extract the archive.
5. Keep all OpenCore files from the same release together.

Do not combine an old `OpenCore.efi` with unrelated drivers from a different release.

---

# 3. Understanding the EFI Structure

EFI is the directory structure used by the UEFI boot process.

OpenCore expects a particular directory layout.

A typical 64-bit EFI folder looks like this:

```text
EFI
│
├── BOOT
│   └── BOOTx64.efi
│
└── OC
    │
    ├── ACPI
    │
    ├── Drivers
    │
    ├── Kexts
    │
    ├── Resources
    │
    ├── Tools
    │
    ├── OpenCore.efi
    │
    └── config.plist
```

## 3.1 BOOT

The `BOOT` directory contains the UEFI fallback bootloader.

For a typical 64-bit UEFI installation, the file is:

`BOOTx64.efi`

## 3.2 OC

The `OC` directory contains the main OpenCore components.

## 3.3 ACPI

This directory stores ACPI files, usually with the `.aml` extension.

## 3.4 Drivers

This directory stores UEFI drivers with the `.efi` extension.

## 3.5 Kexts

This directory stores kernel extensions with the `.kext` extension.

## 3.6 Resources

This directory can contain fonts, icons, audio resources, and other graphical assets.

## 3.7 Tools

This directory can contain additional utilities accessible through OpenCore.

## 3.8 config.plist

This is the main OpenCore configuration file.

It determines which files are loaded and how OpenCore behaves.

**Important:** The contents of this folder must correspond to the entries in `config.plist`.

---

# 4. Requirements

Prepare the following before beginning.

| Requirement | Purpose |
|---|---|
| Compatible computer | Runs the target operating system |
| USB flash drive | Stores the installation environment |
| Internet connection | Downloads recovery files |
| Windows, Linux, or macOS | Prepares the installation media |
| Python 3 | Runs macrecovery on Windows |
| OpenCorePkg | Provides the bootloader |
| ProperTree | Edits config.plist |
| Backup storage | Stores recovery copies |
| Sufficient disk space | Stores macOS and applications |

A USB drive with at least 16 GB of capacity is recommended.

For the installation disk, capacity requirements depend on the macOS version, applications, and intended use.

---

# 5. Identifying Your Hardware

This is one of the most important stages.

You cannot prepare a reliable EFI configuration without knowing what hardware is installed.

The objective is to identify all important components, including devices that Windows may display only by their hardware ID.

## 5.1 Identify the Processor

On Windows:

1. Press `Windows + R`.
2. Type `msinfo32`.
3. Press Enter.
4. Find the `Processor` field.
5. Copy the complete processor name.

Record:

- Manufacturer
- Model
- Generation
- Core count
- Architecture

You can also open PowerShell and run:

```powershell
Get-CimInstance Win32_Processor |
Select-Object Name, Manufacturer, NumberOfCores, NumberOfLogicalProcessors
```

Save the result.

## 5.2 Identify the Graphics Processor

1. Press `Windows + X`.
2. Select Device Manager.
3. Expand `Display adapters`.
4. Write down every listed graphics device.

For each graphics device, collect:

- Device name
- Manufacturer
- Hardware ID
- Integrated or dedicated status

To find the hardware ID:

1. Right-click the graphics device.
2. Select Properties.
3. Open the Details tab.
4. Select Hardware Ids.
5. Copy the displayed values.

The values commonly include:

```text
PCI\VEN_8086&DEV_XXXX
```

The `VEN` value identifies the manufacturer, while `DEV` identifies a specific device.

## 5.3 Identify Wi-Fi

1. Open Device Manager.
2. Expand Network adapters.
3. Find the wireless adapter.
4. Open its Properties.
5. Select Details.
6. Select Hardware Ids.
7. Copy the hardware ID.

Do not assume that every Intel or Broadcom adapter has the same macOS compatibility.

## 5.4 Identify Ethernet

Repeat the previous process for the wired network adapter.

Record the exact controller model.

## 5.5 Identify Bluetooth

1. Expand Bluetooth in Device Manager.
2. Find the Bluetooth device.
3. Open its Properties.
4. Copy the hardware ID.

Bluetooth and Wi-Fi may be separate devices even when they share the same physical wireless card.

## 5.6 Identify Audio

1. Expand Sound, video and game controllers.
2. Identify the audio controller.
3. Open its Properties.
4. Copy its hardware ID.

For a laptop, also identify the audio codec when possible.

## 5.7 Identify Storage

Open Disk Management:

1. Right-click Start.
2. Select Disk Management.
3. Identify each physical drive.
4. Record its capacity, model, and partition layout.

Also record whether the storage device is SATA or NVMe.

## 5.8 Save a Hardware Report

Create a text file named:

`hardware-report.txt`

Use the following structure:

```text
CPU:
GPU:
RAM:
Storage:
Wi-Fi:
Bluetooth:
Ethernet:
Audio:
Touchpad:
Keyboard:
BIOS Version:
Firmware Mode:
```

Keep this report available throughout the installation.

---

# 6. Checking Hardware Compatibility

Identifying hardware is not enough. Each component must also be checked against the intended macOS version.

## 6.1 CPU Compatibility

The supported CPU families and required configuration depend on the macOS release.

Select the appropriate platform guide:

- Intel desktop
- Intel laptop
- AMD desktop

Some devices require additional configuration or patches.

Do not use Intel-specific CPU configuration on an AMD system.

## 6.2 GPU Compatibility

Graphics support is especially important.

The graphics processor must be supported by the chosen macOS release.

Check:

- GPU generation
- Device ID
- Architecture
- Required framebuffer configuration
- Graphics acceleration support

A system may boot without full graphics acceleration, but that does not mean the GPU is properly configured.

## 6.3 Wi-Fi Compatibility

Some wireless adapters need additional drivers.

Others may have limited functionality or no support.

Check the exact adapter model before selecting a wireless solution.

## 6.4 Storage Compatibility

Check whether your storage controller and NVMe device are compatible.

Some systems need additional configuration for storage detection or power management.

## 6.5 Compatibility Resources

Use the following references:

- https://dortania.github.io/OpenCore-Install-Guide/
- https://dortania.github.io/GPU-Buyers-Guide/
- https://dortania.github.io/Wireless-Buyers-Guide/
- https://dortania.github.io/OpenCore-Install-Guide/macos-limits.html

**Do not proceed until the major hardware components have been identified and compatibility has been investigated.**

---

# 7. Preparing Windows

This section explains how to prepare the tools and files using Windows.

## 7.1 Back Up Important Data

Before changing partitions or firmware settings:

1. Copy important documents to another storage device.
2. Save photos and personal files.
3. Back up important application data.
4. Make sure you can access your Windows recovery options.
5. Check BitLocker status if encryption is enabled.
6. Keep your BitLocker recovery key available.

Never experiment with the internal Windows disk while preparing the installer USB.

## 7.2 Check UEFI Mode

1. Press `Windows + R`.
2. Type `msinfo32`.
3. Press Enter.
4. Find `BIOS Mode`.

It should normally display:

`UEFI`

If it displays `Legacy`, additional investigation is required before following the UEFI instructions.

## 7.3 Create a Working Folder

Create a folder on a drive with enough free space.

For example:

```text
C:\Hackintosh\
│
├── Downloads\
├── OpenCore\
├── Kexts\
├── ACPI\
├── Drivers\
├── EFI-Backup\
└── Hardware-Report\
```

Keep downloaded files organized.

Do not place unrelated files inside the final EFI folder.

---

# 8. Downloading Required Tools

Download tools only from their official project pages.

| Tool | Purpose | Official Source |
|---|---|---|
| OpenCorePkg | OpenCore bootloader | github.com/acidanthera/OpenCorePkg |
| ProperTree | Plist editor | github.com/corpnewt/ProperTree |
| GenSMBIOS | SMBIOS utility | github.com/corpnewt/GenSMBIOS |
| Python | Runs macrecovery | python.org |
| Rufus | Formats USB drives | rufus.ie |

## 8.1 Download OpenCorePkg

1. Open the OpenCorePkg release page.
2. Select the appropriate release.
3. Download the release archive.
4. Extract the archive.
5. Keep the extracted directory unchanged.

Official page:

https://github.com/acidanthera/OpenCorePkg/releases

## 8.2 Install Python

1. Open the official Python download page.
2. Download Python 3 for Windows.
3. Run the installer.
4. Enable the option to add Python to PATH if available.
5. Complete installation.

To check Python:

1. Open Command Prompt.
2. Run:

```cmd
py --version
```

A Python version should appear.

If Windows cannot find `py`, verify that Python was installed correctly.

## 8.3 Download ProperTree

1. Open the ProperTree repository.
2. Select Code.
3. Select Download ZIP.
4. Extract the archive.
5. Keep the folder available.

On Windows, ProperTree can be opened using:

`ProperTree.bat`

## 8.4 Download GenSMBIOS

1. Open the GenSMBIOS repository.
2. Download its ZIP archive.
3. Extract it.
4. Read the included instructions before using it.

Do not publish generated serial numbers or other identifying values.

---

# 9. Downloading macOS Recovery

This guide uses Apple's recovery download mechanism through the `macrecovery` utility included in OpenCorePkg.

The recovery installer is downloaded from Apple's servers.

A recovery installer is not the same as a complete offline installer. It requires a working internet connection during installation.

## 9.1 Find macrecovery

1. Open the extracted OpenCorePkg directory.
2. Open `Utilities`.
3. Open `macrecovery`.

The folder should contain:

```text
macrecovery
│
├── macrecovery.py
└── Other utility files
```

## 9.2 Open Command Prompt in the Correct Folder

1. Open the `macrecovery` folder.
2. Click the File Explorer address bar.
3. Type:

```text
cmd
```

4. Press Enter.

Command Prompt should now open inside the `macrecovery` directory.

## 9.3 Download the Current Tahoe Recovery

For the Tahoe 26 recovery command documented by OpenCore:

```cmd
py macrecovery.py -b Mac-CFF7D910A743CAAF -m 00000000000000000 -os latest download
```

Use the appropriate command for the macOS version you intend to install.

Do not assume that a recovery identifier for one macOS version is suitable for every other version.

## 9.4 Wait for the Download

The download may take some time.

Do not close Command Prompt while the download is running.

When it finishes, inspect the generated files.

You should obtain a recovery image and its corresponding chunklist.

Common filenames include:

```text
BaseSystem.dmg
BaseSystem.chunklist
```

Depending on the recovery method, the output may instead use `RecoveryImage` filenames.

Keep the matching image and chunklist together.

## 9.5 Check the Download

Before copying the files:

1. Check that the download completed.
2. Confirm that the image file exists.
3. Confirm that its corresponding chunklist exists.
4. Make sure neither file has a suspiciously small size.
5. Do not rename one file without renaming it according to the relevant instructions.

Reference:

https://dortania.github.io/OpenCore-Install-Guide/installer-guide/mac-install-recovery.html

---

# 10. Creating the Installation USB

This section explains how to prepare the USB drive on Windows.

**Warning: Formatting the USB erases its existing contents. Make absolutely sure that the selected device is the USB drive, not an internal disk.**

## 10.1 Connect the USB Drive

1. Connect the USB drive.
2. Open File Explorer.
3. Confirm that Windows detects it.
4. Check its drive letter.
5. Confirm its capacity.

## 10.2 Format Using Rufus

1. Open Rufus.
2. Find the Device field.
3. Select your USB drive.
4. Check the displayed capacity.
5. Set Boot selection to `Non bootable`.
6. Select `Large FAT32` as the file system when available.
7. Start the formatting process.
8. Confirm that the selected USB can be erased.

Do not select your Windows system disk.

## 10.3 Open the USB Drive

After formatting, open the USB drive in File Explorer.

Its root directory should be empty or nearly empty.

## 10.4 Create the Recovery Directory

Create a folder named exactly:

```text
com.apple.recovery.boot
```

The folder must be located at the root of the USB drive.

Correct:

```text
USB:\
└── com.apple.recovery.boot\
```

Incorrect:

```text
USB:\
└── SomeFolder\
    └── com.apple.recovery.boot\
```

## 10.5 Copy the Recovery Files

Copy both matching recovery files into the new directory.

Example:

```text
USB:\
└── com.apple.recovery.boot\
    ├── BaseSystem.dmg
    └── BaseSystem.chunklist
```

Do not place them inside another unnecessary directory.

---

# 11. Preparing the OpenCore EFI Folder

Now the USB needs the OpenCore bootloader.

## 11.1 Find the Correct OpenCore Folder

1. Open the extracted OpenCorePkg directory.
2. Open `X64` for a typical 64-bit UEFI system.
3. Find the `EFI` folder.
4. Copy the complete folder.

## 11.2 Copy EFI to the USB

Paste the `EFI` folder into the root of the USB drive.

The basic structure should now look like this:

```text
USB:\
│
├── EFI\
│   ├── BOOT\
│   │   └── BOOTx64.efi
│   │
│   └── OC\
│       ├── ACPI\
│       ├── Drivers\
│       ├── Kexts\
│       ├── Resources\
│       ├── Tools\
│       └── OpenCore.efi
│
└── com.apple.recovery.boot\
    ├── BaseSystem.dmg
    └── BaseSystem.chunklist
```

The EFI folder is not yet fully configured.

It is only the starting structure.

---

# 12. Understanding ACPI

ACPI is an important part of hardware configuration.

It describes devices and power-management methods that the operating system can use.

On some systems, the original firmware tables are not presented in the way macOS expects.

Additional SSDT files or carefully selected patches may therefore be required.

## 12.1 What Is an SSDT?

An SSDT is a Secondary System Description Table.

It can describe or modify selected ACPI devices and methods.

Common examples include:

| SSDT | General Purpose |
|---|---|
| SSDT-EC | Embedded Controller compatibility |
| SSDT-PLUG | CPU power-management configuration |
| SSDT-PNLF | Backlight control on supported laptops |
| SSDT-USBX | USB power properties |
| SSDT-AWAC | Certain system-clock compatibility issues |

These files are examples only.

Not every computer needs every SSDT.

## 12.2 Find the Correct ACPI Files

1. Identify your CPU generation.
2. Determine whether the system is a laptop or desktop.
3. Open the matching OpenCore platform guide.
4. Review its ACPI requirements.
5. Select only the SSDTs required for your hardware.
6. Put the selected `.aml` files in:

`EFI/OC/ACPI/`

Do not download every SSDT from a random EFI repository.

## 12.3 Add ACPI Files to config.plist

Once files have been copied, update the OpenCore configuration.

ProperTree can perform a Clean Snapshot to add the ACPI entries.

Later, check that each entry points to a real file.

Reference:

https://dortania.github.io/Getting-Started-With-ACPI/

---

# 13. Choosing and Installing Kexts

A Kext is a kernel extension.

Kexts are used to provide additional functionality and hardware compatibility.

They must be selected according to the specific computer.

## 13.1 Common Kexts

The following table explains several commonly encountered projects.

| Kext | General Purpose |
|---|---|
| Lilu | Framework used by many other Kexts |
| VirtualSMC | SMC emulation |
| WhateverGreen | Graphics-related compatibility |
| AppleALC | Audio compatibility |
| IntelMausi | Supported Intel Ethernet controllers |
| NVMeFix | NVMe-related compatibility and power management |
| VoodooPS2Controller | Supported PS/2 input devices |
| VoodooI2C | Supported I2C input devices |
| USBToolBox | USB mapping workflow |
| IntelBluetoothFirmware | Supported Intel Bluetooth devices |
| BlueToolFixup | Bluetooth compatibility for supported configurations |

These are examples, not a universal installation list.

## 13.2 Download Kexts

For each required Kext:

1. Open its official repository.
2. Read the compatibility notes.
3. Select a release appropriate for your macOS version.
4. Download the release archive.
5. Extract the archive.
6. Locate the `.kext` bundle.
7. Verify its actual name.
8. Copy it into:

`EFI/OC/Kexts/`

Do not copy source-code folders into the Kexts directory.

## 13.3 Check Kext Dependencies

Some Kexts require another Kext to load first.

For example, Lilu is a dependency for several commonly used Kexts.

ProperTree and OpenCore documentation should be used to verify the final load order.

Do not arrange Kexts alphabetically without checking their dependencies.

## 13.4 Check Compatibility

Before adding a Kext, confirm:

- Supported processor architecture
- Supported macOS versions
- Required dependencies
- Required configuration entries
- Whether the hardware actually needs it

Adding unnecessary Kexts can make troubleshooting more difficult.

---

# 14. Installing UEFI Drivers

UEFI drivers are loaded before macOS starts.

They are separate from kernel extensions.

Some commonly encountered drivers include:

| Driver | General Purpose |
|---|---|
| OpenRuntime.efi | OpenCore runtime support |
| HfsPlus.efi | Access to supported HFS+ volumes |
| OpenCanopy.efi | Graphical OpenCore picker |

The required drivers depend on the intended configuration.

## 14.1 Copy the Drivers

1. Open your chosen OpenCore release.
2. Find its `X64/EFI/OC/Drivers` directory.
3. Select the drivers required by the matching guide.
4. Copy them to your working EFI.

## 14.2 Update config.plist

Every enabled driver must have a corresponding entry in the UEFI Drivers section.

A driver existing in the folder does not automatically mean that OpenCore will load it.

Check its path and enabled status.

---

# 15. Creating config.plist

This is one of the most important stages.

`config.plist` determines how OpenCore loads the system.

A configuration copied from another computer can contain incorrect device information, patches, or drivers.

## 15.1 Locate Sample.plist

Inside OpenCorePkg, open:

`Docs`

Find:

`Sample.plist`

Copy it into:

`EFI/OC/`

Rename the copied file to:

`config.plist`

Do not edit the original release file.

## 15.2 Open ProperTree

1. Open the ProperTree directory.
2. Run `ProperTree.bat`.
3. Select File.
4. Select Open.
5. Open your new `config.plist`.

Alternatively, use the keyboard shortcut:

`Ctrl + O`

## 15.3 Perform a Clean Snapshot

1. Keep `config.plist` open.
2. Press:

`Ctrl + Shift + R`

3. Select the `EFI/OC` folder.
4. Confirm the Clean Snapshot.

This updates the file lists from the contents of the EFI folder.

It helps synchronize the ACPI, Kext, and driver entries.

If ProperTree displays a duplicate-plugin warning, investigate it and follow the tool's prompt carefully.

## 15.4 Understand the Main Sections

The configuration contains several important sections.

| Section | Purpose |
|---|---|
| ACPI | ACPI tables, patches and quirks |
| Booter | Booter configuration |
| DeviceProperties | Hardware properties |
| Kernel | Kexts, patches and kernel quirks |
| Misc | Picker, security and debugging settings |
| NVRAM | macOS startup variables |
| PlatformInfo | SMBIOS and platform information |
| UEFI | Firmware drivers and UEFI settings |

## 15.5 Do Not Copy Random Configuration Values

The correct values depend on:

- CPU generation
- CPU manufacturer
- Laptop or desktop
- Graphics hardware
- Firmware
- macOS version
- Storage controller
- Input devices

Follow the matching platform-specific guide for every required setting.

Official configuration guide:

https://dortania.github.io/OpenCore-Install-Guide/config.plist/

---

# 16. Configuring OpenCore

Now configure each relevant section.

The exact values must be selected from the matching platform guide.

## 16.1 ACPI

Open:

`ACPI`

Check:

- Add
- Delete
- Patch
- Quirks

Make sure every enabled ACPI file exists in:

`EFI/OC/ACPI/`

Do not enable patches just because they appear in an example.

## 16.2 Booter

Open:

`Booter`

This section controls certain bootloader and memory-related behaviors.

Some settings are CPU- and firmware-specific.

Follow the relevant guide rather than copying values from another system.

## 16.3 DeviceProperties

Open:

`DeviceProperties`

This section can inject device properties into supported hardware.

Graphics configuration is one of its common uses.

The required properties depend on the GPU and PCI device path.

Do not copy graphics properties from an unrelated GPU.

## 16.4 Kernel

Open:

`Kernel`

Review:

- Add
- Block
- Emulate
- Patch
- Quirks
- Scheme

The Add section contains the Kext list.

Check that:

1. Each required Kext is listed.
2. The path matches the real filename.
3. The executable path is correct.
4. The bundle identifier is correct.
5. Enabled is set appropriately.
6. Dependencies load in the required order.

## 16.5 Misc

Open:

`Misc`

This section contains settings related to the OpenCore picker, security, debugging and other functions.

For initial troubleshooting, useful debugging options may be enabled according to the official guide.

Do not change security settings without understanding their purpose.

## 16.6 NVRAM

Open:

`NVRAM`

This section stores startup variables and configured boot arguments.

Some common boot arguments are used for diagnosis, but they should not be copied indiscriminately.

Only add boot arguments required by the platform guide or a specific troubleshooting procedure.

## 16.7 PlatformInfo

Open:

`PlatformInfo`

This is where the SMBIOS information is configured.

Follow the next section.

## 16.8 UEFI

Open:

`UEFI`

Check:

- Drivers
- Input
- Output
- Quirks
- APFS
- ProtocolOverrides

Driver paths must match the real files inside:

`EFI/OC/Drivers/`

---

# 17. Configuring SMBIOS

SMBIOS identifies the Mac model that the system reports to macOS.

Selecting an appropriate model is an important part of configuration.

## 17.1 Choose an Appropriate SMBIOS

The choice depends on:

- CPU generation
- GPU compatibility
- Laptop or desktop status
- macOS version
- Hardware features

Do not select an SMBIOS simply because it is popular.

Use the recommendations in the matching Dortania platform guide.

## 17.2 Generate SMBIOS Data

GenSMBIOS can generate the required data.

General process:

1. Open GenSMBIOS.
2. Read its instructions.
3. Select the appropriate operation.
4. Select the SMBIOS model chosen from the compatibility guide.
5. Generate the required identifiers.
6. Enter the values in the correct `PlatformInfo` fields.
7. Save your configuration.

Do not publish generated serial numbers, UUIDs, or other identifying values.

## 17.3 Check PlatformInfo

Review the following fields:

- SystemProductName
- SystemSerialNumber
- MLB
- SystemUUID
- ROM

The fields must be configured consistently.

Do not reuse another person's identifiers.

---

# 18. Checking and Validating EFI

Before attempting installation, inspect the complete EFI.

## 18.1 Check the Directory Structure

Confirm that the folders exist:

```text
EFI
├── BOOT
│   └── BOOTx64.efi
│
└── OC
    ├── ACPI
    ├── Drivers
    ├── Kexts
    ├── Resources
    ├── Tools
    ├── OpenCore.efi
    └── config.plist
```

## 18.2 Check ACPI Files

For each entry in:

`ACPI -> Add`

Confirm that the corresponding `.aml` file exists.

## 18.3 Check Kexts

For each enabled entry in:

`Kernel -> Add`

Confirm that:

- BundlePath is correct.
- ExecutablePath is correct.
- PlistPath is correct.
- The Kext exists.
- Required dependencies are present.

## 18.4 Check Drivers

For every enabled driver in:

`UEFI -> Drivers`

Confirm that the driver exists in the EFI folder.

## 18.5 Run ocvalidate

Open the `Utilities/ocvalidate` directory in the OpenCore release.

On Windows, run the matching executable against your configuration.

Example:

```cmd
ocvalidate.exe "E:\EFI\OC\config.plist"
```

Replace `E:` with the actual drive letter of the USB EFI volume.

Resolve the reported errors.

Use the `ocvalidate` version that matches the OpenCore release you are using.

## 18.6 Save a Backup

Before testing:

1. Copy the complete working EFI folder.
2. Save it outside the USB drive.
3. Name it with the date and OpenCore version.
4. Do not edit the only working copy.

Example:

```text
EFI-Backup/
└── OpenCore-1.0.x-Initial/
```

---

# 19. Configuring BIOS/UEFI

BIOS settings can affect OpenCore and macOS startup.

The available options differ between computer manufacturers.

Before changing anything:

1. Record the original settings.
2. Take photographs of relevant BIOS pages.
3. Make only changes required by your hardware guide.
4. Avoid changing storage-controller settings without understanding the effect on Windows.

Common settings that may need investigation include:

| Setting | Why It Matters |
|---|---|
| UEFI Boot | Determines the firmware boot method |
| Secure Boot | May affect third-party bootloader loading |
| Fast Boot | Can skip device initialization |
| CSM | Controls legacy compatibility |
| SATA Mode | Affects storage-controller operation |
| VT-d | Virtualization and DMA-related behavior |
| CFG Lock | CPU configuration lock |
| Above 4G Decoding | PCI resource allocation |
| XHCI Hand-off | USB controller hand-off |

These are not universal instructions to disable or enable everything.

Use the relevant OpenCore guide for your platform.

---

# 20. Booting OpenCore

Now the prepared USB can be tested.

## 20.1 Connect the USB

Connect the installation USB directly to the computer.

For initial testing, avoid unnecessary USB hubs.

## 20.2 Open the Boot Menu

1. Restart the computer.
2. Press the boot-menu key.
3. Open the list of boot devices.
4. Select the USB's UEFI entry.

Common boot-menu keys include F12, F11, F10 and Esc, depending on the manufacturer.

## 20.3 Check the OpenCore Picker

If the EFI is working, the OpenCore picker should appear.

It may show:

- macOS Recovery
- Windows
- Other detected boot entries
- OpenCore tools, depending on the configuration

If the picker does not appear, stop and troubleshoot OpenCore before proceeding.

## 20.4 Enable Verbose Boot for Troubleshooting

The `-v` boot argument can display startup messages instead of hiding them behind the Apple logo.

This helps identify the stage at which startup fails.

Keep a photograph of the last visible message if the system stops.

Do not assume that the final visible message is always the root cause.

---

# 21. Starting macOS Recovery

When the OpenCore picker appears:

1. Find the recovery entry.
2. Select it.
3. Press Enter.
4. Wait for the recovery environment to load.

The first start may take longer than a normal operating-system boot.

Recovery requires a supported internet connection for downloading macOS files.

If Recovery cannot connect:

- Check whether the network adapter is supported.
- Check whether Ethernet is available.
- Confirm the computer has a working internet connection.
- Review the network adapter's compatibility documentation.

---

# 22. Preparing the Installation Disk

Disk selection is one of the most dangerous parts of the installation process.

Selecting the wrong device can delete an existing operating system and its files.

## 22.1 Open Disk Utility

Inside macOS Recovery:

1. Select Disk Utility.
2. Open View.
3. Select Show All Devices.

This displays physical disks as well as their partitions.

## 22.2 Identify the Correct Disk

Before erasing anything, compare:

- Disk capacity
- Disk model
- Connection type
- Existing partitions
- Intended installation destination

Never identify a disk only by its displayed name.

## 22.3 Choose an Installation Strategy

For a beginner, using a separate physical drive for macOS is generally easier to manage than modifying a Windows disk.

### Separate physical disk

1. Identify the dedicated macOS disk.
2. Confirm it contains no data that needs to be preserved.
3. Select the physical device.
4. Choose Erase.
5. Use GUID Partition Map.
6. Use APFS as the format when appropriate.
7. Give the volume a recognizable name.
8. Confirm the operation.

### Existing disk containing Windows

Dual-boot installations require additional planning.

Do not erase the complete physical disk.

Do not delete the Windows EFI partition.

Do not change Windows partitions unless you understand the consequences and have a complete backup.

For a first installation, avoid modifying a working Windows installation.

---

# 23. Installing macOS

Once the destination is ready:

## 23.1 Start the Installer

1. Close Disk Utility.
2. Return to the Recovery main window.
3. Select Install macOS.
4. Continue.
5. Accept the displayed installation terms if you choose to proceed.
6. Select the intended macOS destination.
7. Start the installation.

## 23.2 Wait for Installation

The installation may take a considerable amount of time.

The computer may restart more than once.

Do not interrupt the process simply because the progress bar appears stationary.

## 23.3 Select the Correct Boot Entry After Restart

After a restart, return to the OpenCore picker.

Depending on the installation stage, the required entry may be named:

- macOS Installer
- The name of your macOS installation volume

Select the entry that continues the installation.

Do not repeatedly select the original Recovery entry if the installer has created a new installation entry.

## 23.4 Continue Until Setup Assistant Appears

Repeat the restart and boot-entry selection process until the macOS setup assistant appears.

Keep the USB connected throughout this process.

---

# 24. Completing Initial Setup

Once macOS reaches Setup Assistant:

1. Select the language.
2. Select the region.
3. Configure accessibility settings.
4. Connect to a supported network.
5. Continue through the setup screens.
6. Configure the local user account.
7. Complete the initial desktop setup.

Some online Apple services may require additional verification or compatible hardware configuration.

Do not troubleshoot unrelated services until basic system startup is stable.

---

# 25. Configuring Graphics

A system that displays the desktop is not necessarily using full graphics acceleration.

Graphics acceleration is important for smooth animations, video playback, and normal desktop performance.

## 25.1 Check the Graphics Device

Open:

Apple Menu → About This Mac

Check whether the displayed graphics information is correct.

You can also inspect:

System Information → Graphics/Displays

## 25.2 What to Check

- Is the graphics device detected?
- Is the display resolution correct?
- Is the correct amount of graphics memory reported?
- Is graphics acceleration working?
- Does display output work reliably?
- Does external display support work, when applicable?

## 25.3 Graphics Configuration

The correct configuration depends on the GPU.

Some systems require specific DeviceProperties or framebuffer settings.

Others may need different Kexts or patches.

Use the matching graphics section in the OpenCore guide.

Do not apply a framebuffer configuration from another GPU.

---

# 26. Configuring Audio

Audio support varies depending on the audio codec and hardware connection.

## 26.1 Identify the Audio Codec

Before configuring audio, identify the codec.

A laptop's internal audio codec is not necessarily the same as its external audio controller.

## 26.2 AppleALC

AppleALC provides support for a range of compatible audio codecs.

It commonly works alongside Lilu.

However, installing AppleALC does not automatically guarantee sound.

## 26.3 Audio Layout

A suitable layout identifier may be required.

The correct identifier depends on the codec and its supported layouts.

Use the supported-codec documentation rather than trying random values.

## 26.4 Test Audio

After configuration:

1. Open System Settings.
2. Select Sound.
3. Check the output device.
4. Play a test sound.
5. Test the internal speakers.
6. Test headphones.
7. Test the microphone.
8. Check volume control.

Record any missing outputs or inputs for troubleshooting.

---

# 27. Configuring Wi-Fi and Bluetooth

Wireless support depends on the exact wireless chipset.

Do not choose a driver based only on a brand name.

## 27.1 Identify the Wireless Chipset

Check the hardware ID and compare it with the supported device list.

## 27.2 Install the Appropriate Support

Some Intel wireless devices use projects maintained by OpenIntelWireless.

Other adapters may need different solutions.

Always check compatibility with the chosen macOS version.

Official project:

https://github.com/OpenIntelWireless

## 27.3 Test Wi-Fi

After configuration:

1. Open System Settings.
2. Open Wi-Fi.
3. Enable Wi-Fi.
4. Check whether networks appear.
5. Connect to your network.
6. Test internet access.

## 27.4 Test Bluetooth

1. Open System Settings.
2. Open Bluetooth.
3. Enable Bluetooth.
4. Search for a nearby supported device.
5. Test pairing.
6. Check whether the device reconnects after a restart.

Wi-Fi and Bluetooth may need separate configuration.

---

# 28. Configuring Ethernet

Ethernet is often easier to configure than wireless networking, but the correct driver still depends on the controller.

Common driver families support different Ethernet chipsets.

## Steps

1. Identify the Ethernet controller.
2. Check the controller's compatibility.
3. Select its appropriate Kext, if required.
4. Place the Kext in `EFI/OC/Kexts/`.
5. Add it to `config.plist`.
6. Restart macOS.
7. Connect an Ethernet cable.
8. Open System Settings → Network.
9. Confirm that the Ethernet interface appears.
10. Test network access.

Do not install several unrelated Ethernet Kexts simply to see which one works.

---

# 29. Configuring USB Ports

USB configuration is particularly important on recent macOS releases.

A computer may have several USB controllers and many physical USB ports.

Not every physical connector necessarily corresponds to only one internal port entry.

## 29.1 Why USB Mapping Matters

Correct USB mapping helps macOS identify the USB ports that should be available.

It can also improve:

- Port functionality
- Sleep and wake behavior
- Internal Bluetooth connectivity
- USB device stability

## 29.2 Understand USB Port Types

USB port entries can represent:

- USB 2
- USB 3
- Internal USB connections
- Type-C connectors
- Other controller-specific ports

## 29.3 Create a USB Map

Use the USB mapping guide that matches your controller.

The USBToolBox project provides a mapping workflow for supported Intel systems.

Manual mapping is also documented for systems that require it.

## 29.4 Test the Final Map

After configuring the USB map:

1. Restart macOS.
2. Test each physical USB port.
3. Test USB 2 devices.
4. Test USB 3 devices.
5. Test internal Bluetooth if available.
6. Test sleep and wake.
7. Check that essential ports remain available.

Do not rely on the `XhciPortLimit` quirk as a permanent substitute for proper USB mapping.

Reference:

https://dortania.github.io/OpenCore-Post-Install/usb/

---

# 30. Configuring Keyboard and Trackpad

Laptop input hardware can use different connection protocols.

Common examples include:

- PS/2
- I2C
- USB
- Other platform-specific interfaces

## 30.1 Identify the Input Device

Check the device information and hardware IDs.

Determine whether the keyboard and touchpad use the same controller or separate interfaces.

## 30.2 Select the Correct Driver

Some systems use VoodooPS2Controller.

Others use VoodooI2C and related components.

The driver choice depends on the actual connection type.

## 30.3 Test the Keyboard

Check:

- All standard keys
- Function keys
- Modifier keys
- Keyboard backlight, if supported
- Special laptop keys

## 30.4 Test the Trackpad

Check:

- Pointer movement
- Single-finger clicking
- Two-finger scrolling
- Multi-finger gestures
- Physical clicking
- Tap-to-click

Do not assume that every touchpad gesture is supported on every device.

---

# 31. Battery and Power Management

Power management is important for laptops.

A system that boots successfully may still have problems with sleep, battery reporting, or power consumption.

## 31.1 Battery Reporting

Check whether macOS displays:

- Battery percentage
- Charging status
- Remaining capacity
- Power adapter status

Some systems need additional ACPI support or a suitable battery Kext.

## 31.2 Sleep and Wake

Test:

1. Select Sleep.
2. Wait for the computer to enter sleep.
3. Wake it using the power button or supported input.
4. Check whether the display returns.
5. Check whether Wi-Fi reconnects.
6. Check whether audio still works.
7. Check whether USB devices remain available.

## 31.3 Power Management

Power-management behavior depends on the CPU generation and platform configuration.

Follow the appropriate OpenCore guide.

Do not apply generic CPU power-management settings without checking the processor and macOS version.

---

# 32. Installing OpenCore on the Internal EFI

The USB installer should remain available until the system has been tested.

Only move OpenCore to the internal EFI after the external EFI is known to work.

## 32.1 Back Up the USB EFI

1. Open the USB EFI volume.
2. Copy the complete `EFI` folder.
3. Store it in a safe backup location.
4. Do not edit or delete the only working copy.

## 32.2 Identify the Correct Internal EFI Partition

Use a reliable EFI mounting method.

Check the disk and partition identifiers carefully.

Do not assume that the first EFI partition belongs to macOS.

A dual-boot system may use an existing EFI partition containing Windows boot files.

## 32.3 Copy the Tested EFI

Copy the tested OpenCore EFI folder into the intended internal EFI partition.

When Windows boot files already exist, do not delete the existing `Microsoft` directory.

Do not replace the complete EFI partition with an empty folder.

Preserve the existing boot files.

## 32.4 Test Internal Boot

1. Shut down.
2. Remove the USB installer.
3. Start the computer.
4. Select the internal OpenCore boot entry.
5. Confirm that macOS starts.
6. Confirm that Windows remains accessible.

If internal boot fails, reconnect the USB and use the previously tested EFI.

---

# 33. Updating OpenCore and macOS

Updates should be treated as a separate maintenance task.

Do not update multiple components at once without a backup.

## 33.1 Before Updating OpenCore

1. Back up the working EFI.
2. Read the OpenCore release notes.
3. Download the new release.
4. Check whether configuration entries have changed.
5. Review Kext compatibility.
6. Review UEFI driver compatibility.
7. Replace related OpenCore files as documented.
8. Update `config.plist` using the matching documentation.
9. Run the matching `ocvalidate`.
10. Test the updated EFI from USB first.

## 33.2 Before Updating Kexts

For each Kext:

1. Open the official repository.
2. Read the release notes.
3. Confirm macOS compatibility.
4. Check required dependencies.
5. Download the new version.
6. Replace the old Kext.
7. Update the configuration if necessary.
8. Test the system.

## 33.3 Before Updating macOS

1. Check the macOS release notes.
2. Check hardware compatibility.
3. Check OpenCore compatibility.
4. Check graphics support.
5. Check Kext support.
6. Back up important files.
7. Back up the working EFI.
8. Confirm that you have a recovery method.

Do not assume that a macOS update will preserve every existing Hackintosh configuration.

---

# 34. Troubleshooting

Troubleshooting should be performed systematically.

First identify the exact stage at which the failure occurs.

## 34.1 OpenCore Does Not Appear

Check:

- Is the USB detected by BIOS?
- Is the USB booted in UEFI mode?
- Does `EFI/BOOT/BOOTx64.efi` exist?
- Is the EFI directory in the root of the USB?
- Is the USB formatted correctly?
- Is the firmware boot configuration correct?

## 34.2 OpenCore Picker Appears but macOS Does Not Start

Check:

- Is the recovery image present?
- Is the recovery folder named correctly?
- Is the OpenCore version consistent?
- Is `config.plist` valid?
- Are required drivers enabled?
- Are required Kexts present?
- Is the correct platform configuration selected?

## 34.3 Stuck During Boot

Enable appropriate debugging.

Use verbose output to determine the stage where the process stops.

Record:

- The last visible messages
- The selected boot entry
- OpenCore version
- macOS version
- Relevant hardware IDs
- Recent configuration changes

Change one relevant setting at a time.

## 34.4 Black Screen

Investigate:

- GPU compatibility
- Framebuffer configuration
- DeviceProperties
- Display output
- Graphics-related Kexts
- The selected SMBIOS

Do not immediately assume that the entire installation is broken.

## 34.5 No Wi-Fi

Investigate:

- Exact wireless chipset
- Hardware ID
- macOS version
- Driver compatibility
- Kext dependencies
- Configuration entries

## 34.6 No Audio

Investigate:

- Audio codec
- AppleALC compatibility
- Lilu version
- Layout configuration
- Audio device detection

## 34.7 No Bluetooth

Investigate:

- Bluetooth hardware ID
- Wireless card details
- Required firmware
- Bluetooth Kext dependencies
- macOS compatibility

## 34.8 USB Ports Do Not Work

Investigate:

- USB controller
- USB mapping
- Port types
- USB map Kext
- Internal USB connections

## 34.9 macOS Installer Cannot Find the Disk

Investigate:

- Whether the intended disk is detected by Disk Utility
- Storage-controller compatibility
- Controller configuration
- Supported filesystem
- The intended partition scheme

Do not erase other disks to try to solve a storage-detection problem.

## 34.10 Use Official Troubleshooting Documentation

- https://dortania.github.io/OpenCore-Install-Guide/troubleshooting/troubleshooting.html
- https://dortania.github.io/OpenCore-Install-Guide/troubleshooting/boot.html
- https://dortania.github.io/OpenCore-Post-Install/

---

# 35. Final Checklist

Before considering the system complete, check every item.

## Installation

- [ ] Hardware identified
- [ ] Hardware compatibility checked
- [ ] macOS recovery downloaded
- [ ] USB created
- [ ] OpenCore EFI prepared
- [ ] config.plist configured
- [ ] EFI validated
- [ ] BIOS configuration checked
- [ ] OpenCore picker starts
- [ ] macOS installer starts
- [ ] macOS installation completed

## Hardware

- [ ] Graphics acceleration
- [ ] Correct display resolution
- [ ] Internal speakers
- [ ] Headphone output
- [ ] Microphone
- [ ] Wi-Fi
- [ ] Bluetooth
- [ ] Ethernet
- [ ] USB ports
- [ ] Keyboard
- [ ] Trackpad
- [ ] Battery reporting
- [ ] Sleep and wake
- [ ] Power management

## Maintenance

- [ ] EFI backup created
- [ ] Internal EFI tested
- [ ] Windows boot verified, if installed
- [ ] OpenCore version documented
- [ ] macOS version documented
- [ ] Kext versions documented
- [ ] Private SMBIOS values removed from public files

---

# 36. Useful Resources

The following projects contain detailed technical documentation.

| Project | Purpose |
|---|---|
| [OpenCorePkg](https://github.com/acidanthera/OpenCorePkg) | OpenCore bootloader |
| [OpenCore Install Guide](https://dortania.github.io/OpenCore-Install-Guide/) | Main installation documentation |
| [OpenCore Post-Install](https://dortania.github.io/OpenCore-Post-Install/) | Post-installation configuration |
| [Getting Started with ACPI](https://dortania.github.io/Getting-Started-With-ACPI/) | ACPI and SSDT information |
| [ProperTree](https://github.com/corpnewt/ProperTree) | Plist editor |
| [GenSMBIOS](https://github.com/corpnewt/GenSMBIOS) | SMBIOS utility |
| [OpenIntelWireless](https://github.com/OpenIntelWireless) | Intel wireless projects |

---

# 37. Contributing

Contributions are welcome!

You can help improve this repository by:

- Correcting documentation mistakes.
- Improving explanations.
- Adding troubleshooting examples.
- Updating outdated links.
- Reporting broken instructions.
- Adding hardware compatibility notes.

## Contribution Guidelines

Before submitting changes:

1. Make sure the information is accurate.
2. Prefer official documentation.
3. Identify which macOS versions are covered.
4. Identify which hardware platforms are affected.
5. Explain any hardware-specific limitations.
6. Do not publish personal serial numbers or UUIDs.
7. Do not upload copyrighted macOS installer images.
8. Do not upload another person's private EFI configuration.

---

# 38. License

This repository's original documentation is licensed under the MIT License.

See the `LICENSE` file for details.

The MIT License applies to this repository's original content. It does not automatically apply to Apple software, third-party projects, or files with separate licenses.

---

<p align="center">

# Simple Hackintosh

### Learn the process. Understand the configuration. Build responsibly.

</p>
