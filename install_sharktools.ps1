<#
Usage examples (run as Administrator):
  powershell -ExecutionPolicy Bypass -File scripts\install_sharktools.ps1
  powershell -ExecutionPolicy Bypass -File scripts\install_sharktools.ps1 -Configuration Release
Optional env var: SW_INSTALL_DIR overrides SolidWorks path.
This script builds SharkTools (Debug|x86 by default) and registers it via RegAsm.
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Debug",
    [string]$Platform = "x64",
    [string]$SolidWorksInstallDir
)

function Require-Admin {
    $current = [Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()
    if (-not $current.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw "Please run this script as Administrator (needed for regasm/registry)."
    }
}

function Resolve-SolidWorksPath {
    param([string]$InputPath)
    if ($InputPath) { return $InputPath }
    if ($env:SW_INSTALL_DIR) { return $env:SW_INSTALL_DIR }
    $candidates = @(
        'D:\Program Files\SOLIDWORKS Corp\SOLIDWORKS',
        'C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS',
        "$env:ProgramFiles\SOLIDWORKS Corp\SOLIDWORKS"
    )
    foreach ($c in $candidates) {
        if (Test-Path $c) { return $c }
    }
    return $null
}

try {
    Require-Admin

    $scriptDir = Split-Path -Parent $PSCommandPath
    $candidateRoots = @(
        $scriptDir,
        (Join-Path $scriptDir '..'),
        (Join-Path $scriptDir '..\..')
    )
    $solutionPath = $null
    $projectPath = $null
    foreach ($root in $candidateRoots) {
        $testSln = Join-Path $root 'sharktools\SharkTools.sln'
        $testProj = Join-Path $root 'sharktools\SharkTools.csproj'
        if (-not $solutionPath -and (Test-Path $testSln)) { $solutionPath = (Resolve-Path $testSln).Path }
        if (-not $projectPath -and (Test-Path $testProj)) { $projectPath = (Resolve-Path $testProj).Path }
    }
    if (-not $projectPath) { throw "Cannot locate sharktools/SharkTools.csproj relative to $scriptDir" }
    $projectDir = Split-Path $projectPath -Parent

    $swPath = Resolve-SolidWorksPath -InputPath $SolidWorksInstallDir
    if (-not $swPath) { throw "SolidWorks install path not found. Set -SolidWorksInstallDir or SW_INSTALL_DIR." }
    $interopDir = Join-Path $swPath 'api\redist'
    if (-not (Test-Path $interopDir)) { throw "Interop redist not found at $interopDir" }

    Write-Host "SolidWorks path: $swPath"
    Write-Host "Building SharkTools ($Configuration|$Platform)..."

    Push-Location $projectDir
    dotnet build $projectPath -c $Configuration -p:Platform=$Platform | Write-Host
    Pop-Location

    $outputDir = Join-Path $projectDir "bin\$Platform\$Configuration\net472"
    $dllPath = Join-Path $outputDir "SharkTools.dll"
    if (-not (Test-Path $dllPath)) { throw "Build output not found: $dllPath" }

    # Ensure SolidWorks interop assemblies are alongside the add-in for RegAsm probing
    $interopNames = @('SolidWorks.Interop.sldworks.dll','SolidWorks.Interop.swconst.dll','SolidWorks.Interop.swpublished.dll')
    foreach ($name in $interopNames) {
        $src = Join-Path $interopDir $name
        $dst = Join-Path $outputDir $name
        if (-not (Test-Path $src)) { throw "Missing interop file: $src" }
        Copy-Item -Force $src $dst
    }

    $regasm = if ($Platform -eq "x64") { "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe" } else { "C:\Windows\Microsoft.NET\Framework\v4.0.30319\RegAsm.exe" }
    if (-not (Test-Path $regasm)) { throw "RegAsm not found at $regasm" }

    $tlbPath = Join-Path $outputDir "SharkTools.tlb"
    Write-Host "Registering via RegAsm..."
    & $regasm $dllPath /codebase /tlb:$tlbPath | Write-Host

    Write-Host "Done. Enable SharkTools in SOLIDWORKS Add-Ins (Hello button on the toolbar)."
    Write-Host "Unregister with: `\"$regasm\" `\"$dllPath`\" /unregister"
}
catch {
    Write-Error $_
    exit 1
}
