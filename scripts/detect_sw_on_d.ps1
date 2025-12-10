# Check likely SolidWorks install path on D:\ based on registry hints
$baseCandidates = @(
    'D:\Program Files\SOLIDWORKS Corp\SOLIDWORKS',
    'D:\Program Files\SOLIDWORKS Corp',
    'D:\Program Files\SOLIDWORKS Corp\SOLIDWORKS 2024',
    'D:\Program Files\SOLIDWORKS Corp\SOLIDWORKS 2024\SOLIDWORKS'
)
foreach ($base in $baseCandidates) {
    $exe = Join-Path $base 'sldworks.exe'
    if (Test-Path $exe) { Write-Output "FOUND_EXE:$exe"; $api = Join-Path $base 'api\assemblies'; if (Test-Path $api) { Write-Output "FOUND_API:$api" } }
    else { Write-Output "NO:$exe" }
}

Write-Output 'Finished checking D:\ candidates.'
