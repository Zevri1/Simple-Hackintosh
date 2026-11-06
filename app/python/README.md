# Simple Hackintosh CLI

A real Python command-line program for Windows Command Prompt (CMD) and PowerShell. It runs locally; it is not a web page and does not require a browser, login, internet connection, or third-party Python packages.

## Requirements

- Windows 10 or 11 for the hardware inventory command.
- Python 3.10 or newer.
- Windows PowerShell (included with Windows).
- EFI and plist checks also run on other platforms with Python 3.10+.

## Start in CMD

1. Download the repository and extract the ZIP.
2. Open the \`app\\python\` folder in Command Prompt.
3. Run:

\`\`\`cmd
py -3 simple_hackintosh.py --help
py -3 simple_hackintosh.py doctor
\`\`\`

You can also use the included \`run.bat\` launcher:

\`\`\`cmd
run.bat --help
run.bat doctor
run.bat hardware
\`\`\`

## Commands

### 1. Hardware inventory

\`\`\`cmd
py -3 simple_hackintosh.py hardware
py -3 simple_hackintosh.py hardware --output hardware-report.md
\`\`\`

Reads CPU, GPU names, motherboard, BIOS version, Windows version, installed memory, disk models/capacities, and physical network-adapter names through Windows CIM/PowerShell. It deliberately does not collect serial numbers, UUIDs, MAC addresses, passwords, or account identifiers.

### 2. EFI folder or ZIP check

\`\`\`cmd
py -3 simple_hackintosh.py efi-check "E:\\EFI"
py -3 simple_hackintosh.py efi-check "C:\\Users\\YourName\\Downloads\\EFI.zip"
\`\`\`

Checks the basic OpenCore layout, parses the embedded config.plist, and checks enabled ACPI, Kext, UEFI driver, and tool references. ZIP files are inspected without extracting them. The checker rejects unsafe ZIP paths and does not modify the selected files.

### 3. config.plist check

\`\`\`cmd
py -3 simple_hackintosh.py config-check "E:\\EFI\\OC\\config.plist"
\`\`\`

Supports XML and binary property lists. Checks the main OpenCore sections and selected basic value types. This is not the official \`ocvalidate\` tool and cannot certify that an EFI will boot.

## Exit codes

- \`0\`: checks completed without reported errors/warnings.
- \`1\`: a validation warning or error was found.
- \`2\`: invalid input, unreadable file, or runtime/environment error.

## Privacy and safety

All checks run on the computer where the command is launched. The program does not upload selected EFI files or hardware data. It is read-only: it does not format disks, change partitions, edit EFI files, write boot entries, flash firmware, or change BIOS settings.

Hardware inventory is informational only. It does not establish macOS compatibility. Review the exact CPU, GPU, wireless chipset, storage controller, firmware, macOS version, and current upstream OpenCore documentation before making changes.

## Tests

From this directory:

\`\`\`cmd
py -3 -m unittest discover -s tests -v
\`\`\`

The test suite uses Python's standard library and temporary sample EFI files; it does not access or alter a real EFI partition.
