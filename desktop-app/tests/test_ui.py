import os
os.environ.setdefault("QT_QPA_PLATFORM","offscreen")
from PySide6.QtWidgets import QApplication
from app.main import MainWindow

def test_window_has_downloader_and_efi_pages():
    app=QApplication.instance() or QApplication([])
    window=MainWindow()
    assert window.pages.count()==5
    assert window.nav.count()==5
    assert "Simple Hackintosh" in window.windowTitle()
    assert window.pages.widget(1) is not None  # Downloader
    assert window.pages.widget(2) is not None  # EFI Builder
    window.close()
