$api = 'D:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\api\assemblies'
if (Test-Path $api) {
    Write-Output "Found API folder: $api"
    Get-ChildItem -Path $api -Filter '*.dll' | Select-Object -First 20 | ForEach-Object { Write-Output $_.Name }
} else {
    Write-Output "API folder not found: $api"
}