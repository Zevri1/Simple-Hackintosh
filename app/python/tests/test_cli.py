import hashlib
import io
import json
import plistlib
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch
import zipfile
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from simple_hackintosh import (
    Finding,
    _pci_ids,
    _safe_archive_name,
    build_support_report,
    command_backup_efi,
    command_create_efi,
    command_macrecovery,
    command_hardware_sniffer,
    _download_hardware_sniffer,
    _verify_hardware_sniffer_signature,
    build_parser,
    check_config_file,
    check_efi_path,
    hardware_report_markdown,
    estimate_macos_compatibility,
    parse_macos_profiler_data,
    render_menu,
    _file_digests,
    validate_config_data,
)


def valid_config():
    return {section: {} for section in (
        "ACPI", "Booter", "DeviceProperties", "Kernel",
        "Misc", "NVRAM", "PlatformInfo", "UEFI",
    )}



class MacRecoveryIntegrationTests(unittest.TestCase):
    def test_cli_parser_passes_download_options_through(self):
        parsed = build_parser().parse_args([
            "macrecovery", "download", "--os-type", "latest",
            "--outdir", "recovery-output",
        ])
        self.assertEqual(
            parsed.arguments,
            ["download", "--os-type", "latest", "--outdir", "recovery-output"],
        )

    def test_command_forwards_arguments_to_bundled_upstream_script(self):
        with patch("simple_hackintosh.subprocess.run") as mocked_run:
            mocked_run.return_value.returncode = 7
            result = command_macrecovery(["download", "--os-type", "latest"])

        self.assertEqual(result, 7)
        command = mocked_run.call_args.args[0]
        self.assertEqual(command[0], sys.executable)
        self.assertTrue(command[1].endswith(str(Path("macrecovery") / "macrecovery.py")))
        self.assertEqual(command[2:], ["download", "--os-type", "latest"])

    def test_upstream_macrecovery_help_runs_without_network(self):
        script = Path(__file__).resolve().parents[1] / "macrecovery" / "macrecovery.py"
        result = subprocess.run(
            [sys.executable, str(script), "-h"],
            capture_output=True,
            text=True,
            check=False,
            timeout=20,
        )
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("Gather recovery information for Macs", result.stdout)



class CompatibilityEstimateTests(unittest.TestCase):
    def test_comet_lake_uhd_graphics_gets_conservative_sequoia_estimate(self):
        data = {
            "CPU": "Intel(R) Core(TM) i5-10210U CPU @ 1.60GHz",
            "GPUs": [{"Name": "Intel(R) UHD Graphics", "DriverVersion": "test"}],
            "Devices": [{
                "Name": "Intel(R) UHD Graphics",
                "Class": "Display",
                "HardwareIDs": [r"PCI\VEN_8086&DEV_9B41&SUBSYS_507C17AA"],
            }],
        }
        result = estimate_macos_compatibility(data)
        self.assertEqual(result["status"], "documented-baseline")
        self.assertIn("Sequoia 15", result["target"])
        self.assertIn("Tahoe 26", result["experimental_target"])
        self.assertEqual(result["cpu_generation"], 10)
        self.assertIn("Comet Lake", result["gpu_family"])

    def test_intel_uhd_610_is_flagged_as_limited(self):
        result = estimate_macos_compatibility({
            "CPU": "Intel Core i5-10200H",
            "GPUs": [{"Name": "Intel UHD Graphics 610"}],
            "Devices": [],
        })
        self.assertEqual(result["status"], "limited")
        self.assertIn("No reliable", result["target"])

    def test_intel_xe_is_not_given_a_false_compatibility_ceiling(self):
        result = estimate_macos_compatibility({
            "CPU": "Intel Core i7-1165G7",
            "GPUs": [{"Name": "Intel Iris Xe Graphics"}],
            "Devices": [],
        })
        self.assertEqual(result["status"], "limited")
        self.assertIsNone(result["experimental_target"])

    def test_unrecognised_gpu_returns_unknown_instead_of_guessing(self):
        result = estimate_macos_compatibility({
            "CPU": "Intel Core i5-10210U",
            "GPUs": [{"Name": "Microsoft Basic Display Adapter"}],
            "Devices": [],
        })
        self.assertEqual(result["status"], "unknown")
        self.assertIn("Insufficient", result["target"])

    def test_hardware_report_includes_compatibility_estimate_and_sources(self):
        report = hardware_report_markdown({
            "CPU": "Intel Core i5-10210U",
            "GPUs": [{"Name": "Intel UHD Graphics"}],
            "Devices": [{
                "Name": "Intel UHD Graphics",
                "Class": "Display",
                "HardwareIDs": [r"PCI\VEN_8086&DEV_9B41"],
            }],
        })
        self.assertIn("macOS compatibility estimate", report)
        self.assertIn("Sequoia 15", report)
        self.assertIn("GPU Buyers Guide", report)


class MenuRenderingTests(unittest.TestCase):
    def test_menu_includes_all_options_and_sections_without_ansi(self):
        menu = render_menu(use_color=False)
        self.assertIn("SIMPLE HACKINTOSH", menu)
        self.assertIn("HARDWARE & DIAGNOSTICS", menu)
        self.assertIn("EFI WORKBENCH", menu)
        self.assertIn("RECOVERY & SYSTEM", menu)
        for option in ("01", "02", "03", "04", "05", "06", "07", "08", "09", "10", "00"):
            self.assertIn(option, menu)
        self.assertNotIn("\033[", menu)

    def test_menu_can_render_ansi_colours(self):
        menu = render_menu(use_color=True)
        self.assertIn("\033[", menu)
        self.assertIn("Download Recovery", menu)




class HardwareSnifferIntegrationTests(unittest.TestCase):

    def test_hardware_sniffer_download_verifies_github_sha256(self):
        with tempfile.TemporaryDirectory() as temp:
            payload = b"signed-test-executable-placeholder"
            metadata = {
                "tag_name": "2.1.2-test",
                "draft": False,
                "prerelease": False,
                "assets": [{
                    "name": "Hardware-Sniffer.exe",
                    "digest": "sha256:" + hashlib.sha256(payload).hexdigest(),
                    "size": len(payload),
                    "browser_download_url": (
                        "https://github.com/lzhoang2801/Hardware-Sniffer/releases/download/"
                        "2.1.2-test/Hardware-Sniffer.exe"
                    ),
                }],
            }
            responses = [
                io.BytesIO(json.dumps(metadata).encode("utf-8")),
                io.BytesIO(payload),
            ]
            with (
                patch.dict("os.environ", {"LOCALAPPDATA": temp}),
                patch("simple_hackintosh.urllib.request.urlopen", side_effect=responses),
            ):
                executable, release_tag = _download_hardware_sniffer()

            self.assertEqual(release_tag, "2.1.2-test")
            self.assertEqual(executable.read_bytes(), payload)

    def test_hardware_sniffer_rejects_a_download_with_wrong_digest(self):
        with tempfile.TemporaryDirectory() as temp:
            payload = b"tampered-executable"
            metadata = {
                "tag_name": "2.1.2-test",
                "draft": False,
                "prerelease": False,
                "assets": [{
                    "name": "Hardware-Sniffer.exe",
                    "digest": "sha256:" + ("0" * 64),
                    "size": len(payload),
                    "browser_download_url": (
                        "https://github.com/lzhoang2801/Hardware-Sniffer/releases/download/"
                        "2.1.2-test/Hardware-Sniffer.exe"
                    ),
                }],
            }
            responses = [
                io.BytesIO(json.dumps(metadata).encode("utf-8")),
                io.BytesIO(payload),
            ]
            with (
                patch.dict("os.environ", {"LOCALAPPDATA": temp}),
                patch("simple_hackintosh.urllib.request.urlopen", side_effect=responses),
                self.assertRaisesRegex(RuntimeError, "SHA-256 verification failed"),
            ):
                _download_hardware_sniffer()

    def test_hardware_sniffer_cli_parser_accepts_output_directory(self):
        parsed = build_parser().parse_args([
            "hardware-sniffer", "--output-dir", "hardware-results",
        ])
        self.assertEqual(parsed.command, "hardware-sniffer")
        self.assertEqual(parsed.output_dir, "hardware-results")

    def test_hardware_sniffer_accepts_unsigned_official_release_after_sha256_check(self):
        with tempfile.TemporaryDirectory() as temp:
            executable = Path(temp) / "Hardware-Sniffer.exe"
            executable.write_bytes(b"official-release-payload")
            completed = subprocess.CompletedProcess(
                ["powershell"], 0, stdout="NOT_SIGNED\n", stderr=""
            )
            with (
                patch("simple_hackintosh._powershell_executable", return_value="powershell"),
                patch("simple_hackintosh.subprocess.run", return_value=completed) as run,
            ):
                result = _verify_hardware_sniffer_signature(executable)
            self.assertIn("GitHub SHA-256 digest matched", result)
            self.assertIn("Get-AuthenticodeSignature", run.call_args.args[0][-1])

    def test_hardware_sniffer_accepts_a_valid_authenticode_signature(self):
        with tempfile.TemporaryDirectory() as temp:
            executable = Path(temp) / "Hardware-Sniffer.exe"
            executable.write_bytes(b"signed-release-payload")
            completed = subprocess.CompletedProcess(
                ["powershell"], 0, stdout="SIGNED:CN=Example Publisher\n", stderr=""
            )
            with (
                patch("simple_hackintosh._powershell_executable", return_value="powershell"),
                patch("simple_hackintosh.subprocess.run", return_value=completed),
            ):
                result = _verify_hardware_sniffer_signature(executable)
            self.assertEqual(result, "CN=Example Publisher")

    def test_hardware_sniffer_rejects_an_invalid_authenticode_signature(self):
        with tempfile.TemporaryDirectory() as temp:
            executable = Path(temp) / "Hardware-Sniffer.exe"
            executable.write_bytes(b"tampered-release-payload")
            completed = subprocess.CompletedProcess(
                ["powershell"], 1, stdout="", stderr="Authenticode status: HashMismatch"
            )
            with (
                patch("simple_hackintosh._powershell_executable", return_value="powershell"),
                patch("simple_hackintosh.subprocess.run", return_value=completed),
                self.assertRaisesRegex(RuntimeError, "invalid Authenticode signature"),
            ):
                _verify_hardware_sniffer_signature(executable)

    def test_hardware_sniffer_command_is_windows_only(self):
        with patch("simple_hackintosh.platform.system", return_value="Darwin"):
            with self.assertRaisesRegex(RuntimeError, "does not support macOS"):
                command_hardware_sniffer()

    def test_hardware_sniffer_automatically_exports_report_without_acpi_dump(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            executable = root / "Hardware-Sniffer.exe"
            executable.write_bytes(b"test executable")
            captured = {}

            def fake_run(command, **kwargs):
                captured.update(kwargs)
                report_dir = Path(kwargs["cwd"]) / "SysReport"
                report_dir.mkdir(parents=True)
                (report_dir / "Report.json").write_text(
                    '{"Motherboard": {}, "BIOS": {}, "CPU": {}, "GPU": {}}',
                    encoding="utf-8",
                )
                return subprocess.CompletedProcess(command, 0, stdout="", stderr="")

            with (
                patch("simple_hackintosh.platform.system", return_value="Windows"),
                patch("simple_hackintosh._download_hardware_sniffer", return_value=(executable, "test-release")),
                patch("simple_hackintosh._verify_hardware_sniffer_signature", return_value="CN=Test Publisher"),
                patch("simple_hackintosh.subprocess.run", side_effect=fake_run),
            ):
                self.assertEqual(command_hardware_sniffer(str(root / "output")), 0)

            self.assertEqual(captured["input"], "H\n\nQ\n\n")
            self.assertIsNone(captured["stdout"], "stdout must inherit the console to preserve Windows UTF-8 console I/O")
            self.assertEqual(captured["stderr"], subprocess.PIPE)
            self.assertNotIn("capture_output", captured)
            self.assertTrue(captured["text"])
            self.assertEqual(captured["encoding"], "utf-8")
            self.assertEqual(captured["errors"], "replace")
            self.assertEqual(captured["timeout"], 180)
            self.assertTrue((root / "output" / "SysReport" / "Report.json").is_file())
            self.assertFalse((root / "output" / "SysReport" / "ACPI").exists())

    def test_hardware_sniffer_preserves_previous_report_and_creates_a_new_one(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            report = root / "SysReport" / "Report.json"
            report.parent.mkdir(parents=True)
            report.write_text('{"existing": true}', encoding="utf-8")
            executable = root / "Hardware-Sniffer.exe"
            executable.write_bytes(b"test executable")

            def fake_run(command, **kwargs):
                (Path(kwargs["cwd"]) / "SysReport" / "Report.json").write_text(
                    '{"CPU": {"Processor Name": "Test CPU"}}', encoding="utf-8"
                )
                return subprocess.CompletedProcess(command, 0, stdout="", stderr="")

            with (
                patch("simple_hackintosh.platform.system", return_value="Windows"),
                patch("simple_hackintosh._download_hardware_sniffer", return_value=(executable, "test-release")),
                patch("simple_hackintosh._verify_hardware_sniffer_signature", return_value="CN=Test Publisher"),
                patch("simple_hackintosh.subprocess.run", side_effect=fake_run),
            ):
                self.assertEqual(command_hardware_sniffer(str(root)), 0)

            self.assertIn('"Test CPU"', report.read_text(encoding="utf-8"))
            archived = list(report.parent.glob("Report.previous-*.json"))
            self.assertEqual(len(archived), 1)
            self.assertIn('"existing": true', archived[0].read_text(encoding="utf-8"))

    def test_hardware_sniffer_exit_code_two_without_report_has_diagnostic(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            executable = root / "Hardware-Sniffer.exe"
            executable.write_bytes(b"test executable")
            with (
                patch("simple_hackintosh.platform.system", return_value="Windows"),
                patch("simple_hackintosh._download_hardware_sniffer", return_value=(executable, "test-release")),
                patch("simple_hackintosh._verify_hardware_sniffer_signature", return_value="CN=Test Publisher"),
                patch(
                    "simple_hackintosh.subprocess.run",
                    return_value=subprocess.CompletedProcess(
                        [str(executable)], 2, stdout="collector stopped", stderr="bad device query"
                    ),
                ),
                self.assertRaisesRegex(RuntimeError, r"exited with code 2.*bad device query"),
            ):
                command_hardware_sniffer(str(root / "output"))


class MacHardwareInventoryTests(unittest.TestCase):
    def sample_profiler_data(self):
        return {
            "SPHardwareDataType": [{
                "_name": "hardware_overview",
                "machine_name": "MacBook Pro",
                "machine_model": "MacBookPro16,1",
                "chip_type": "Intel Core i7-9750H",
                "total_number_cores": "6",
                "physical_memory": "16 GB",
                "boot_rom_version": "1554.100.20.0.0",
                "serial_number": "PRIVATE-SERIAL-MUST-NOT-LEAK",
                "platform_UUID": "PRIVATE-UUID-MUST-NOT-LEAK",
            }],
            "SPDisplaysDataType": [{
                "_name": "Intel UHD Graphics 630",
                "sppci_model": "Intel UHD Graphics 630",
                "spdisplays_vram": "1536 MB",
            }],
            "SPPCIDataType": [{
                "_name": "Intel UHD Graphics 630",
                "spci_model": "Intel UHD Graphics 630",
                "spci_vendor-id": "0x8086",
                "spci_device-id": "0x3e9b",
            }],
            "SPStorageDataType": [{
                "physical_drive": "APPLE SSD AP0512",
                "physical_drive_size": "500 GB",
            }],
        }

    def test_parses_macos_hardware_and_display_data(self):
        data = parse_macos_profiler_data(self.sample_profiler_data(), "15.4", "x86_64")
        self.assertEqual(data["HostPlatform"], "Darwin")
        self.assertEqual(data["OS"], "15.4")
        self.assertEqual(data["ComputerModel"], "MacBook Pro (MacBookPro16,1)")
        self.assertEqual(data["CPU"], "Intel Core i7-9750H")
        self.assertEqual(data["CPUCores"], 6)
        self.assertEqual(data["MemoryGB"], 16.0)
        self.assertIn("Intel UHD Graphics 630", [gpu["Name"] for gpu in data["GPUs"]])
        self.assertTrue(any("VEN_8086&DEV_3E9B" in device["HardwareIDs"] for device in data["Devices"]))

    def test_macos_parser_does_not_retain_serial_or_hardware_uuid(self):
        data = parse_macos_profiler_data(self.sample_profiler_data(), "15.4", "x86_64")
        rendered = repr(data)
        self.assertNotIn("PRIVATE-SERIAL-MUST-NOT-LEAK", rendered)
        self.assertNotIn("PRIVATE-UUID-MUST-NOT-LEAK", rendered)

    def test_apple_silicon_report_confirms_running_version_not_maximum(self):
        data = parse_macos_profiler_data({
            "SPHardwareDataType": [{
                "machine_name": "MacBook Air",
                "machine_model": "MacBookAir10,1",
                "chip_type": "Apple M2",
                "total_number_cores": "8",
                "physical_memory": "16 GB",
            }],
            "SPDisplaysDataType": [{"_name": "Apple M2", "sppci_model": "Apple M2"}],
        }, "15.4", "arm64")
        result = estimate_macos_compatibility(data)
        self.assertEqual(result["status"], "native-mac")
        self.assertIn("15.4 is currently running", result["target"])
        self.assertIn("maximum supported version is not inferred", result["confidence"])

    def test_macos_hardware_report_shows_os_and_running_version_estimate(self):
        data = parse_macos_profiler_data(self.sample_profiler_data(), "15.4", "x86_64")
        report = hardware_report_markdown(data)
        self.assertIn("Operating system", report)
        self.assertIn("15.4 is currently running on this Mac", report)
        self.assertNotIn("PRIVATE-SERIAL-MUST-NOT-LEAK", report)


class CreateEfiTests(unittest.TestCase):
    def _make_release(self, archive_path: Path):
        with zipfile.ZipFile(archive_path, "w", compression=zipfile.ZIP_DEFLATED) as archive:
            archive.writestr("OpenCore/X64/EFI/BOOT/BOOTx64.efi", b"boot-loader")
            archive.writestr("OpenCore/X64/EFI/OC/OpenCore.efi", b"opencore")
            archive.writestr("OpenCore/X64/EFI/OC/Drivers/OpenRuntime.efi", b"runtime")
            archive.writestr(
                "OpenCore/Docs/Sample.plist",
                plistlib.dumps(valid_config(), fmt=plistlib.FMT_XML),
            )

    def test_create_efi_starter_from_release_zip(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            package = root / "OpenCore.zip"
            output = root / "EFI"
            self._make_release(package)

            result = command_create_efi(str(package), str(output))

            self.assertEqual(result, 0)
            self.assertEqual((output / "BOOT" / "BOOTx64.efi").read_bytes(), b"boot-loader")
            self.assertEqual((output / "OC" / "OpenCore.efi").read_bytes(), b"opencore")
            self.assertEqual((output / "OC" / "Drivers" / "OpenRuntime.efi").read_bytes(), b"runtime")
            self.assertTrue((output / "OC" / "ACPI").is_dir())
            self.assertTrue((output / "OC" / "Kexts").is_dir())
            self.assertTrue((output / "OC" / "Tools").is_dir())
            self.assertTrue((output / "SETUP_REQUIRED.txt").is_file())
            parsed = plistlib.loads((output / "OC" / "config.plist").read_bytes())
            self.assertIsInstance(parsed, dict)

    def test_create_efi_refuses_existing_output(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            package = root / "OpenCore.zip"
            output = root / "EFI"
            output.mkdir()
            marker = output / "keep.txt"
            marker.write_text("keep", encoding="utf-8")
            self._make_release(package)

            self.assertEqual(command_create_efi(str(package), str(output)), 2)
            self.assertEqual(marker.read_text(encoding="utf-8"), "keep")

    def test_create_efi_rejects_package_missing_core_files(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            package = root / "OpenCore.zip"
            output = root / "EFI"
            with zipfile.ZipFile(package, "w") as archive:
                archive.writestr("X64/EFI/BOOT/BOOTx64.efi", b"boot")
                archive.writestr("Docs/Sample.plist", plistlib.dumps(valid_config()))
            self.assertEqual(command_create_efi(str(package), str(output)), 2)
            self.assertFalse(output.exists())

    def test_create_efi_ignores_archive_path_traversal(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            package = root / "OpenCore.zip"
            output = root / "EFI"
            self._make_release(package)
            marker_name = f"escaped-{root.name}.txt"
            with zipfile.ZipFile(package, "a") as archive:
                archive.writestr(f"../../{marker_name}", b"do not extract")
            self.assertEqual(command_create_efi(str(package), str(output)), 0)
            self.assertFalse((root.parent / marker_name).exists())

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


class BackupTests(unittest.TestCase):
    def test_backup_creates_zip_and_preserves_source(self):
        with tempfile.TemporaryDirectory() as temp:
            base = Path(temp)
            efi = base / "EFI"
            efi.mkdir()
            sample = efi / "sample.txt"
            sample.write_text("EFI stays unchanged", encoding="utf-8")
            archive = base / "backup.zip"
            self.assertEqual(command_backup_efi(str(efi), str(archive)), 0)
            self.assertEqual(sample.read_text(encoding="utf-8"), "EFI stays unchanged")
            with zipfile.ZipFile(archive) as zipped:
                self.assertEqual(zipped.read("sample.txt"), b"EFI stays unchanged")

    def test_backup_does_not_overwrite_existing_zip(self):
        with tempfile.TemporaryDirectory() as temp:
            base = Path(temp)
            efi = base / "EFI"
            efi.mkdir()
            (efi / "sample.txt").write_text("source", encoding="utf-8")
            archive = base / "backup.zip"
            archive.write_bytes(b"keep this file")
            self.assertEqual(command_backup_efi(str(efi), str(archive)), 2)
            self.assertEqual(archive.read_bytes(), b"keep this file")

    def test_backup_refuses_destination_inside_source(self):
        with tempfile.TemporaryDirectory() as temp:
            efi = Path(temp) / "EFI"
            efi.mkdir()
            (efi / "sample.txt").write_text("source", encoding="utf-8")
            self.assertEqual(command_backup_efi(str(efi), str(efi / "backup.zip")), 2)
            self.assertFalse((efi / "backup.zip").exists())


class CompareTests(unittest.TestCase):
    def test_hash_compare_detects_changed_file(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            first = root / "first"
            second = root / "second"
            first.mkdir()
            second.mkdir()
            (first / "config.plist").write_bytes(b"one")
            (second / "config.plist").write_bytes(b"two")
            left_entries = {"config.plist": "config.plist"}
            right_entries = {"config.plist": "config.plist"}
            self.assertNotEqual(
                _file_digests(first, left_entries)["config.plist"],
                _file_digests(second, right_entries)["config.plist"],
            )

    def test_hash_compare_accepts_matching_zip_file_content(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            folder = root / "EFI"
            folder.mkdir()
            (folder / "sample.txt").write_text("sample", encoding="utf-8")
            archive_path = root / "EFI.zip"
            with zipfile.ZipFile(archive_path, "w") as archive:
                archive.write(folder / "sample.txt", "EFI/sample.txt")
            folder_entries = {"sample.txt": "sample.txt"}
            zip_entries = {"efi/sample.txt": "EFI/sample.txt"}
            self.assertEqual(
                _file_digests(folder, folder_entries)["sample.txt"],
                _file_digests(archive_path, zip_entries)["efi/sample.txt"],
            )


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
