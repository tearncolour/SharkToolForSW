#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Builds and registers the SharkTools SOLIDWORKS add-in.
.DESCRIPTION
    This script automates the following process:
    1. Ensures it's running with Administrator privileges.
    2. Forcefully closes SOLIDWORKS to unlock files.
    3. Builds the C# add-in project (SharkTools.csproj).
    4. Finds the .NET Framework 4.x RegAsm.exe tool.
    5. Un-registers the old version of the add-in DLL.
    6. Registers the newly built DLL using the /codebase flag.
    7. Provides detailed log output.
.PARAMETER Configuration
    Specifies the build configuration, either 'Debug' or 'Release'. Defaults to 'Debug'.
#>
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

# --- Script Initialization ---
$PSScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
Set-Location $PSScriptRoot

function Write-Log {
    param (
        [string]$Message,
        [string]$Color = "White"
    )
    Write-Host "[$([DateTime]::Now.ToString('HH:mm:ss'))] $Message" -ForegroundColor $Color
}

# --- 1. Privilege Check (Handled by #Requires -RunAsAdministrator) ---
Write-Log "Running with Administrator privileges." -Color Green

# --- 2. Define Paths and Variables ---
$projectName = "SharkTools"
$projectDir = Join-Path $PSScriptRoot "sharktools"
$projectFile = Join-Path $projectDir "$projectName.csproj"
$targetFramework = "net472"
$platform = "x64"

$buildOutputDir = Join-Path $projectDir "bin\$platform\$Configuration\$targetFramework"
$dllName = "$projectName.dll"
$dllPath = Join-Path $buildOutputDir $dllName

Write-Log "Project Path: $projectFile"
Write-Log "Target DLL: $dllPath"

# --- 3. Force Close SOLIDWORKS ---
Write-Log "Attempting to close SOLIDWORKS (SLDWORKS.exe)..."
$swProcess = Get-Process -Name "SLDWORKS" -ErrorAction SilentlyContinue
if ($swProcess) {
    Stop-Process -Name "SLDWORKS" -Force
    Write-Log "SOLIDWORKS process was terminated." -Color Yellow
} else {
    Write-Log "SOLIDWORKS is not running."
}

# --- 4. Build C# Project ---
Write-Log "Building project (Configuration: $Configuration, Platform: $platform)..."
dotnet build $projectFile -c $Configuration -p:Platform=$platform
if ($LASTEXITCODE -ne 0) {
    Write-Log "Project build failed! Check the build errors." -Color Red
    exit 1
}
Write-Log "Project built successfully." -Color Green

if (-not (Test-Path $dllPath)) {
    Write-Log "ERROR: Built DLL not found at the expected path: $dllPath" -Color Red
    exit 1
}

# --- 5. Find RegAsm.exe ---
Write-Log "Locating RegAsm.exe..."
$regAsmPath = Get-ChildItem -Path "$env:windir\Microsoft.NET\Framework64" -Filter "RegAsm.exe" -Recurse | Where-Object { $_.DirectoryName -like "*v4.0*" } | Select-Object -Last 1 | ForEach-Object { $_.FullName }

if (-not $regAsmPath) {
    Write-Log "ERROR: .NET Framework 4.x (64-bit) RegAsm.exe not found." -Color Red
    Write-Log "Please ensure the .NET Framework 4.7.2 (or later) Developer Pack is installed." -Color Yellow
    exit 1
}
Write-Log "RegAsm.exe found at: $regAsmPath"

# --- 6. Un-register and Re-register DLL ---
Write-Log "Un-registering old version (if it exists)..."
& $regAsmPath /unregister $dllPath | Out-Null # Suppress output, ignore errors if not registered

Write-Log "Registering new version..."
& $regAsmPath /codebase $dllPath
if ($LASTEXITCODE -ne 0) {
    Write-Log "Add-in registration failed!" -Color Red
    Write-Log "Ensure all dependencies are in the output directory and check the RegAsm output." -Color Yellow
    exit 1
}

Write-Log "--------------------------------------------------" -Color Cyan
Write-Log "         SharkTools Add-in built and registered successfully!" -Color Green
Write-Log "--------------------------------------------------" -Color Cyan
Write-Log "You can now start SOLIDWORKS and test the add-in."
