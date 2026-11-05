# 09 — Post-installation checks

A successful first boot is only the beginning. Test core functions before treating the installation as stable.

## 1. Confirm basic system information

- Confirm the intended macOS version and build.
- Confirm that OpenCore loads from the expected EFI.
- Check that the internal storage is detected consistently.
- Confirm the correct display resolution and refresh rate where available.

## 2. Test graphics

Check that the intended GPU is detected and that graphics acceleration is active. A visible desktop or a recognized model name does not necessarily prove acceleration.

Use the current GPU guide and project documentation. Avoid copying device properties from another model.

## 3. Test network and Bluetooth

Test Wi-Fi, Ethernet, Bluetooth discovery, and a normal reconnect after restart. Wi-Fi and Bluetooth components on the same physical card may use different chipsets and different drivers.

## 4. Test audio

Test speakers, headphones, microphone, and any external digital-audio output separately. A supported codec does not guarantee every audio port works.

For macOS 26, read the dedicated [Tahoe notes](11-macos-tahoe-26.md), because analog audio behavior differs from earlier releases.

## 5. Test USB and sleep behavior

Test each physical USB port with a low-risk device, then test sleep, wake, restart, and shutdown. Do not rely on a temporary USB-port-limit workaround as a permanent mapping solution.

See [10 — USB mapping](10-usb-mapping.md).

## 6. Keep the EFI backup

Once a stable configuration is confirmed, save a separate copy of the entire EFI. Record the OpenCore release, Kext versions, SMBIOS model choice, and any hardware-specific changes. Keep private identifiers private.

## 7. Delay optional security or update changes

Do not enable FileVault, change Secure Boot settings, apply major OS updates, or remove recovery media until you understand the current OpenCore support and have a tested recovery plan.

## Completion checklist

- [ ] Stable cold boot and restart
- [ ] Correct graphics acceleration
- [ ] Network and Bluetooth tested
- [ ] Audio input and output tested
- [ ] USB ports tested
- [ ] Sleep and wake tested where supported
- [ ] Backup EFI stored separately
- [ ] Private identifiers removed from any shared report

## Next chapter

Read [10 — USB mapping](10-usb-mapping.md) for a reliable long-term USB configuration.
