import plistlib
import sys
import tempfile
import unittest
import zipfile
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from simple_hackintosh import (
    Finding,
    _pci_ids,
    _safe_archive_name,
    build_support_report,
    check_config_file,
    check_efi_path,
    hardware_report_markdown,
    validate_config_data,
)


def valid_config():
    return {section: {} for section in (
        "ACPI", "Booter", "DeviceProperties", "Kernel",
        "Misc", "NVRAM", "PlatformInfo", "UEFI",
    )}


class ConfigValidationTests(unittest.TestCase):
    def test_accepts_minimal_main_sections(self):
        findings = validate_config_data(valid_config())
        self.assertEqual([item.level for item in findings], ["PASS"])

    def test_rejects_missing_section(self):
        config = valid_config()
        del config["UEFI"]
        findings = validate_config_data(config)
        self.assertTrue(any("Missing top-level section: UEFI" in item.message for item in findings))

    def test_rejects_wrong_container_type(self):
        config = valid_config()
        config["Kernel"] = []
        findings = validate_config_data(config)
        self.assertTrue(any("Kernel must be a dictionary" in item.message for item in findings))

    def test_binary_plist_is_supported(self):
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "config.plist"
            path.write_bytes(plistlib.dumps(valid_config(), fmt=plistlib.FMT_BINARY))
            findings, summary = check_config_file(path)
            self.assertIsNotNone(summary)
            self.assertTrue(any(item.level == "PASS" for item in findings))

    def test_enabled_entry_requires_path(self):
        config = valid_config()
        config["ACPI"]["Add"] = [{"Enabled": True}]
        from simple_hackintosh import _check_config_rows
        findings = _check_config_rows(config)
        self.assertTrue(any(item.code == "ENTRY_PATH" for item in findings))

    def test_duplicate_kext_identifier_is_warned(self):
        config = valid_config()
        config["Kernel"]["Add"] = [
            {"Enabled": True, "BundlePath": "A.kext", "BundleIdentifier": "org.example.same"},
            {"Enabled": True, "BundlePath": "B.kext", "BundleIdentifier": "org.example.same"},
        ]
        from simple_hackintosh import _check_config_rows
        findings = _check_config_rows(config)
        self.assertTrue(any(item.code == "DUPLICATE_BUNDLE_ID" for item in findings))


class ArchivePathTests(unittest.TestCase):
    def test_rejects_path_traversal(self):
        self.assertIsNone(_safe_archive_name("../outside.txt"))
        self.assertIsNone(_safe_archive_name("EFI/../../outside.txt"))

    def test_normalizes_windows_separators(self):
        self.assertEqual(_safe_archive_name(r"EFI\OC\config.plist"), "EFI/OC/config.plist")


class EfiCheckTests(unittest.TestCase):
    def _create_efi(self, root: Path):
        (root / "BOOT").mkdir(parents=True)
        for folder in ("ACPI", "Drivers", "Kexts", "Tools"):
            (root / "OC" / folder).mkdir(parents=True)
        (root / "BOOT" / "BOOTx64.efi").write_bytes(b"test")
        (root / "OC" / "OpenCore.efi").write_bytes(b"test")
        config = valid_config()
        config["ACPI"]["Add"] = [{"Enabled": True, "Path": "SSDT-Test.aml"}]
        config["Kernel"]["Add"] = [{"Enabled": True, "BundlePath": "Test.kext", "BundleIdentifier": "test.kext"}]
        config["UEFI"]["Drivers"] = [{"Enabled": True, "Path": "OpenRuntime.efi"}]
        config["Misc"]["Tools"] = [{"Enabled": True, "Path": "OpenShell.efi"}]
        (root / "OC" / "ACPI" / "SSDT-Test.aml").write_bytes(b"test")
        kext_contents = root / "OC" / "Kexts" / "Test.kext" / "Contents"
        kext_contents.mkdir(parents=True)
        (kext_contents / "Info.plist").write_bytes(
            plistlib.dumps({"CFBundleIdentifier": "test.kext", "CFBundleVersion": "1.0"})
        )
        (root / "OC" / "Drivers" / "OpenRuntime.efi").write_bytes(b"test")
        (root / "OC" / "Tools" / "OpenShell.efi").write_bytes(b"test")
        (root / "OC" / "config.plist").write_bytes(plistlib.dumps(config))

    def test_valid_efi_directory_and_references(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp) / "EFI"
            root.mkdir()
            self._create_efi(root)
            findings, summary = check_efi_path(root)
            self.assertIsNotNone(summary)
            self.assertFalse([item for item in findings if item.level in ("WARN", "ERROR")])

    def test_efi_zip_is_checked_without_extraction(self):
        with tempfile.TemporaryDirectory() as temp:
            base = Path(temp)
            root = base / "EFI"
            root.mkdir()
            self._create_efi(root)
            archive_path = base / "EFI.zip"
            with zipfile.ZipFile(archive_path, "w") as archive:
                for item in root.rglob("*"):
                    if item.is_file():
                        archive.write(item, item.relative_to(base).as_posix())
            findings, summary = check_efi_path(archive_path)
            self.assertIsNotNone(summary)
            self.assertFalse([item for item in findings if item.level in ("WARN", "ERROR")])

    def test_missing_required_boot_file_is_reported(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp) / "EFI"
            root.mkdir()
            self._create_efi(root)
            (root / "BOOT" / "BOOTx64.efi").unlink()
            findings, _ = check_efi_path(root)
            self.assertTrue(any(item.code == "REQUIRED_FILE_MISSING" for item in findings))

    def test_efi_folder_inside_wrapper_directory(self):
        with tempfile.TemporaryDirectory() as temp:
            parent = Path(temp) / "download"
            root = parent / "EFI"
            root.mkdir(parents=True)
            self._create_efi(root)
            findings, summary = check_efi_path(parent)
            self.assertIsNotNone(summary)
            self.assertFalse([item for item in findings if item.level in ("WARN", "ERROR")])


class ReportTests(unittest.TestCase):
    def test_pci_ids_are_extracted(self):
        ids = _pci_ids([r"PCI\VEN_8086&DEV_9B41&SUBSYS_507C17AA", "USB\\VID_1234"])
        self.assertEqual(ids, ["VEN_8086&DEV_9B41"])

    def test_hardware_report_includes_device_table(self):
        report = hardware_report_markdown({
            "ComputerModel": "Test PC",
            "CPU": "Test CPU",
            "Devices": [{
                "Name": "Intel Network Controller",
                "Class": "Net",
                "Status": "OK",
                "HardwareIDs": [r"PCI\VEN_8086&DEV_1234"],
            }],
        })
        self.assertIn("PCI and device inventory", report)
        self.assertIn("VEN_8086&DEV_1234", report)

    def test_support_report_omits_raw_config_values(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp) / "EFI"
            root.mkdir()
            self._create_support_efi(root)
            report = build_support_report(str(root), include_hardware=False)
            rendered = str(report)
            self.assertNotIn("A-SECRET-SERIAL", rendered)
            self.assertIn("No serial numbers", report["privacy"])

    @staticmethod
    def _create_support_efi(root: Path):
        (root / "BOOT").mkdir()
        (root / "OC").mkdir()
        for name in ("BOOT/BOOTx64.efi", "OC/OpenCore.efi"):
            target = root / name
            target.write_bytes(b"test")
        config = valid_config()
        config["PlatformInfo"] = {"Generic": {"SystemSerialNumber": "A-SECRET-SERIAL"}}
        (root / "OC" / "config.plist").write_bytes(plistlib.dumps(config))


if __name__ == "__main__":
    unittest.main()
