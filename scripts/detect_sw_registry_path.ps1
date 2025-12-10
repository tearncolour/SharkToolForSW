$swKeys = @('HKLM:\SOFTWARE\SolidWorks', 'HKLM:\SOFTWARE\WOW6432Node\SolidWorks')
$found = $false
foreach ($k in $swKeys) {
    if (Test-Path $k) {
        foreach ($sub in Get-ChildItem -Path $k -Recurse -ErrorAction SilentlyContinue) {
            try {
                $props = Get-ItemProperty -Path $sub.PSPath -ErrorAction Stop
                foreach ($p in $props.PSObject.Properties) {
                    if ($p.Name -match 'Install|Path|Program|Product|Location') {
                        Write-Output "$($sub.PSPath) >> $($p.Name) = $($p.Value)"
                        $found = $true
                    }
                }
            } catch {}
        }
    }
}
if(-not $found) { Write-Output 'No matching registry properties found.' }
