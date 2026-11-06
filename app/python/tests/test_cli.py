import plistlib
import sys
import tempfile
import unittest
import zipfile
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from simple_hackintosh import _safe_archive_name, check_efi_path, validate_config_data


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
        config["Kernel"]["Add"] = [{"Enabled": True, "BundlePath": "Test.kext"}]
        config["UEFI"]["Drivers"] = [{"Enabled": True, "Path": "OpenRuntime.efi"}]
        config["Misc"]["Tools"] = [{"Enabled": True, "Path": "OpenShell.efi"}]
        (root / "OC" / "ACPI" / "SSDT-Test.aml").write_bytes(b"test")
        (root / "OC" / "Kexts" / "Test.kext").mkdir()
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
            self.assertTrue(any("BOOT/BOOTx64.efi" in item.message and item.level == "ERROR" for item in findings))


if __name__ == "__main__":
    unittest.main()
