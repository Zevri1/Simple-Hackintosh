# 10 — USB mapping

USB mapping describes which physical USB ports belong to which controller ports and how they are exposed to macOS. A correct map improves reliability and can affect sleep and device behavior.

## Why mapping matters

A computer can expose more USB ports than macOS allows a controller to use. Front-panel ports, internal Bluetooth connections, USB-C ports, and different connector speeds may be represented separately.

A temporary boot workaround is not a substitute for a correct map.

## Recommended workflow

1. Read the current [Dortania USB mapping guide](https://dortania.github.io/OpenCore-Post-Install/usb/).
2. Identify the USB controllers and their physical ports.
3. Use the currently maintained mapping method recommended by the guide.
4. Connect and test devices systematically, including USB 2 and USB 3 behavior where applicable.
5. Identify internal devices separately from external ports.
6. Build a map that reflects the actual physical layout.
7. Install and configure the result according to the tool's current instructions.
8. Test ports, sleep, wake, and restart after applying the map.

## Important version caveat

Dortania documents that XhciPortLimit is broken on macOS 11.3 and later and can cause boot loops. Do not rely on it as a permanent solution. Read the current USB guide before applying or changing any related quirk.

## Document the result

Keep a simple table containing the controller, physical port location, connector type, speed, and whether it is internal or external. Do not publish unrelated device identifiers or private logs.

## Troubleshooting

If a port stops working, check the map, connector type, controller, and the exact macOS/OpenCore version. Change one mapping element at a time and keep a backup EFI.

## Next chapter

Review [11 — macOS Tahoe 26](11-macos-tahoe-26.md) if your target is macOS 26.
