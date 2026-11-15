<#
  Publishes Framecalm.exe and Framecalm.ElevatedHelper.exe as self-contained single files,
  and puts the pinned PresentMon next to them. Used by the release workflow and build-msix.ps1.
#>
param([string] $Out = "dist\publish", [string] $Version = "0.1.0")
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$common = @("-c", "Release", "-r", "win-x64", "--self-contained", "-p:PublishSingleFile=true",
  "-p:IncludeNativeLibrariesForSelfExtract=true", "-p:EnableCompressionInSingleFile=true",
  "-p:DebugType=none", "-p:Version=$Version", "-o", $Out)
dotnet publish "$root\src\Framecalm.App" @common
if ($LASTEXITCODE -ne 0) { throw "publish failed (app)" }
dotnet publish "$root\src\Framecalm.ElevatedHelper" @common
if ($LASTEXITCODE -ne 0) { throw "publish failed (helper)" }
& "$PSScriptRoot\fetch-presentmon.ps1" -Out $Out
Get-ChildItem $Out -Filter *.pdb | Remove-Item
