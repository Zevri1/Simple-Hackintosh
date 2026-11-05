# 11 — macOS 26 Tahoe notes

macOS 26 changes some assumptions that were common in earlier Hackintosh guides. Check this page together with the live [Dortania Tahoe guide](https://dortania.github.io/OpenCore-Install-Guide/extras/tahoe.html) before planning an installation.

## 1. Check the supported SMBIOS list

Dortania's Tahoe page lists a limited set of SMBIOS models. Read the current list and related notes before choosing PlatformInfo. A familiar model name is not automatically appropriate for a particular CPU or GPU.

Do not copy another computer's PlatformInfo values.

## 2. Analog audio requires special attention

Dortania documents that AppleHDA.kext is no longer present in macOS 26. As a result, the usual AppleALC path for analog audio through common onboard codecs does not work in the same way. Digital audio through HDMI, DisplayPort, or a USB audio device is a separate path.

Do not promise that built-in speakers or headphone jacks will work simply because the audio codec worked on an earlier macOS release. Check the current Tahoe guidance and understand any security trade-offs before considering alternative approaches.

## 3. Intel Bluetooth

The Tahoe notes document the -ibtcompatbeta boot argument for Intel Bluetooth. Check the current Intel Bluetooth project instructions and the exact controller generation before applying it.

## 4. Broadcom wireless

Broadcom wireless support has changed across recent macOS releases. Dortania currently points to AppleBCMWLANCompanion for some macOS 15 and 26 cases and notes limitations. Check the current project status, hardware compatibility, and firmware requirements. Do not assume every Broadcom card is supported.

## 5. OTA updates

Dortania documents additional requirements for OTA updates on macOS 14.4 and later, including RestrictEvents and configuration choices. These settings are version-sensitive. Follow the live page and the matching Kext documentation rather than copying a generic boot-argument list.

## 6. WhateverGreen and AMD graphics

The Tahoe page documents an AMD connector-patching issue involving WhateverGreen and notes that some kernel-panic cases may require a different approach. This is a version-specific caveat, not a universal instruction to remove WhateverGreen. Follow the exact current troubleshooting notes for the GPU.

## 7. Validate the whole system

Before choosing Tahoe, verify CPU, GPU acceleration, Wi-Fi, Bluetooth, audio, storage, and firmware support as a complete platform. If one critical feature is unsupported, consider another macOS target rather than assuming a later patch will appear.

## Version reminder

This page is a summary, not a replacement for live documentation. Tahoe-specific details can change as OpenCore, Kexts, and project guidance are updated.
