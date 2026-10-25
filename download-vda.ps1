$ErrorActionPreference = 'Stop'
$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
New-Item -ItemType Directory -Force -Path $dir | Out-Null
$rel = Invoke-RestMethod -Uri 'https://api.github.com/repos/Ciantic/VirtualDesktopAccessor/releases/latest' -Headers @{ 'User-Agent' = 'VD-X' }
$asset = $rel.assets | Where-Object { $_.name -match '\.dll$' } | Select-Object -First 1
if (-not $asset) { throw 'No DLL asset in latest release' }
$out = Join-Path $dir 'VirtualDesktopAccessor.dll'
Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $out -UseBasicParsing
Get-Item $out | Format-List Name, Length, LastWriteTime
