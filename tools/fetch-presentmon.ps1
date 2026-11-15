<#
  Downloads the pinned PresentMon console build (MIT, Intel) from its official GitHub release
  and refuses to use it unless the SHA-256 matches. Bump both values together after testing.
#>
param([string] $Out = "dist\publish")
$ErrorActionPreference = "Stop"
$version = "2.6.0"
$asset = "PresentMon-$version-x64.exe"
$sha256 = "B2A706BC6AD475749E3B7E3409263AA1E6906D45BDCF993F6DBC0F660188F1AF"   # Intel-signed release asset, checked on download
$url = "https://github.com/GameTechDev/PresentMon/releases/download/v$version/$asset"

New-Item -ItemType Directory -Force $Out | Out-Null
$dest = Join-Path $Out "PresentMon.exe"
Invoke-WebRequest $url -OutFile $dest
$actual = (Get-FileHash $dest -Algorithm SHA256).Hash
if ($sha256 -eq "") {
  Remove-Item $dest
  throw "PresentMon hash isn't pinned yet. Downloaded $asset has SHA-256 $actual. Check it against the release, then set `$sha256 in tools/fetch-presentmon.ps1."
}
if ($actual -ne $sha256) { Remove-Item $dest; throw "PresentMon hash mismatch: got $actual, expected $sha256" }
Invoke-WebRequest "https://raw.githubusercontent.com/GameTechDev/PresentMon/v$version/LICENSE.txt" -OutFile (Join-Path $Out "PresentMon-LICENSE.txt")
Write-Host "PresentMon $version ready ($actual)"
