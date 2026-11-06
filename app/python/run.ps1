[CmdletBinding()]
param(
    [Parameter(Position = 0, ValueFromRemainingArguments = $true)]
    [string[]] $CliArguments
)

$ErrorActionPreference = "Stop"
$ScriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$PythonScript = Join-Path $ScriptDirectory "simple_hackintosh.py"

if (-not (Test-Path -LiteralPath $PythonScript -PathType Leaf)) {
    Write-Host "[ERROR] simple_hackintosh.py was not found beside run.ps1." -ForegroundColor Red
    exit 2
}

$PythonCommand = Get-Command -Name py, python, python3 -CommandType Application -ErrorAction SilentlyContinue |
    Select-Object -First 1

if (-not $PythonCommand) {
    Write-Host "[ERROR] Python 3.10 or newer was not found." -ForegroundColor Red
    Write-Host "Install Python from https://www.python.org/downloads/ and reopen PowerShell." -ForegroundColor Yellow
    exit 2
}

$PythonPrefix = @()
if ($PythonCommand.Name -match '^py(\.exe)?$') {
    $PythonPrefix = @("-3")
}

$VersionProbe = @()
$VersionProbe += $PythonPrefix
$VersionProbe += @("-c", "import sys; raise SystemExit(0 if sys.version_info >= (3, 10) else 1)")
& $PythonCommand.Source @VersionProbe
if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] Python 3.10 or newer is required." -ForegroundColor Red
    exit 2
}

function Invoke-Assistant {
    param([string[]] $ArgumentsToPass)

    $CallArguments = @()
    $CallArguments += $PythonPrefix
    $CallArguments += $PythonScript
    $CallArguments += "--no-update-check"
    if ($ArgumentsToPass) {
        $CallArguments += $ArgumentsToPass
    }
    & $PythonCommand.Source @CallArguments
    return $LASTEXITCODE
}

if ($CliArguments -and $CliArguments.Count -gt 0) {
    exit (Invoke-Assistant -ArgumentsToPass $CliArguments)
}

# The native PowerShell dashboard does not enter Python's interactive menu.
# Check the app and Hardware Sniffer once when this dashboard starts.
$updateArgs = @()
$updateArgs += $PythonPrefix
$updateArgs += @($PythonScript, "check-updates")
$script:UpdateCheckOutput = @(& $PythonCommand.Source @updateArgs 2>&1)

function Write-Dashboard {
    Clear-Host
    foreach ($line in $script:UpdateCheckOutput) {
        Write-Host "  $line" -ForegroundColor DarkGray
    }
    if ($script:UpdateCheckOutput) { Write-Host "" }
    $versionArgs = @()
    $versionArgs += $PythonPrefix
    $versionArgs += @($PythonScript, "--version")
    $version = & $PythonCommand.Source @versionArgs 2>$null
    Write-Host ""
    Write-Host "  +======================================================================+" -ForegroundColor DarkCyan
    Write-Host "  |                  SIMPLE HACKINTOSH / POWERSHELL                     |" -ForegroundColor Cyan
    Write-Host "  +======================================================================+" -ForegroundColor DarkCyan
    Write-Host "  $version   |   PowerShell $($PSVersionTable.PSVersion)   |   $([Environment]::OSVersion.Platform)" -ForegroundColor DarkGray
    Write-Host ""
    Write-Host "  HARDWARE & DIAGNOSTICS" -ForegroundColor Yellow
    if ($env:OS -eq "Windows_NT") {
        Write-Host "   01  Hardware report             Export Report.json + macOS estimate"
    }
    else {
        Write-Host "   01  Hardware report             Native macOS system_profiler inventory"
    }
    Write-Host "   02  Scan EFI                    Inspect a folder or ZIP"
    Write-Host "   03  Validate config.plist       Check structure and common entry errors"
    Write-Host "   04  Run ocvalidate              Use the matching OpenCore validator"
    Write-Host ""
    Write-Host "  EFI WORKBENCH" -ForegroundColor Yellow
    Write-Host "   05  Compare EFI copies          Compare file lists and SHA-256 hashes"
    Write-Host "   06  Support report              Create a troubleshooting report"
    Write-Host "   08  Back up EFI                 Create a ZIP backup"
    Write-Host "   10  Create EFI starter          Start from an OpenCore release ZIP"
    Write-Host ""
    Write-Host "  RECOVERY & SYSTEM" -ForegroundColor Yellow
    Write-Host "   09  Download Apple Recovery     Download and verify Recovery files"
    Write-Host "   07  Program diagnostics         Check Python and available tools"
    Write-Host ""
    Write-Host "   00  Exit" -ForegroundColor Red
    Write-Host ""
}

while ($true) {
    Write-Dashboard
    $choice = Read-Host "  Select an option [00-10]"
    $result = 0

    switch ($choice.Trim()) {
        { $_ -in @("0", "00") } { Write-Host "Goodbye." -ForegroundColor Green; exit 0 }
        { $_ -in @("1", "01") } {
            if ($env:OS -eq "Windows_NT") {
                $outputDirectory = Read-Host "Report output folder [HardwareReport]"
                if (-not $outputDirectory) { $outputDirectory = "HardwareReport" }
                $result = Invoke-Assistant @("hardware-sniffer", "--output-dir", $outputDirectory)
            }
            else {
                $result = Invoke-Assistant @("hardware")
            }
        }
        { $_ -in @("2", "02") } {
            $path = Read-Host "EFI folder or ZIP path"
            if ($path) { $result = Invoke-Assistant @("efi-check", $path) }
        }
        { $_ -in @("3", "03") } {
            $path = Read-Host "Path to config.plist"
            if ($path) { $result = Invoke-Assistant @("config-check", $path) }
        }
        { $_ -in @("4", "04") } {
            $config = Read-Host "Path to config.plist"
            $tool = Read-Host "Path to matching ocvalidate executable"
            $expected = Read-Host "Expected OpenCore version (optional)"
            if ($config -and $tool) {
                $argsToPass = @("ocvalidate", $config, "--tool", $tool)
                if ($expected) { $argsToPass += @("--expect-version", $expected) }
                $result = Invoke-Assistant $argsToPass
            }
        }
        { $_ -in @("5", "05") } {
            $first = Read-Host "First EFI folder or ZIP"
            $second = Read-Host "Second EFI folder or ZIP"
            if ($first -and $second) { $result = Invoke-Assistant @("compare-efi", $first, $second) }
        }
        { $_ -in @("6", "06") } {
            $path = Read-Host "EFI folder or ZIP path"
            if ($path) { $result = Invoke-Assistant @("support-report", $path) }
        }
        { $_ -in @("7", "07") } { $result = Invoke-Assistant @("doctor") }
        { $_ -in @("8", "08") } {
            $path = Read-Host "EFI folder to back up"
            $output = Read-Host "Backup ZIP path [EFI-backup.zip]"
            if (-not $output) { $output = "EFI-backup.zip" }
            if ($path) { $result = Invoke-Assistant @("backup-efi", $path, "--output", $output) }
        }
        "9" {
            $output = Read-Host "Recovery output folder [com.apple.recovery.boot]"
            if (-not $output) { $output = "com.apple.recovery.boot" }
            $result = Invoke-Assistant @("macrecovery", "download", "--os-type", "latest", "--outdir", $output)
        }
        "09" {
            $output = Read-Host "Recovery output folder [com.apple.recovery.boot]"
            if (-not $output) { $output = "com.apple.recovery.boot" }
            $result = Invoke-Assistant @("macrecovery", "download", "--os-type", "latest", "--outdir", $output)
        }
        "10" {
            $zip = Read-Host "Path to official OpenCore release ZIP"
            $output = Read-Host "New EFI output folder [EFI]"
            if (-not $output) { $output = "EFI" }
            if ($zip) { $result = Invoke-Assistant @("create-efi", $zip, "--output", $output) }
        }
        default { Write-Host "  Invalid option. Choose 00 to exit." -ForegroundColor Red }
    }

    if ($choice.Trim() -notin @("0", "00")) {
        if ($result -ne 0) {
            Write-Host ""
            Write-Host "  Finished with exit code $result." -ForegroundColor Yellow
        }
        [void](Read-Host "  Press Enter to continue...")
    }
}
