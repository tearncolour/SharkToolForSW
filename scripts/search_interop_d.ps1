$base = 'D:\Program Files\SOLIDWORKS Corp'
if (Test-Path $base) {
    Write-Output "Searching for SolidWorks.Interop.sldworks.dll under $base..."
    $found = Get-ChildItem -Path $base -Filter 'SolidWorks.Interop.sldworks.dll' -Recurse -ErrorAction SilentlyContinue -Force
    foreach ($f in $found) { Write-Output $f.FullName }
} else { Write-Output 'Base not found: ' + $base }
