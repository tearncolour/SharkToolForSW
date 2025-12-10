# Detect SolidWorks installation path and API folder
$paths = @(
    'C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS',
    'C:\Program Files (x86)\SOLIDWORKS Corp\SOLIDWORKS',
    'C:\Program Files\SOLIDWORKS\SOLIDWORKS'
)

foreach ($p in $paths) {
    $f = Join-Path $p 'sldworks.exe'
    if (Test-Path $f) {
        Write-Output "FOUND_EXE:$f"
        $api = Join-Path $p 'api\assemblies'
        if (Test-Path $api) { Write-Output "FOUND_API:$api" }
    } else {
        Write-Output "NO:$p"
    }
}

# Registry App Paths
$regLocations = @(
    'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\sldworks.exe',
    'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\App Paths\sldworks.exe'
)
foreach ($rp in $regLocations) {
    try {
        $item = Get-ItemProperty -Path $rp -ErrorAction Stop
        if ($item.Path) {
            Write-Output "REG_APP_PATH:$($item.Path)"
            $base = Split-Path $item.Path
            $api = Join-Path $base 'api\assemblies'
            if (Test-Path $api) { Write-Output "REG_API:$api" }
        }
    } catch { Write-Output "No registry app path at $rp" }
}

# Try Mongo (SolidWorks registry keys)
$swKeys = @('HKLM:\SOFTWARE\SolidWorks','HKLM:\SOFTWARE\WOW6432Node\SolidWorks')
foreach ($sk in $swKeys) {
    if (Test-Path $sk) {
        Write-Output "REG-SOLIDWORKS: $sk"
        Get-ChildItem -Path $sk | ForEach-Object { Write-Output "SubKey: $($_.Name)" }
    }
}

# Search 'Program Files' for sldworks.exe if nothing found (fast search under Program Files only)
$pfPaths = @('C:\Program Files','C:\Program Files (x86)')
foreach ($base in $pfPaths) {
    try {
        $found = Get-ChildItem -Path $base -Filter 'sldworks.exe' -Recurse -ErrorAction SilentlyContinue -Force -Depth 3
        foreach ($f in $found) { Write-Output "FOUND_PROGRAMFILES:$($f.FullName)" }
    } catch { }
}

Write-Output "DETECTION-FINISHED"
