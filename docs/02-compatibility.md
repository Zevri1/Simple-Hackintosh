# 02 — Compatibility check

Check the complete computer against the target macOS version before downloading tools or erasing a USB drive.

## Compatibility matrix

| Component | What to verify | Why it matters |
|---|---|---|
| CPU | Exact model, generation, instruction-set support, and platform-specific guidance | Determines whether the kernel and required patches are suitable |
| Integrated GPU | Exact graphics generation and macOS support | The integrated GPU may provide display output and acceleration |
| Discrete GPU | Exact device ID, architecture, and macOS driver support | Unsupported GPUs may have no acceleration or no usable output |
| Wi-Fi | Exact chipset and current driver support | Support varies significantly by chipset and macOS version |
| Bluetooth | Exact controller and firmware support | Bluetooth can have different support from Wi-Fi even on one card |
| Ethernet | Exact controller and Kext support | Wired networking may be easier to support, but is not guaranteed |
| Audio | Exact codec/controller and supported layout or alternative | A working codec does not guarantee every jack or built-in speaker |
| Storage | NVMe/SATA controller, SSD model, and firmware behavior | Storage incompatibility can prevent installation or stable boot |
| Firmware | UEFI behavior, ACPI tables, and required settings | OpenCore depends on firmware and ACPI behavior |

## 1. Use primary sources

- Start with the [Dortania OpenCore Install Guide](https://dortania.github.io/OpenCore-Install-Guide/).
- Check the [GPU Buyers Guide](https://dortania.github.io/GPU-Buyers-Guide/) for graphics.
- Check the [Wireless Buyers Guide](https://dortania.github.io/Wireless-Buyers-Guide/) for Wi-Fi and Bluetooth.
- Check the relevant vendor and project documentation for Ethernet, audio, and storage controllers.

Community success reports can help identify possible issues, but verify that they match the exact hardware and macOS version.

## 2. Separate “boots” from “works”

A successful boot is only one compatibility test. A usable setup may also need:

- graphics acceleration and correct display output;
- Wi-Fi, Bluetooth, and Ethernet;
- speakers, microphone, and headphone output;
- USB ports and sleep/wake behavior;
- battery, brightness, keyboard, trackpad, and power management where applicable;
- stable restart and shutdown behavior.

Record each feature as **confirmed**, **unconfirmed**, or **unsupported**. Do not mark it working based only on another model's report.

## 3. Choose the macOS version

Choose a release only after checking all critical components. Newer macOS releases may remove older drivers or change behavior. A computer that worked with one release may not work with a later one.

## 4. Make a go/no-go decision

Proceed only when the major components have a documented path and you understand the remaining limitations. If the primary GPU is unsupported or a critical storage/network device has no viable support, reconsider the macOS target before continuing.

## Next chapter

When the hardware review is complete, prepare the installer using [03 — Windows USB installer](03-usb-installer-windows.md).
