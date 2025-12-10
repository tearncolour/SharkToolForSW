$api = 'D:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\api'
if (Test-Path $api) {
    Get-ChildItem -Path $api -Directory | ForEach-Object { Write-Output $_.FullName }
} else { Write-Output "API root not found: $api" }
