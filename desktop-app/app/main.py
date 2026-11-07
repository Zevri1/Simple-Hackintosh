import os, platform, subprocess, sys, plistlib
from pathlib import Path
from PySide6.QtCore import Qt
from PySide6.QtGui import QFont
from PySide6.QtWidgets import (QApplication,QMainWindow,QWidget,QVBoxLayout,QHBoxLayout,QLabel,
 QPushButton,QFrame,QListWidget,QStackedWidget,QTextEdit,QMessageBox,QFileDialog,
 QLineEdit,QComboBox,QFormLayout)
from app.hardware import inventory
from app.downloader import prepare_and_launch
from app.efi_builder import build_efi

APP_NAME="Simple Hackintosh"; VERSION="0.2.0"
STYLE="""
QMainWindow,QWidget#root{background:#0b0d12;color:#e8eaf2;font-family:Inter,'Segoe UI',sans-serif}
QWidget#sidebar{background:#10131b;border-right:1px solid #242837}
QLabel#brand{font-size:20px;font-weight:800;color:#fff;padding:18px 8px}
QLabel#muted{color:#8e96aa} QLabel#title{font-size:27px;font-weight:750;color:#fff}
QLabel#subtitle{color:#9ba3b8;font-size:13px}
QFrame#card{background:#131722;border:1px solid #252b3b;border-radius:14px}
QPushButton{background:#1b2030;color:#e8eaf2;border:1px solid #30374a;border-radius:9px;padding:10px 14px;font-weight:600}
QPushButton:hover{background:#252b40;border-color:#6250a8}
QPushButton#primary{background:#7653e8;border:0;color:white}
QLineEdit,QComboBox{background:#0e1118;color:#e8eaf2;border:1px solid #30374a;border-radius:8px;padding:9px}
QTextEdit{background:#0e1118;color:#b9c4d8;border:1px solid #252b3b;border-radius:9px;padding:8px}
"""
def card(title,desc=""):
 b=QFrame();b.setObjectName("card");l=QVBoxLayout(b);l.setContentsMargins(18,16,18,16);l.setSpacing(9)
 h=QLabel(title);h.setStyleSheet("font-size:16px;font-weight:700;color:#fff");l.addWidget(h)
 if desc:d=QLabel(desc);d.setObjectName("muted");d.setWordWrap(True);l.addWidget(d)
 return b,l

class MainWindow(QMainWindow):
 def __init__(self):
  super().__init__();self.setWindowTitle(f"{APP_NAME} {VERSION}");self.resize(1180,780);self.setMinimumSize(900,620)
  self.acpi=[];self.kext_archives=[];self.dest=str(Path.home()/"Desktop"/"SimpleHackintosh-EFI")
  root=QWidget();root.setObjectName("root");self.setCentralWidget(root);layout=QHBoxLayout(root);layout.setContentsMargins(0,0,0,0);layout.setSpacing(0)
  side=QWidget();side.setObjectName("sidebar");side.setFixedWidth(225);sl=QVBoxLayout(side);sl.setContentsMargins(18,12,18,18)
  brand=QLabel("◈ SIMPLE\n   HACKINTOSH");brand.setObjectName("brand");sl.addWidget(brand)
  tag=QLabel("AUTOMATION STUDIO");tag.setObjectName("muted");sl.addWidget(tag)
  self.nav=QListWidget()
  for x in ["⌂  Overview","⌘  macOS Downloader","⚙  EFI Builder","✓  Validation","☰  Logs"]:self.nav.addItem(x)
  sl.addWidget(self.nav);sl.addStretch();foot=QLabel("PREVIEW • 0.2.0\nWindows + macOS");foot.setObjectName("muted");sl.addWidget(foot);layout.addWidget(side)
  self.pages=QStackedWidget();layout.addWidget(self.pages,1);self.nav.currentRowChanged.connect(self.pages.setCurrentIndex)
  self.pages.addWidget(self.overview());self.pages.addWidget(self.downloader_page());self.pages.addWidget(self.efi_page());self.pages.addWidget(self.validation_page());self.pages.addWidget(self.logs_page());self.nav.setCurrentRow(0)
 def header(self,t,s):
  w=QWidget();l=QVBoxLayout(w);l.setContentsMargins(0,0,0,14);a=QLabel(t);a.setObjectName("title");b=QLabel(s);b.setObjectName("subtitle");l.addWidget(a);l.addWidget(b);return w
 def overview(self):
  p=QWidget();l=QVBoxLayout(p);l.setContentsMargins(30,26,30,25);l.addWidget(self.header("Simple Hackintosh","macOS acquisition helper + OpenCore EFI starter builder"))
  c,cl=card("What's implemented","0.2.0 preview")
  for s in ["• macOS Downloader: launches gibMacOS to browse Apple-hosted installer catalog/downloads.","• EFI Builder: downloads official OpenCore release and creates EFI/OC structure + starter config.plist.","• Validation: parses plist and checks basic EFI file layout."]:
   x=QLabel(s);x.setWordWrap(True);x.setObjectName("muted");cl.addWidget(x)
  l.addWidget(c);c,cl=card("Hardware snapshot","Read-only detection")
  self.system=QLabel(f"{platform.system()} {platform.release()} • {platform.machine()}");self.system.setObjectName("muted");cl.addWidget(self.system)
  b=QPushButton("Detect hardware");b.clicked.connect(self.detect);cl.addWidget(b,alignment=Qt.AlignLeft);l.addWidget(c);l.addStretch();return p
 def downloader_page(self):
  p=QWidget();l=QVBoxLayout(p);l.setContentsMargins(30,26,30,25);l.addWidget(self.header("macOS Downloader","Use gibMacOS to retrieve installer packages from Apple's software catalog."))
  c,cl=card("Download workflow","The helper is downloaded from the upstream gibMacOS project, then opened in its own window.")
  x=QLabel("Select a macOS version inside gibMacOS. Downloads are saved in its folder. This app does not host Apple installer files.");x.setWordWrap(True);x.setObjectName("muted");cl.addWidget(x)
  b=QPushButton("Prepare / Launch gibMacOS");b.setObjectName("primary");b.clicked.connect(self.launch_downloader);cl.addWidget(b,alignment=Qt.AlignLeft)
  src=QPushButton("Open gibMacOS project page");src.clicked.connect(lambda:subprocess.Popen(["cmd","/c","start","https://github.com/corpnewt/gibMacOS"]) if platform.system()=="Windows" else subprocess.Popen(["open","https://github.com/corpnewt/gibMacOS"]));cl.addWidget(src,alignment=Qt.AlignLeft)
  l.addWidget(c);l.addStretch();return p
 def efi_page(self):
  p=QWidget();l=QVBoxLayout(p);l.setContentsMargins(30,26,30,25);l.addWidget(self.header("EFI Builder","Build a starter OpenCore EFI. Review every setting before booting it."))
  c,cl=card("Configuration","No machine-specific ACPI or kexts are guessed automatically.")
  form=QFormLayout();self.dest_edit=QLineEdit(self.dest);b=QPushButton("Browse…");b.clicked.connect(self.choose_dest);r=QHBoxLayout();r.addWidget(self.dest_edit);r.addWidget(b);form.addRow("Output folder",r)
  self.smbios=QComboBox();self.smbios.setEditable(True);self.smbios.addItems(["MacBookPro16,3","MacBookPro15,2","MacBookAir9,1","iMac20,1"]);form.addRow("SMBIOS model",self.smbios)
  cl.addLayout(form)
  row=QHBoxLayout();a=QPushButton("Add ACPI .aml");a.clicked.connect(self.pick_acpi);k=QPushButton("Add kext ZIP");k.clicked.connect(self.pick_kext);row.addWidget(a);row.addWidget(k);cl.addLayout(row)
  self.import_label=QLabel("ACPI: none • Kext archives: none");self.import_label.setObjectName("muted");self.import_label.setWordWrap(True);cl.addWidget(self.import_label)
  build=QPushButton("Download OpenCore + Build EFI");build.setObjectName("primary");build.clicked.connect(self.do_build);cl.addWidget(build,alignment=Qt.AlignLeft);l.addWidget(c)
  warn=QLabel("⚠ Starter config is NOT boot-ready. SMBIOS identifiers, ACPI, kexts, drivers, quirks and DeviceProperties must be tailored to the exact hardware.");warn.setWordWrap(True);warn.setObjectName("muted");l.addWidget(warn);l.addStretch();return p
 def validation_page(self):
  p=QWidget();l=QVBoxLayout(p);l.setContentsMargins(30,26,30,25);l.addWidget(self.header("Validation","Basic structural and plist validation for an existing EFI folder."))
  c,cl=card("Check EFI");b=QPushButton("Select EFI folder + validate");b.setObjectName("primary");b.clicked.connect(self.validate);cl.addWidget(b,alignment=Qt.AlignLeft);self.validation=QTextEdit();self.validation.setReadOnly(True);cl.addWidget(self.validation);l.addWidget(c);l.addStretch();return p
 def logs_page(self):
  p=QWidget();l=QVBoxLayout(p);l.setContentsMargins(30,26,30,25);l.addWidget(self.header("Activity logs","Local operation messages"));self.log=QTextEdit();self.log.setReadOnly(True);self.log.setPlainText("Simple Hackintosh 0.2.0 started.\n");l.addWidget(self.log);return p
 def logmsg(self,s):
  if hasattr(self,"log"):self.log.append(s)
 def detect(self):
  try:self.system.setText(inventory());self.logmsg("Hardware inventory collected (read-only).");self.nav.setCurrentRow(4)
  except Exception as e:QMessageBox.warning(self,"Detection",str(e))
 def launch_downloader(self):
  try:
   path=prepare_and_launch(Path.home()/"SimpleHackintosh-Downloads");self.logmsg("gibMacOS launched: "+path);QMessageBox.information(self,"Downloader","gibMacOS opened. Choose the macOS version in its window.")
  except Exception as e:QMessageBox.critical(self,"Downloader error",str(e))
 def choose_dest(self):
  d=QFileDialog.getExistingDirectory(self,"Choose output parent folder",self.dest_edit.text())
  if d:self.dest_edit.setText(d)
 def pick_acpi(self):
  fs,_=QFileDialog.getOpenFileNames(self,"Select ACPI AML files","","AML files (*.aml)")
  self.acpi.extend(fs);self.refresh_imports()
 def pick_kext(self):
  fs,_=QFileDialog.getOpenFileNames(self,"Select kext ZIP archives","","ZIP archives (*.zip)")
  self.kext_archives.extend(fs);self.refresh_imports()
 def refresh_imports(self):self.import_label.setText(f"ACPI: {len(self.acpi)} file(s) • Kext ZIPs: {len(self.kext_archives)}")
 def do_build(self):
  dest=self.dest_edit.text().strip()
  if not dest:QMessageBox.warning(self,"Output","Choose an output folder first.");return
  if Path(dest,"EFI").exists() and QMessageBox.question(self,"Replace EFI?","An EFI folder exists. Files may be overwritten. Continue?")!=QMessageBox.Yes:return
  if QMessageBox.question(self,"Confirm","Download official OpenCore and write a starter EFI to:\n"+dest+"\nContinue?")!=QMessageBox.Yes:return
  try:
   result=build_efi(dest,self.smbios.currentText().strip(),self.acpi,self.kext_archives);self.logmsg("EFI generated: "+result["path"])
   QMessageBox.information(self,"EFI created",result["path"]+"\n\n"+result["note"])
  except Exception as e:QMessageBox.critical(self,"EFI build failed",str(e));self.logmsg("EFI build error: "+str(e))
 def validate(self):
  d=QFileDialog.getExistingDirectory(self,"Select EFI folder")
  if not d:return
  e=Path(d);checks=[]
  for f in ["BOOT/BOOTx64.efi","OC/OpenCore.efi","OC/config.plist"]:checks.append(("PASS" if (e/f).is_file() else "FAIL")+" "+f)
  try:
   with open(e/"OC/config.plist","rb") as f:plistlib.load(f)
   checks.append("PASS config.plist parses")
  except Exception as x:checks.append("FAIL config.plist: "+str(x))
  self.validation.setPlainText("\n".join(checks));self.logmsg("EFI validation completed for "+d)
if __name__=="__main__":
 app=QApplication(sys.argv);app.setStyleSheet(STYLE);app.setFont(QFont("Segoe UI",10));w=MainWindow();w.show();sys.exit(app.exec())
