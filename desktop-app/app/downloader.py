"""Launch gibMacOS to acquire Apple-hosted macOS installer packages."""
from __future__ import annotations
import os, platform, shutil, subprocess, urllib.request, zipfile
from pathlib import Path

REPO_ZIP="https://github.com/corpnewt/gibMacOS/archive/refs/heads/master.zip"
def prepare_and_launch(workdir):
    root=Path(workdir).expanduser().resolve()/"gibMacOS"
    root.parent.mkdir(parents=True,exist_ok=True)
    if not root.exists():
        archive=root.parent/"gibMacOS.zip"
        req=urllib.request.Request(REPO_ZIP,headers={"User-Agent":"Simple-Hackintosh"})
        with urllib.request.urlopen(req,timeout=60) as r,open(archive,"wb") as f: shutil.copyfileobj(r,f)
        with zipfile.ZipFile(archive) as z: z.extractall(root.parent)
        extracted=next(root.parent.glob("gibMacOS-*"),None)
        if not extracted: raise RuntimeError("gibMacOS archive extraction failed")
        extracted.rename(root); archive.unlink(missing_ok=True)
    if platform.system()=="Windows":
        script=root/"gibMacOS.bat"
        if not script.exists(): raise RuntimeError("gibMacOS.bat missing")
        subprocess.Popen(["cmd","/c","start","",str(script)],cwd=str(root),shell=False)
    elif platform.system()=="Darwin":
        script=root/"gibMacOS.command"
        if not script.exists(): raise RuntimeError("gibMacOS.command missing")
        subprocess.Popen(["open",str(script)],cwd=str(root))
    else:
        raise RuntimeError("macOS installer download helper supports Windows/macOS only.")
    return str(root)
