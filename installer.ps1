param(
    [Parameter(Mandatory = $true)]
    [string]$SoftwareName
)

# ----- SETTINGS -----
$registryUrl = "https://daniel-packageinstaller.pages.dev/software_registry.json"  # <-- Replace with registry URL, please use neit global when possible
$scriptDir = $PSScriptRoot
$registryPath = "$scriptDir\software_registry.json"
$logDir = "$scriptDir\logs"
$logFile = "$logDir\install_log.txt"

# Create logs directory if it doesn't exist
if (-not (Test-Path $logDir)) {
    New-Item -Path $logDir -ItemType Directory | Out-Null
}

function Write-Log {
    param (
        [string]$Message
    )
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Add-Content -Path $logFile -Value "$timestamp - $Message"
}

# ----- Step 1: Always download latest registry -----
Write-Host "Downloading latest software registry..." -ForegroundColor Cyan
try {
    Invoke-WebRequest -Uri $registryUrl -OutFile $registryPath -ErrorAction Stop
    Write-Host "Registry downloaded to $registryPath" -ForegroundColor Green
} catch {
    Write-Error "Failed to download registry file from $registryUrl"
    Write-Log "ERROR: Failed to download registry file from $registryUrl"
    exit 1
}

# ----- Step 2: Load registry and search for the software -----
$registry = Get-Content -Raw -Path $registryPath | ConvertFrom-Json
$software = $registry | Where-Object { $_.name -ieq $SoftwareName }

if (-not $software) {
    Write-Error "Software '$SoftwareName' not found in registry."
    Write-Log "ERROR: '$SoftwareName' not found in registry."
    exit 1
}

# ----- Step 3: Get currently installed version -----
function Get-InstalledVersion($name) {
    $apps = Get-ItemProperty -Path "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*" -ErrorAction SilentlyContinue |
        Where-Object { $_.DisplayName -eq $name }

    if (-not $apps) {
        $apps = Get-ItemProperty -Path "HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*" -ErrorAction SilentlyContinue |
            Where-Object { $_.DisplayName -eq $name }
    }

    return $apps.DisplayVersion
}

$installedVersion = Get-InstalledVersion $SoftwareName

if ($installedVersion) {
    if ($installedVersion -eq $software.version) {
        Write-Host "$SoftwareName is already up to date (version $installedVersion)." -ForegroundColor Yellow
        Write-Log "$SoftwareName is already up to date. Version: $installedVersion"
        exit 0
    } else {
        Write-Host "$SoftwareName installed version is $installedVersion, updating to $($software.version)..." -ForegroundColor Cyan
        Write-Log "Updating $SoftwareName from version $installedVersion to $($software.version)"
    }
} else {
    Write-Host "$SoftwareName is not installed. Installing version $($software.version)..." -ForegroundColor Cyan
    Write-Log "Installing $SoftwareName version $($software.version)"
}

# ----- Step 4: Download installer -----
$fileExtension = [System.IO.Path]::GetExtension($software.downloadUrl)
$tempInstallerPath = "$env:TEMP\$SoftwareName-installer$fileExtension"

try {
    Write-Host "Downloading installer from $($software.downloadUrl)..." -ForegroundColor Cyan
    Invoke-WebRequest -Uri $software.downloadUrl -OutFile $tempInstallerPath -ErrorAction Stop
    Write-Host "Downloaded to $tempInstallerPath" -ForegroundColor Green
    Write-Log "Downloaded $fileExtension installer for $SoftwareName"
} catch {
    Write-Error "Failed to download installer."
    Write-Log "ERROR: Failed to download installer for $SoftwareName."
    exit 1
}

# ----- Step 5: Install based on installer type -----
try {
    if ($fileExtension -ieq ".msi") {
        Write-Host "Installing MSI..." -ForegroundColor Cyan
        $arguments = "/i `"$tempInstallerPath`" $($software.installSwitch)"
        Start-Process -FilePath "msiexec.exe" -ArgumentList $arguments -Wait -ErrorAction Stop
        Write-Log "$SoftwareName MSI installed successfully. Version: $($software.version)"
    } elseif ($fileExtension -ieq ".exe") {
        Write-Host "Installing EXE..." -ForegroundColor Cyan
        Start-Process -FilePath $tempInstallerPath -ArgumentList $software.installSwitch -Wait -ErrorAction Stop
        Write-Log "$SoftwareName EXE installed successfully. Version: $($software.version)"
    } else {
        Write-Error "Unknown installer type: $fileExtension"
        Write-Log "ERROR: Unknown installer type for $SoftwareName ($fileExtension)"
        exit 1
    }

    Write-Host "$SoftwareName installation completed." -ForegroundColor Green
} catch {
    Write-Error "Failed to run installer."
    Write-Log "ERROR: Failed to run $fileExtension installer for $SoftwareName."
    exit 1
}

# ----- Step 6: Cleanup -----
Remove-Item $tempInstallerPath -Force -ErrorAction SilentlyContinue
