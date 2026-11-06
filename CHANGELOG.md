# Changelog

All notable documentation and CLI changes are recorded here.

## 2026-11-06 — CLI v2.5.2

- Fixed the official Windows Hardware Sniffer crash caused by Unicode progress characters being printed through a CP1252 stream; the child process now uses UTF-8 with replacement fallback.
- Added a Windows CI smoke test that runs the official upstream executable and validates its generated Report.json.
- Fixed the Tools page so it shares the saved light/dark preference, supports keyboard-operable tabs, and uses more reliable mobile navigation and download behavior.
- Added safer ZIP path normalization and delayed object-URL revocation for report downloads.


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
