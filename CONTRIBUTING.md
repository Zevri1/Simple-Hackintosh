# Contributing

Contributions that improve accuracy, clarity, and safety are welcome.

## Before opening a pull request

1. Check the current official OpenCore and macOS documentation.
2. State the macOS version, OpenCore release, and hardware scope affected by the change.
3. Do not present a hardware-specific setting as universal.
4. Include the reason for a recommended Kext, SSDT, driver, or quirk.
5. Test commands in the environment they describe, or clearly label them as examples that need verification.
6. Keep private identifiers out of examples, logs, screenshots, and EFI files.
7. Do not include macOS installer images, Apple firmware, or third-party binaries without explicit redistribution permission.

## Documentation style

- Use numbered steps for procedures.
- Put destructive actions behind a clear warning.
- State expected results after important steps.
- Link to primary documentation where possible.
- Record the date when version-sensitive facts were checked.
- Explain platform-specific limitations.

## Pull request checklist

- [ ] Links checked
- [ ] Version and hardware scope stated
- [ ] No private identifiers included
- [ ] No unlicensed binaries included
- [ ] Instructions distinguish tested steps from general guidance
