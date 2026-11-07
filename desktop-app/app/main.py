import os, platform, subprocess, sys
from app.hardware import inventory
from pathlib import Path
from PySide6.QtCore import Qt, QThread, Signal
from PySide6.QtGui import QFont
from PySide6.QtWidgets import (QApplication, QMainWindow, QWidget, QVBoxLayout, QHBoxLayout,
    QLabel, QPushButton, QFrame, QListWidget, QListWidgetItem, QStackedWidget, QTextEdit,
    QProgressBar, QMessageBox, QFileDialog, QGridLayout)

APP_NAME = "Simple Hackintosh"
ACCENT = "#8b5cf6"

STYLE = """
QMainWindow, QWidget#root { background:#0b0d12; color:#e8eaf2; font-family:Inter,'Segoe UI',sans-serif; }
QWidget#sidebar { background:#10131b; border-right:1px solid #242837; }
QLabel#brand { font-size:20px; font-weight:800; color:#fff; padding:18px 8px; }
QLabel#muted { color:#8e96aa; }
QLabel#title { font-size:27px; font-weight:750; color:#fff; }
QLabel#subtitle { color:#9ba3b8; font-size:13px; }
QFrame#card { background:#131722; border:1px solid #252b3b; border-radius:14px; }
QPushButton { background:#1b2030; color:#e8eaf2; border:1px solid #30374a; border-radius:9px; padding:10px 14px; font-weight:600; }
QPushButton:hover { background:#252b40; border-color:#6250a8; }
QPushButton#primary { background:#7653e8; border:0; color:white; }
QPushButton#primary:hover { background:#8869f1; }
QListWidget { background:transparent; border:0; outline:0; }
QListWidget::item { padding:13px 12px; margin:3px 0; border-radius:9px; color:#9ba3b8; }
QListWidget::item:selected { background:#25213a; color:#c6b6ff; }
QTextEdit { background:#0e1118; color:#b9c4d8; border:1px solid #252b3b; border-radius:9px; padding:8px; }
QProgressBar { background:#202536; border:0; border-radius:5px; height:9px; text-align:center; }
QProgressBar::chunk { background:#8b5cf6; border-radius:5px; }
"""

def card(title, description=""):
    box=QFrame(); box.setObjectName("card")
    l=QVBoxLayout(box); l.setContentsMargins(18,16,18,16); l.setSpacing(8)
    h=QLabel(title); h.setStyleSheet("font-size:16px;font-weight:700;color:#fff")
    l.addWidget(h)
    if description:
        d=QLabel(description); d.setObjectName("muted"); d.setWordWrap(True); l.addWidget(d)
    return box,l

class Worker(QThread):
    done=Signal(str)
    def __init__(self, command): super().__init__(); self.command=command
    def run(self):
        try:
            out=subprocess.check_output(self.command,stderr=subprocess.STDOUT,text=True,timeout=25)
            self.done.emit(out[:12000])
        except Exception as e: self.done.emit(f"Command unavailable: {e}")

class MainWindow(QMainWindow):
    def __init__(self):
        super().__init__()
        self.setWindowTitle("Simple Hackintosh")
        self.resize(1180,760); self.setMinimumSize(900,600)
        root=QWidget(); root.setObjectName("root"); self.setCentralWidget(root)
        layout=QHBoxLayout(root); layout.setContentsMargins(0,0,0,0); layout.setSpacing(0)
        side=QWidget(); side.setObjectName("sidebar"); side.setFixedWidth(225)
        sl=QVBoxLayout(side); sl.setContentsMargins(18,12,18,18)
        brand=QLabel("◈ SIMPLE\n   HACKINTOSH"); brand.setObjectName("brand"); sl.addWidget(brand)
        tag=QLabel("AUTOMATION STUDIO"); tag.setObjectName("muted"); tag.setStyleSheet("letter-spacing:2px;font-size:10px;padding:4px 8px 14px")
        sl.addWidget(tag)
        self.nav=QListWidget()
        for name in ["⌂   Overview","⌘   macOS Downloader","⚙   EFI Builder","▣   USB Installer","⇧   Install macOS","✓   Validation","☰   Logs"]:
            self.nav.addItem(name)
        self.nav.setCurrentRow(0); self.nav.currentRowChanged.connect(self.pages.setCurrentIndex if hasattr(self,'pages') else lambda _:None)
        sl.addWidget(self.nav); sl.addStretch()
        foot=QLabel("PREVIEW • 0.1.0\nWindows + macOS"); foot.setObjectName("muted"); sl.addWidget(foot)
        layout.addWidget(side)
        self.pages=QStackedWidget(); layout.addWidget(self.pages,1)
        self.nav.currentRowChanged.connect(self.pages.setCurrentIndex)
        self.pages.addWidget(self.overview()); self.pages.addWidget(self.simple_page("macOS Downloader","Download official macOS installer assets from Apple Software Update catalogs. A Mac is not required for catalog access; installation media still needs Apple-compatible packaging and verification.","Download catalog"))
        self.pages.addWidget(self.simple_page("EFI Builder","Hardware-aware OpenCore configuration: SMBIOS selection, ACPI, drivers, kext inventory and config.plist validation. Generated EFI must be reviewed before boot.","Build EFI"))
        self.pages.addWidget(self.simple_page("USB Installer","Prepare a bootable installer on a removable drive. The selected USB will be erased—this workflow will require an explicit confirmation and disk identity check.","Select USB"))
        self.pages.addWidget(self.simple_page("Install macOS","Choose a target disk and follow the guided install. The app will never format a disk silently; backups and explicit disk confirmation are mandatory.","Choose target"))
        self.pages.addWidget(self.simple_page("Validation","Check OpenCore files, plist syntax, required drivers and common configuration conflicts before reboot.","Run checks"))
        self.pages.addWidget(self.logs_page())

    def header(self,title,sub):
        w=QWidget(); l=QVBoxLayout(w); l.setContentsMargins(0,0,0,14)
        a=QLabel(title); a.setObjectName("title"); b=QLabel(sub); b.setObjectName("subtitle")
        l.addWidget(a); l.addWidget(b); return w

    def overview(self):
        page=QWidget(); l=QVBoxLayout(page); l.setContentsMargins(30,26,30,25); l.setSpacing(18)
        l.addWidget(self.header("Welcome to Simple Hackintosh","A guided macOS installation workspace for Windows and macOS."))
        row=QHBoxLayout()
        for t,d in [("01  Download","Get installer assets"),("02  Build EFI","Generate a hardware profile"),("03  Install","Create media and install")]:
            c,cl=card(t,d); cl.addStretch(); row.addWidget(c)
        l.addLayout(row)
        c,cl=card("System snapshot","Detected locally • no changes made")
        self.system=QLabel(self.system_info()); self.system.setObjectName("muted"); self.system.setWordWrap(True)
        cl.addWidget(self.system)
        btn=QPushButton("↻  Detect hardware"); btn.setObjectName("primary"); btn.clicked.connect(self.detect); cl.addWidget(btn,alignment=Qt.AlignLeft)
        l.addWidget(c)
        c,cl=card("Quick start","These actions are safe previews; disk-writing steps are gated.")
        actions=QHBoxLayout()
        for label,index in [("Download macOS",1),("Build EFI",2),("Create USB",3)]:
            b=QPushButton(label); b.clicked.connect(lambda checked=False,i=index:self.pages.setCurrentIndex(i)); actions.addWidget(b)
        cl.addLayout(actions); l.addWidget(c); l.addStretch()
        return page

    def system_info(self):
        return f"OS: {platform.system()} {platform.release()}  •  Architecture: {platform.machine()}\nPython: {platform.python_version()}  •  App: Simple Hackintosh 0.1.0"

    def detect(self):
        self.system.setText(self.system_info()+"\n\nHardware inventory: baseline OS data detected. Detailed PCI/ACPI inventory adapters are planned for Windows and macOS.")
        QMessageBox.information(self,"Detection complete","Basic system information was detected. Advanced PCI/ACPI inventory is not implemented in this preview.")

    def simple_page(self,title,desc,action):
        page=QWidget(); l=QVBoxLayout(page); l.setContentsMargins(30,26,30,25)
        l.addWidget(self.header(title,desc))
        c,cl=card("Workflow","")
        info=QLabel("This module is scaffolded. No disk writes or firmware changes are performed in this preview.")
        info.setObjectName("muted"); info.setWordWrap(True); cl.addWidget(info)
        progress=QProgressBar(); progress.setRange(0,100); progress.setValue(0); progress.setTextVisible(False); cl.addWidget(progress)
        btn=QPushButton(action); btn.setObjectName("primary")
        btn.clicked.connect(lambda:QMessageBox.information(self,title,"This workflow is not wired yet. The UI foundation is ready; next step is implementing and testing this module."))
        cl.addWidget(btn,alignment=Qt.AlignLeft); l.addWidget(c); l.addStretch(); return page

    def logs_page(self):
        p=QWidget(); l=QVBoxLayout(p); l.setContentsMargins(30,26,30,25); l.addWidget(self.header("Activity logs","Local diagnostic output"))
        self.log=QTextEdit(); self.log.setReadOnly(True); self.log.setPlainText("Simple Hackintosh 0.1.0 started.\nSafe preview mode enabled.\n")
        l.addWidget(self.log); return p

if __name__=="__main__":
    app=QApplication(sys.argv); app.setStyleSheet(STYLE)
    app.setFont(QFont("Segoe UI",10))
    w=MainWindow(); w.show(); sys.exit(app.exec())
