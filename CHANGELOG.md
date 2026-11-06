# Changelog

## Unreleased

## 2026-11-06 — CLI v3.0.0
- Added a human-readable Windows Hardware Sniffer report with a conservative, source-linked macOS compatibility estimate. The original OpCore-Simplify `Report.json` remains unchanged.
- Consolidated the cross-platform hardware inventory, EFI validation, Kext/path checks, OpenCore version-matched `ocvalidate`, EFI starter creation, safe backups, SHA-256 comparison, and privacy-conscious support reporting into the v3 CLI release.

All notable documentation and CLI changes are recorded here.

## 2026-11-06 — CLI v2.5.2

- Fixed the Windows Hardware Sniffer `UnicodeEncodeError` by keeping stdout attached to the real console instead of redirecting it through a legacy-codepage pipe.
- Kept stderr capture, UTF-8-safe diagnostic decoding, automated report export, and JSON validation.
- Fixed the home-page document title and learning-path navigation state; improved responsive Tools navigation and keyboard-operable tabs.
- Added DOM-hook and responsive-site contract checks.


## 2026-11-06 — CLI v2.5.1

- Fixed the Hardware Sniffer launch gate: the upstream Windows release is not Authenticode-signed, so a missing signature is now accepted only after the official GitHub SHA-256 digest matches.
- Kept rejection of invalid Authenticode signatures and retained the automated report export, JSON validation, error diagnostics, and previous-report preservation.
- Updated the website and release links to the v2.5.1 hotfix.


## 2026-11-06 — CLI v2.5.0

- Automated the official OpCore-Simplify Hardware Sniffer report-export menu instead of leaving a second interactive prompt open.
- Validated the generated JSON and surfaced the helper's exit code and captured diagnostics when no report is produced.
- Preserved a report from a previous run with a timestamped filename before exporting a replacement.
- Rebuilt the website visual system and added direct CLI release links to the home page and Tools page.
- Added static website contract checks to the Pages workflow.


## 2026-11-05

- Replaced the incomplete repository introduction with a working chapter index.
- Added the missing numbered documentation chapters and report templates.
- Added an illustrated walkthrough with source attribution.
- Added clearer hardware-compatibility, privacy, backup, and legal guidance.
- Added macOS 26 / Tahoe caveats and stronger OpenCore version-matching guidance.
- Fixed links that previously pointed to files not present in the repository.

## Earlier starter-pack notes

- Established the Simple Hackintosh documentation project.
- Added contribution, support, source, and publishing notes.
- Added repository Social Preview artwork.
