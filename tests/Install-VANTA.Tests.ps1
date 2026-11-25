param(
    [Parameter(Mandatory)][string]$PackageDirectory,
    [Parameter(Mandatory)][string]$Version
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$package = [IO.Path]::GetFullPath($PackageDirectory)
$archive = Join-Path $package "VANTA-$Version-win-x64.zip"
$install = Join-Path $package 'Install-VANTA.ps1'
if (-not (Test-Path -LiteralPath (Join-Path $package 'START-HERE.txt') -PathType Leaf)) { throw 'Missing user instructions.' }
$hash = ((Get-Content -LiteralPath (Join-Path $package 'SHA256SUMS.txt') -Raw).Trim() -split '\s+')[0]
$root = Join-Path ([IO.Path]::GetTempPath()) ('VANTA-install-test-'+[Guid]::NewGuid().ToString('N'))
$dest = Join-Path $root $Version
function Expect-Failure([scriptblock]$Action,[string]$Label) {
    $failed = $false
    try { & $Action } catch { $failed = $true; Write-Host "$Label rejected: $($_.Exception.Message)" }
    if (-not $failed) { throw "$Label unexpectedly succeeded" }
}
try {
    Expect-Failure { & $install -Archive $archive -ExpectedSha256 ('0'*64) -Version $Version -InstallRoot $root } 'Wrong hash'
    if (Test-Path -LiteralPath $dest) { throw 'Wrong hash published output.' }
    & $install -Archive $archive -ExpectedSha256 $hash -Version $Version -InstallRoot $root
    $app = Join-Path $dest 'Vanta.App.exe'
    if (-not (Test-Path -LiteralPath $app -PathType Leaf)) { throw 'Install did not publish app.' }
    $before = (Get-FileHash -LiteralPath $app).Hash
    Expect-Failure { & $install -Archive $archive -ExpectedSha256 $hash -Version $Version -InstallRoot $root } 'Existing version'
    if ((Get-FileHash -LiteralPath $app).Hash -ne $before) { throw 'Existing app changed.' }
    Expect-Failure { & $install -Archive $archive -ExpectedSha256 $hash -Version '99.99.99' -InstallRoot $root } 'Manifest version mismatch'
    if (Test-Path -LiteralPath (Join-Path $root '99.99.99')) { throw 'Wrong manifest published output.' }
    $bad = Join-Path $root 'traversal.zip'
    $zip = [IO.Compression.ZipFile]::Open($bad,[IO.Compression.ZipArchiveMode]::Create)
    try { $entry = $zip.CreateEntry('../escape.txt'); $s = $entry.Open(); $s.Dispose() } finally { $zip.Dispose() }
    $badHash = (Get-FileHash -LiteralPath $bad).Hash
    Expect-Failure { & $install -Archive $bad -ExpectedSha256 $badHash -Version '98.98.98' -InstallRoot $root } 'Traversal'
    if ((Test-Path -LiteralPath (Join-Path $root 'escape.txt')) -or (Test-Path -LiteralPath (Join-Path $root '98.98.98'))) { throw 'Traversal published output.' }
    if (Get-ChildItem -LiteralPath $root -Force -Filter '.vanta-install-*') { throw 'Staging leak.' }
    & (Join-Path $dest 'Start-VANTA.ps1') -CheckOnly
    Write-Host 'Verified package installation, hash, collision, manifest and traversal regressions passed.'
} finally {
    if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
}
