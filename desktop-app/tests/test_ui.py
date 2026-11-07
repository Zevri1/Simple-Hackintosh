import os
os.environ.setdefault("QT_QPA_PLATFORM","offscreen")
from PySide6.QtWidgets import QApplication
from app.main import MainWindow

def test_window_has_all_workflow_pages():
    app=QApplication.instance() or QApplication([])
    window=MainWindow()
    assert window.pages.count()==7
    assert window.nav.count()==7
    assert window.windowTitle()=="Simple Hackintosh"
    window.close()
