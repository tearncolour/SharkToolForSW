$files = @( 
    'D:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\api\redist\SolidWorks.Interop.sldworks.dll',
    'D:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\api\redist\SolidWorks.Interop.swconst.dll',
    'D:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\api\redist\SolidWorks.Interop.swpublished.dll'
)
foreach ($f in $files) {
    if (Test-Path $f) { Write-Output "EXISTS: $f" } else { Write-Output "MISSING: $f" }
}
