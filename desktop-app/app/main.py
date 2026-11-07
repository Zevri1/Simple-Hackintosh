import os, platform, subprocess, sys, plistlib
from pathlib import Path

from PySide6.QtCore import Qt
from PySide6.QtGui import QFont
from PySide6.QtWidgets import (
    QApplication, QMainWindow, QWidget, QVBoxLayout, QHBoxLayout, QLabel,
    QPushButton, QFrame, QListWidget, QStackedWidget, QTextEdit, QMessageBox,
    QFileDialog, QLineEdit, QComboBox, QFormLayout, QSizePolicy
)
from app.hardware import inventory
from app.downloader import prepare_and_launch
from app.efi_builder import build_efi

APP_NAME = "Simple Hackintosh"
VERSION = "0.2.1"

STYLE = """
* { font-family: "Inter", "Segoe UI", sans-serif; }
QMainWindow, QWidget#root { background: #0b0e14; color: #e8ecf5; }
QWidget#sidebar { background: #0e121b; border-right: 1px solid #202738; }
QLabel#brand { color: #f7f8ff; font-size: 19px; font-weight: 800; letter-spacing: 1px; }
QLabel#brandMark { color: #9b83ff; font-size: 25px; font-weight: 900; }
QLabel#eyebrow { color: #77839a; font-size: 10px; font-weight: 800; letter-spacing: 1.5px; }
QLabel#pageTitle { color: #f7f8ff; font-size: 28px; font-weight: 800; }
QLabel#pageSubtitle { color: #9ba7bb; font-size: 13px; }
QLabel#muted { color: #929db1; font-size: 12px; }
QLabel#smallMuted { color: #78849a; font-size: 11px; }
QLabel#metric { color: #f7f8ff; font-size: 22px; font-weight: 800; }
QLabel#cardTitle { color: #f4f6fc; font-size: 15px; font-weight: 750; }
QLabel#pill { color: #bcaeff; background: #211b3a; border: 1px solid #3a2e62;
              border-radius: 10px; padding: 5px 9px; font-size: 10px; font-weight: 800; }
QFrame#card { background: #121824; border: 1px solid #242e40; border-radius: 15px; }
QFrame#hero { background: #17162a; border: 1px solid #39315e; border-radius: 18px; }
QFrame#metricCard { background: #111824; border: 1px solid #222d3e; border-radius: 13px; }
QListWidget { background: transparent; border: 0; outline: 0; color: #9ba7bb;
              padding-top: 18px; }
QListWidget::item { padding: 13px 12px; margin: 3px 0; border-radius: 9px;
                    font-size: 13px; font-weight: 600; }
QListWidget::item:hover { background: #171e2c; color: #f3f5fb; }
QListWidget::item:selected { background: #282044; color: #c9bbff; border-left: 3px solid #9277ff; }
QPushButton { background: #1a2231; color: #e7ebf4; border: 1px solid #303c51;
              border-radius: 9px; padding: 10px 15px; font-size: 12px; font-weight: 700; }
QPushButton:hover { background: #242e42; border-color: #7662bd; }
QPushButton:pressed { background: #151c29; }
QPushButton#primary { background: #8064f2; color: white; border: 1px solid #927cff; }
QPushButton#primary:hover { background: #927cff; }
QPushButton#quiet { background: transparent; border: 1px solid #30394b; }
QLineEdit, QComboBox { background: #0d131d; color: #e8ecf5; border: 1px solid #303b4e;
                       border-radius: 8px; padding: 9px 10px; min-height: 18px; }
QLineEdit:focus, QComboBox:focus { border: 1px solid #8870ed; }
QComboBox QAbstractItemView { background: #141b28; color: #e8ecf5; selection-background-color: #34295c; }
QTextEdit { background: #0d131d; color: #b9c5d9; border: 1px solid #263247;
            border-radius: 10px; padding: 10px; selection-background-color: #443576; }
QScrollBar:vertical { background: #0d131d; width: 8px; margin: 2px; }
QScrollBar::handle:vertical { background: #354158; border-radius: 4px; min-height: 25px; }
QScrollBar::add-line:vertical, QScrollBar::sub-line:vertical { height: 0; }
"""

def make_card(title, description=None, object_name="card"):
    frame = QFrame()
    frame.setObjectName(object_name)
    layout = QVBoxLayout(frame)
    layout.setContentsMargins(19, 17, 19, 17)
    layout.setSpacing(10)
    heading = QLabel(title)
    heading.setObjectName("cardTitle")
    layout.addWidget(heading)
    if description:
        detail = QLabel(description)
        detail.setObjectName("muted")
        detail.setWordWrap(True)
        layout.addWidget(detail)
    return frame, layout


def page_header(title, subtitle, eyebrow="WORKSPACE"):
    wrapper = QWidget()
    layout = QVBoxLayout(wrapper)
    layout.setContentsMargins(0, 0, 0, 12)
    layout.setSpacing(6)
    label = QLabel(eyebrow)
    label.setObjectName("eyebrow")
    heading = QLabel(title)
    heading.setObjectName("pageTitle")
    sub = QLabel(subtitle)
    sub.setObjectName("pageSubtitle")
    sub.setWordWrap(True)
    layout.addWidget(label)
    layout.addWidget(heading)
    layout.addWidget(sub)
    return wrapper


class MainWindow(QMainWindow):
    def __init__(self):
        super().__init__()
        self.setWindowTitle(f"{APP_NAME} {VERSION}")
        self.resize(1240, 820)
        self.setMinimumSize(960, 660)
        self.acpi = []
        self.kext_archives = []
        self.dest = str(Path.home() / "Desktop" / "SimpleHackintosh-EFI")

        root = QWidget()
        root.setObjectName("root")
        self.setCentralWidget(root)
        outer = QHBoxLayout(root)
        outer.setContentsMargins(0, 0, 0, 0)
        outer.setSpacing(0)

        sidebar = QWidget()
        sidebar.setObjectName("sidebar")
        sidebar.setFixedWidth(236)
        side = QVBoxLayout(sidebar)
        side.setContentsMargins(19, 22, 19, 18)
        side.setSpacing(8)

        brand_row = QHBoxLayout()
        mark = QLabel("◈")
        mark.setObjectName("brandMark")
        brand = QLabel("SIMPLE\nHACKINTOSH")
        brand.setObjectName("brand")
        brand_row.addWidget(mark, 0, Qt.AlignTop)
        brand_row.addWidget(brand)
        brand_row.addStretch()
        side.addLayout(brand_row)
        side.addSpacing(15)
        section = QLabel("YOUR TOOLKIT")
        section.setObjectName("eyebrow")
        side.addWidget(section)

        self.nav = QListWidget()
        self.nav.setSpacing(3)
        for item in ["⌂    Overview", "⇩    macOS Downloader", "⚙    EFI Builder",
                     "✓    Validation", "☷    Activity Logs"]:
            self.nav.addItem(item)
        side.addWidget(self.nav, 1)

        version_card, version_layout = make_card("PREVIEW BUILD", "Windows  •  macOS")
        version = QLabel(VERSION)
        version.setObjectName("metric")
        version_layout.addWidget(version)
        side.addWidget(version_card)
        footer = QLabel("Built for the Hackintosh community")
        footer.setObjectName("smallMuted")
        footer.setWordWrap(True)
        side.addWidget(footer)
        outer.addWidget(side)

        content = QWidget()
        content_layout = QVBoxLayout(content)
        content_layout.setContentsMargins(28, 20, 28, 24)
        content_layout.setSpacing(0)
        topbar = QHBoxLayout()
        context = QLabel("SIMPLE HACKINTOSH  /  DESKTOP")
        context.setObjectName("eyebrow")
        topbar.addWidget(context)
        topbar.addStretch()
        self.status_pill = QLabel("●  READY")
        self.status_pill.setObjectName("pill")
        topbar.addWidget(self.status_pill)
        content_layout.addLayout(topbar)
        content_layout.addSpacing(18)

        self.pages = QStackedWidget()
        self.pages.addWidget(self.overview())
        self.pages.addWidget(self.downloader_page())
        self.pages.addWidget(self.efi_page())
        self.pages.addWidget(self.validation_page())
        self.pages.addWidget(self.logs_page())
        content_layout.addWidget(self.pages, 1)
        outer.addWidget(content, 1)
        self.nav.currentRowChanged.connect(self.pages.setCurrentIndex)
        self.nav.setCurrentRow(0)

    def page_layout(self, page, title, subtitle, eyebrow="WORKSPACE"):
        layout = QVBoxLayout(page)
        layout.setContentsMargins(0, 0, 0, 0)
        layout.setSpacing(15)
        layout.addWidget(page_header(title, subtitle, eyebrow))
        return layout

    def overview(self):
        page = QWidget()
        layout = self.page_layout(page, "Welcome back.", "A cleaner workspace for your macOS and OpenCore preparation.")
        hero = QFrame()
        hero.setObjectName("hero")
        hero_layout = QHBoxLayout(hero)
        hero_layout.setContentsMargins(23, 20, 23, 20)
        hero_text = QVBoxLayout()
        badge = QLabel("HACKINTOSH WORKSPACE  •  VERSION " + VERSION)
        badge.setObjectName("eyebrow")
        title = QLabel("Build. Prepare. Validate.")
        title.setStyleSheet("font-size: 23px; font-weight: 800; color: #ffffff;")
        description = QLabel("A focused set of helpers for getting your OpenCore workflow organized.")
        description.setObjectName("muted")
        description.setWordWrap(True)
        hero_text.addWidget(badge)
        hero_text.addWidget(title)
        hero_text.addWidget(description)
        hero_layout.addLayout(hero_text, 1)
        hero_action = QPushButton("Open EFI Builder  →")
        hero_action.setObjectName("primary")
        hero_action.clicked.connect(lambda: self.nav.setCurrentRow(2))
        hero_layout.addWidget(hero_action, 0, Qt.AlignVCenter)
        layout.addWidget(hero)

        metrics = QHBoxLayout()
        metrics.setSpacing(12)
        for number, caption in [("02", "CORE WORKFLOWS"), ("05", "WORKSPACE PAGES"), ("0.2.1", "PREVIEW VERSION")]:
            tile = QFrame()
            tile.setObjectName("metricCard")
            tile_layout = QVBoxLayout(tile)
            tile_layout.setContentsMargins(16, 13, 16, 13)
            value = QLabel(number)
            value.setObjectName("metric")
            label = QLabel(caption)
            label.setObjectName("smallMuted")
            tile_layout.addWidget(value)
            tile_layout.addWidget(label)
            metrics.addWidget(tile)
        layout.addLayout(metrics)

        hardware, hardware_layout = make_card("This computer", "Read-only hardware overview for the current machine.")
        self.system = QLabel(f"{platform.system()} {platform.release()}  •  {platform.machine()}")
        self.system.setObjectName("muted")
        self.system.setWordWrap(True)
        hardware_actions = QHBoxLayout()
        hardware_actions.addWidget(self.system, 1)
        detect_button = QPushButton("Detect hardware")
        detect_button.clicked.connect(self.detect)
        hardware_actions.addWidget(detect_button)
        hardware_layout.addLayout(hardware_actions)
        layout.addWidget(hardware)

        row = QHBoxLayout()
        row.setSpacing(13)
        downloader, dl = make_card("01  ·  macOS Downloader", "Use the upstream gibMacOS utility to browse Apple's installer catalog.")
        dl_button = QPushButton("Go to Downloader  →")
        dl_button.clicked.connect(lambda: self.nav.setCurrentRow(1))
        dl.addWidget(dl_button, alignment=Qt.AlignLeft)
        builder, bl = make_card("02  ·  OpenCore EFI Builder", "Fetch official OpenCore files and assemble a starter EFI structure.")
        build_button = QPushButton("Go to EFI Builder  →")
        build_button.clicked.connect(lambda: self.nav.setCurrentRow(2))
        bl.addWidget(build_button, alignment=Qt.AlignLeft)
        row.addWidget(downloader, 1)
        row.addWidget(builder, 1)
        layout.addLayout(row)

        note, nl = make_card("Important compatibility note", "This preview creates a starter EFI, not a machine-ready configuration. Always customize and review ACPI, kexts, drivers, SMBIOS and config.plist before booting.")
        note_label = QLabel("⚠  Keep your working EFI backed up and test changes from a separate USB.")
        note_label.setObjectName("muted")
        note_label.setWordWrap(True)
        nl.addWidget(note_label)
        layout.addWidget(note)
        layout.addStretch()
        return page

    def downloader_page(self):
        page = QWidget()
        layout = self.page_layout(page, "macOS Downloader", "Access Apple's installer catalog through the trusted upstream gibMacOS project.", "INSTALLER TOOLS")
        panel, inner = make_card("Installer acquisition", "Simple Hackintosh prepares and opens the external gibMacOS utility. Installer files are provided by Apple, not hosted by this app.")
        info = QLabel("Choose the macOS release inside the gibMacOS window. The helper manages its own download folder and workflow.")
        info.setObjectName("muted")
        info.setWordWrap(True)
        inner.addWidget(info)
        actions = QHBoxLayout()
        launch = QPushButton("⇩   Prepare & Launch gibMacOS")
        launch.setObjectName("primary")
        launch.clicked.connect(self.launch_downloader)
        source = QPushButton("View upstream project")
        source.setObjectName("quiet")
        source.clicked.connect(self.open_project_page)
        actions.addWidget(launch)
        actions.addWidget(source)
        actions.addStretch()
        inner.addLayout(actions)
        layout.addWidget(panel)
        layout.addStretch()
        return page

    def efi_page(self):
        page = QWidget()
        layout = self.page_layout(page, "OpenCore EFI Builder", "Assemble a starter EFI from official OpenCore files and your selected components.", "BOOT CONFIGURATION")
        panel, inner = make_card("Build settings", "Choose an output location and SMBIOS model, then add the ACPI and kext archives you intend to use.")
        form = QFormLayout()
        form.setSpacing(12)
        self.dest_edit = QLineEdit(self.dest)
        browse = QPushButton("Browse…")
        browse.clicked.connect(self.choose_dest)
        destination = QHBoxLayout()
        destination.addWidget(self.dest_edit, 1)
        destination.addWidget(browse)
        form.addRow("Output folder", destination)
        self.smbios = QComboBox()
        self.smbios.setEditable(True)
        self.smbios.addItems(["MacBookPro16,3", "MacBookPro15,2", "MacBookAir9,1", "iMac20,1"])
        form.addRow("SMBIOS model", self.smbios)
        inner.addLayout(form)

        divider = QFrame()
        divider.setFrameShape(QFrame.HLine)
        divider.setStyleSheet("color: #283247;")
        inner.addWidget(divider)
        component_row = QHBoxLayout()
        acpi_button = QPushButton("+  Add ACPI files")
        acpi_button.clicked.connect(self.pick_acpi)
        kext_button = QPushButton("+  Add kext ZIPs")
        kext_button.clicked.connect(self.pick_kext)
        component_row.addWidget(acpi_button)
        component_row.addWidget(kext_button)
        component_row.addStretch()
        inner.addLayout(component_row)
        self.import_label = QLabel("ACPI: none     •     Kext archives: none")
        self.import_label.setObjectName("muted")
        inner.addWidget(self.import_label)
        build = QPushButton("Download OpenCore & Build EFI  →")
        build.setObjectName("primary")
        build.clicked.connect(self.do_build)
        inner.addWidget(build, alignment=Qt.AlignLeft)
        layout.addWidget(panel)

        warning, warning_layout = make_card("Before you boot", "The generated config.plist is generic. This tool does not yet detect and configure every device or produce a hardware-specific EFI.")
        warning_label = QLabel("⚠  Review ACPI, kexts, drivers, DeviceProperties, quirks and SMBIOS values for your exact machine. Never replace your only working EFI.")
        warning_label.setObjectName("muted")
        warning_label.setWordWrap(True)
        warning_layout.addWidget(warning_label)
        layout.addWidget(warning)
        layout.addStretch()
        return page

    def validation_page(self):
        page = QWidget()
        layout = self.page_layout(page, "EFI Validation", "Run basic structure checks and confirm that config.plist can be parsed.", "QUALITY CHECKS")
        panel, inner = make_card("EFI folder inspection", "Select an EFI directory to check the expected OpenCore files.")
        button = QPushButton("Select EFI folder & Validate")
        button.setObjectName("primary")
        button.clicked.connect(self.validate)
        inner.addWidget(button, alignment=Qt.AlignLeft)
        self.validation = QTextEdit()
        self.validation.setReadOnly(True)
        self.validation.setPlaceholderText("Validation results will appear here…")
        self.validation.setMinimumHeight(220)
        inner.addWidget(self.validation)
        layout.addWidget(panel)
        layout.addStretch()
        return page

    def logs_page(self):
        page = QWidget()
        layout = self.page_layout(page, "Activity Logs", "A local record of actions performed by this app.", "DIAGNOSTICS")
        panel, inner = make_card("Session activity")
        self.log = QTextEdit()
        self.log.setReadOnly(True)
        self.log.setPlainText(f"{APP_NAME} {VERSION} started.\n")
        inner.addWidget(self.log)
        layout.addWidget(panel, 1)
        return page

    def logmsg(self, message):
        if hasattr(self, "log"):
            self.log.append(message)

    def set_status(self, text):
        self.status_pill.setText("●  " + text.upper())

    def detect(self):
        try:
            self.system.setText(inventory())
            self.logmsg("Hardware inventory collected (read-only).")
            self.nav.setCurrentRow(4)
        except Exception as error:
            QMessageBox.warning(self, "Hardware detection", str(error))

    def launch_downloader(self):
        try:
            path = prepare_and_launch(Path.home() / "SimpleHackintosh-Downloads")
            self.logmsg("gibMacOS launched: " + str(path))
            self.set_status("DOWNLOADER OPENED")
            QMessageBox.information(self, "Downloader ready", "gibMacOS has been opened. Choose a macOS version in its own window.")
        except Exception as error:
            self.set_status("ACTION FAILED")
            QMessageBox.critical(self, "Downloader error", str(error))

    def open_project_page(self):
        url = "https://github.com/corpnewt/gibMacOS"
        if platform.system() == "Windows":
            subprocess.Popen(["cmd", "/c", "start", "", url])
        elif platform.system() == "Darwin":
            subprocess.Popen(["open", url])
        else:
            subprocess.Popen(["xdg-open", url])

    def choose_dest(self):
        folder = QFileDialog.getExistingDirectory(self, "Choose output folder", self.dest_edit.text())
        if folder:
            self.dest_edit.setText(folder)

    def pick_acpi(self):
        files, _ = QFileDialog.getOpenFileNames(self, "Select ACPI AML files", "", "AML files (*.aml)")
        self.acpi.extend(files)
        self.refresh_imports()

    def pick_kext(self):
        files, _ = QFileDialog.getOpenFileNames(self, "Select kext ZIP archives", "", "ZIP archives (*.zip)")
        self.kext_archives.extend(files)
        self.refresh_imports()

    def refresh_imports(self):
        self.import_label.setText(f"ACPI: {len(self.acpi)} file(s)     •     Kext archives: {len(self.kext_archives)}")

    def do_build(self):
        destination = self.dest_edit.text().strip()
        if not destination:
            QMessageBox.warning(self, "Output folder", "Choose an output folder first.")
            return
        if Path(destination, "EFI").exists():
            answer = QMessageBox.question(self, "Existing EFI", "An EFI folder already exists. Files may be overwritten. Continue?")
            if answer != QMessageBox.Yes:
                return
        answer = QMessageBox.question(self, "Confirm EFI build", "Download official OpenCore and write a starter EFI to:\n" + destination + "\n\nContinue?")
        if answer != QMessageBox.Yes:
            return
        try:
            result = build_efi(destination, self.smbios.currentText().strip(), self.acpi, self.kext_archives)
            self.logmsg("EFI generated: " + result["path"])
            self.set_status("EFI CREATED")
            QMessageBox.information(self, "Starter EFI created", result["path"] + "\n\n" + result["note"])
        except Exception as error:
            self.set_status("BUILD FAILED")
            QMessageBox.critical(self, "EFI build failed", str(error))
            self.logmsg("EFI build error: " + str(error))

    def validate(self):
        folder = QFileDialog.getExistingDirectory(self, "Select EFI folder")
        if not folder:
            return
        root = Path(folder)
        checks = []
        for relative in ["BOOT/BOOTx64.efi", "OC/OpenCore.efi", "OC/config.plist"]:
            checks.append(("PASS" if (root / relative).is_file() else "FAIL") + "  " + relative)
        try:
            with open(root / "OC/config.plist", "rb") as plist_file:
                plistlib.load(plist_file)
            checks.append("PASS  config.plist parses successfully")
        except Exception as error:
            checks.append("FAIL  config.plist: " + str(error))
        self.validation.setPlainText("\n".join(checks))
        self.logmsg("EFI validation completed for " + folder)
        self.set_status("VALIDATION COMPLETE")


if __name__ == "__main__":
    app = QApplication(sys.argv)
    app.setStyleSheet(STYLE)
    app.setFont(QFont("Segoe UI", 10))
    window = MainWindow()
    window.show()
    sys.exit(app.exec())
