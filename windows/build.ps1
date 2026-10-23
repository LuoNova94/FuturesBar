param([string]$Runtime = "win-x64", [switch]$SkipChecks)
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot
if (-not $SkipChecks) {
    dotnet run --project FuturesBar.Checks -c Release
    if ($LASTEXITCODE -ne 0) { throw "Core checks failed." }
}
$publishDir = Join-Path $PSScriptRoot "../output/windows-publish/$Runtime"
$destination = Join-Path $PSScriptRoot "../output/windows/$Runtime"
dotnet publish FuturesBar.Windows/FuturesBar.Windows.csproj -c Release -r $Runtime --self-contained true -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "Windows publish failed." }
New-Item -ItemType Directory -Force $destination | Out-Null
Copy-Item (Join-Path $publishDir "FuturesBar.exe") $destination
Copy-Item "../LICENSE" $destination
Copy-Item "INSTALL.txt" $destination
$archive = Join-Path $PSScriptRoot "../output/FuturesBar-0.2.1-$Runtime-preview.zip"
Compress-Archive -Path (Join-Path $destination "FuturesBar.exe"),(Join-Path $destination "LICENSE"),(Join-Path $destination "INSTALL.txt") -DestinationPath $archive -Force
Get-FileHash $archive -Algorithm SHA256 | Format-List
Write-Host "Preview package: $archive"
