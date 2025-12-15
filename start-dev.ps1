# SharkTools Electron-App Dev Launcher
# Auto cleanup and start development environment

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "  SharkTools Dev Environment" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

# Cleanup processes
Write-Host "Cleaning up residual processes..." -ForegroundColor Yellow

$electronProcesses = Get-Process -Name "electron" -ErrorAction SilentlyContinue
if ($electronProcesses) {
    Write-Host "  - Terminating electron.exe..." -ForegroundColor Gray
    taskkill /F /IM electron.exe 2>$null | Out-Null
} else {
    Write-Host "  - No electron.exe found" -ForegroundColor Gray
}

$nodeProcesses = Get-Process -Name "node" -ErrorAction SilentlyContinue
if ($nodeProcesses) {
    Write-Host "  - Terminating node.exe..." -ForegroundColor Gray
    taskkill /F /IM node.exe 2>$null | Out-Null
} else {
    Write-Host "  - No node.exe found" -ForegroundColor Gray
}

Write-Host ""
Write-Host "Waiting for processes to terminate..." -ForegroundColor Yellow
Start-Sleep -Seconds 2

# Switch to electron-app directory
$electronAppPath = Join-Path $PSScriptRoot "electron-app"

if (Test-Path $electronAppPath) {
    Write-Host "Switching to electron-app directory..." -ForegroundColor Yellow
    Set-Location $electronAppPath
    Write-Host ""
    
    # Start development environment
    Write-Host "Starting development environment..." -ForegroundColor Green
    Write-Host "Press Ctrl+C to stop" -ForegroundColor Gray
    Write-Host ""
    
    npm run dev
} else {
    Write-Host "ERROR: Cannot find electron-app directory!" -ForegroundColor Red
    Write-Host "Please make sure this script is in the project root" -ForegroundColor Red
    pause
}