# 01 — Hardware inventory

Compatibility research begins with exact device identifiers, not a marketing name such as a laptop series or motherboard family.

## 1. Record the system model and firmware

1. In Windows, open **System Information** by running **msinfo32**.
2. Record System Manufacturer, System Model, BIOS Version/Date, BIOS Mode, and Secure Boot State.
3. Record whether the system boots in UEFI mode. OpenCore's standard PC workflow assumes UEFI firmware.
4. Record the motherboard or system-board model when available.

Do not publish serial numbers or asset tags from System Information.

## 2. Identify the CPU

1. Open Task Manager → Performance → CPU, or System Information.
2. Record the full processor model, not just the brand or family.
3. Record the number of cores and whether the machine uses Intel or AMD.
4. Research that exact generation and architecture in the current guide.

The CPU name alone cannot establish GPU, power-management, or macOS support.

## 3. Identify every graphics device

1. Open Device Manager → Display adapters.
2. Record every listed GPU and its exact model.
3. For more detail, open the device's Properties → Details → Hardware Ids.
4. Save the PCI vendor and device identifiers, plus subsystem information when available.
5. Check whether the computer uses switchable graphics or whether the display is wired to a particular GPU.

A display working in Windows does not prove that its GPU has macOS acceleration.

## 4. Identify network, audio, and storage devices

In Device Manager, inspect:

- Network adapters: exact Wi-Fi, Bluetooth, and Ethernet model.
- Sound, video and game controllers: audio codec or controller where identifiable.
- Storage controllers and Disk drives: SATA/NVMe controller and exact SSD model.
- Universal Serial Bus controllers: controller family, especially on desktop platforms.

For an unknown device, use its Hardware Ids field. A name such as “Network Controller” is not enough to select a driver.

## 5. Create a hardware report

Copy the blank [hardware report template](../templates/hardware-report.md). Fill in exact model names and IDs where available. Write “unknown” rather than guessing.

Keep this report private until you remove serial numbers, MAC addresses, usernames, and other personal information.

## 6. Check the inventory twice

Compare the report with the vendor specification page and Device Manager. Some computers contain multiple graphics or network devices, and a model can ship with different components in different regions.

## Next chapter

Use the report in [02 — Compatibility check](02-compatibility.md).
