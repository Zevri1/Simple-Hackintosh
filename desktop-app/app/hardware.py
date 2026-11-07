"""Read-only hardware inventory helpers for Windows/macOS/Linux."""
import platform
import subprocess

def inventory():
    os_name=platform.system()
    commands={
        "Windows":["powershell","-NoProfile","-ExecutionPolicy","Bypass","-Command",
          "$ErrorActionPreference='SilentlyContinue'; '=== CPU ==='; Get-CimInstance Win32_Processor | Select-Object Name,NumberOfCores | Format-List; '=== Board ==='; Get-CimInstance Win32_BaseBoard | Select-Object Manufacturer,Product | Format-List; '=== BIOS ==='; Get-CimInstance Win32_BIOS | Select-Object Manufacturer,SMBIOSBIOSVersion | Format-List; '=== GPU ==='; Get-CimInstance Win32_VideoController | Select-Object Name,PNPDeviceID | Format-List; '=== Network ==='; Get-CimInstance Win32_NetworkAdapter | Where-Object {$_.PhysicalAdapter} | Select-Object Name,PNPDeviceID | Format-List"],
        "Darwin":["/usr/sbin/system_profiler","SPHardwareDataType","SPPCIDataType","SPDisplaysDataType","SPNetworkDataType"],
        "Linux":["sh","-lc","(lscpu; echo '=== PCI ==='; lspci -nn 2>/dev/null)"]
    }
    command=commands.get(os_name)
    if not command: return f"Unsupported OS: {os_name}"
    try:
        return subprocess.check_output(command,stderr=subprocess.STDOUT,text=True,timeout=35)[:20000]
    except FileNotFoundError:
        return "Required system utility was not found. Install/enable the OS inventory utility and retry."
    except subprocess.CalledProcessError as e:
        return e.output[:20000] or str(e)
    except subprocess.TimeoutExpired:
        return "Hardware inventory timed out."
