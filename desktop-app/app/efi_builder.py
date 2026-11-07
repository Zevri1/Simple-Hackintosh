"""Download official OpenCore release and generate a starter EFI tree."""
from __future__ import annotations
import json, plistlib, secrets, shutil, urllib.request, zipfile
from pathlib import Path

API = "https://api.github.com/repos/acidanthera/OpenCorePkg/releases/latest"
UA = "Simple-Hackintosh-Desktop"

def latest_opencore():
    req=urllib.request.Request(API,headers={"User-Agent":UA,"Accept":"application/vnd.github+json"})
    with urllib.request.urlopen(req,timeout=30) as r: data=json.load(r)
    asset=next((a for a in data.get("assets",[]) if a["name"].lower().endswith(".zip")),None)
    if not asset: raise RuntimeError("OpenCore release ZIP asset not found")
    return data["tag_name"],asset["browser_download_url"]

def _download(url,dest):
    req=urllib.request.Request(url,headers={"User-Agent":UA})
    with urllib.request.urlopen(req,timeout=120) as r,open(dest,"wb") as f: shutil.copyfileobj(r,f)

def build_efi(destination, smbios="MacBookPro16,3", acpi_files=None, kext_archives=None):
    dest=Path(destination).expanduser().resolve()
    dest.mkdir(parents=True,exist_ok=True)
    tag,url=latest_opencore()
    archive=dest/"OpenCore-release.zip"
    _download(url,archive)
    tmp=dest/".oc-extract"; shutil.rmtree(tmp,ignore_errors=True); tmp.mkdir()
    with zipfile.ZipFile(archive) as z: z.extractall(tmp)
    boot=next(tmp.rglob("BOOTx64.efi"),None)
    oc=next((p for p in tmp.rglob("OpenCore.efi") if p.parent.name=="Drivers"),None)
    if boot is None or oc is None: raise RuntimeError("OpenCore ZIP layout not recognized")
    efi=dest/"EFI"; (efi/"BOOT").mkdir(parents=True,exist_ok=True); (efi/"OC").mkdir(parents=True,exist_ok=True)
    shutil.copy2(boot,efi/"BOOT"/"BOOTx64.efi")
    source_oc=oc.parent.parent
    shutil.copytree(source_oc,efi/"OC",dirs_exist_ok=True)
    for folder in ("ACPI","Kexts","Drivers","Tools"): (efi/"OC"/folder).mkdir(exist_ok=True)
    if acpi_files:
        for f in acpi_files:
            p=Path(f); shutil.copy2(p,efi/"OC"/"ACPI"/p.name)
    if kext_archives:
        for f in kext_archives:
            with zipfile.ZipFile(f) as z:
                z.extractall(efi/"OC"/"Kexts")
    config={
      "ACPI":{"Add":[],"Delete":[],"Patch":[],"Quirks":{"FadtEnableReset":False,"ResetLogoStatus":False,"SyncTableIds":False}},
      "Booter":{"MmioWhitelist":[],"Patch":[],"Quirks":{"AllowRelocationBlock":False,"AvoidRuntimeDefrag":True,"DevirtualiseMmio":False,"DisableSingleUser":False,"DisableVariableWrite":False,"DiscardHibernateMap":False,"EnableSafeModeSlide":True,"EnableWriteUnprotector":False,"ForceExitBootServices":False,"ProtectMemoryRegions":False,"ProtectSecureBoot":False,"ProtectUefiServices":False,"ProvideCustomSlide":True,"ProvideMaxSlide":0,"RebuildAppleMemoryMap":False,"ResizeAppleGpuBars":-1,"ResizeGpuBars":-1,"SetupVirtualMap":False,"SignalAppleOS":False,"SyncRuntimePermissions":False}},
      "DeviceProperties":{"Add":{},"Delete":{}},
      "Kernel":{"Add":[],"Block":[],"Emulate":{"Cpuid1Data":"","Cpuid1Mask":""},"Force":[],"Patch":[],"Quirks":{"AppleCpuPmCfgLock":False,"AppleXcpmCfgLock":False,"AppleXcpmExtraMsrs":False,"AppleXcpmForceBoost":False,"CustomSMBIOSGuid":False,"DisableIoMapper":False,"DisableLinkeditJettison":False,"DisableRtcChecksum":False,"ExtendBTFeatureFlags":False,"ForceSecureBootScheme":False,"IncreasePciBarSize":False,"LapicKernelPanic":False,"LegacyCommpage":False,"PanicNoKextDump":True,"PowerTimeoutKernelPanic":True,"ProvideCurrentCpuInfo":False,"SetApfsTrimTimeout":-1,"SetApfsTrimTimeout":-1,"ThirdPartyDrives":False,"XhciPortLimit":False},"Scheme":{"CustomKernel":False,"KernelArch":"x86_64","KernelCache":"Auto"}},
      "Misc":{"BlessOverride":[],"Boot":{"ConsoleAttributes":0,"HibernateMode":"None","HideAuxiliary":True,"PickerAttributes":0,"PickerMode":"Builtin","PickerVariant":"Auto","PollAppleHotKeys":False,"ShowPicker":True,"Timeout":5},"Debug":{"AppleDebug":False,"ApplePanic":False,"DisableWatchDog":False,"DisplayDelay":0,"DisplayLevel":2147483650,"LogModules":"*","SysReport":False,"Target":0},"Entries":[],"Security":{"AllowSetDefault":True,"ApECID":0,"AuthRestart":False,"BlacklistAppleUpdate":True,"DmgLoading":"Signed","EnablePassword":False,"ExposeSensitiveData":6,"HaltLevel":2147483648,"PasswordHash":"","PasswordSalt":"","ScanPolicy":0,"SecureBootModel":"Default","Vault":"Optional"},"Tools":[]},
      "NVRAM":{"Add":{"7C436110-AB2A-4BBB-A880-FE41995C9F82":{"boot-args":"-v","csr-active-config":b"\x00\x00\x00\x00","prev-lang:kbd":"en-US:0"},"4D1FDA02-38C7-4A6A-9CC6-4BCCA8B30102":{"AllowNvramReset":True,"AllowToggleSip":True,"LegacyEnable":False,"LegacyOverwrite":False,"LegacySchema":True,"WriteFlash":False}},"Delete":{},"LegacySchema":True,"WriteFlash":False},
      "PlatformInfo":{"Automatic":False,"CustomMemory":False,"Generic":{"AdviseFeatures":False,"MLB":"","MaxBIOSVersion":False,"ProcessorType":0,"ROM":secrets.token_bytes(6),"SystemProductName":smbios,"SystemSerialNumber":"","SystemUUID":""},"UpdateDataHub":True,"UpdateNVRAM":True,"UpdateSMBIOS":True,"UpdateSMBIOSMode":"Create","UseRawUuidEncoding":False},
      "UEFI":{"APFS":{"EnableJumpstart":True,"GlobalConnect":False,"JumpstartHotPlug":False,"MinDate":-1,"MinVersion":-1},"Drivers":[],"Input":{"KeyMergeThreshold":2,"KeySupport":True,"KeySupportMode":"Auto","KeySwap":False,"PointerSupport":False,"PointerSupportMode":"ASUS","TimerResolution":50000},"Output":{"ClearScreen":False,"ConsoleMode":-1,"DirectGopRendering":False,"ForceResolution":False,"GopBurstMode":"Disabled","GopPassThrough":"Disabled","IgnoreTextInGraphics":False,"InitialMode":"Auto","Resolution":"Max","SanitiseClearScreen":False,"TextRenderer":"BuiltinGraphics","UgaPassThrough":"Disabled"},"ProtocolOverrides":{"AppleAudio":False,"AppleBootPolicy":False,"AppleDebugLog":False,"AppleEg2Info":False,"AppleFramebufferInfo":False,"AppleImageConversion":False,"AppleImg4Verification":False,"AppleKeyMap":False,"AppleRtcRam":False,"AppleSecureBoot":False,"AppleSmcIo":False,"AppleUserInterfaceTheme":False,"DataHub":False,"DeviceProperties":False,"FirmwareVolume":False,"HashServices":False,"OSInfo":False,"PciIo":False,"UnicodeCollation":False},"Quirks":{"ActivateHpetSupport":False,"DisableSecurityPolicy":False,"EnableVectorAcceleration":True,"ExitBootServicesDelay":0,"ForceOcWriteFlash":False,"ForgeUefiSupport":False,"IgnoreInvalidFlexRatio":False,"ReleaseUsbOwnership":False,"ReloadOptionRoms":False,"RequestBootVarRouting":True,"ResizeGpuBars":-1,"TscSyncTimeout":0,"UnblockFsConnect":False}}}
    # UUID and SMBIOS fields are unique placeholders; user must replace/validate for their machine.
    import uuid
    config["PlatformInfo"]["Generic"]["SystemUUID"]=str(uuid.uuid4()).upper()
    with open(efi/"OC"/"config.plist","wb") as f: plistlib.dump(config,f,sort_keys=False)
    shutil.rmtree(tmp,ignore_errors=True); archive.unlink(missing_ok=True)
    return {"opencore":tag,"path":str(efi),"config":str(efi/"OC"/"config.plist"),"note":"Starter config only; ACPI, kexts, quirks, SMBIOS and device properties require hardware-specific review."}
