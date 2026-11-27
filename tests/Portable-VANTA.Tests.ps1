param(
    [Parameter(Mandatory)][string]$PublishedDirectory,
    [Parameter(Mandatory)][string]$PackageDirectory,
    [Parameter(Mandatory)][string]$Version,
    [Parameter(Mandatory)][string]$Commit
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$source = [IO.Path]::GetFullPath($PublishedDirectory)
$legacy = Join-Path ([IO.Path]::GetFullPath($PackageDirectory)) "VANTA-$Version-win-x64.zip"
$root = Join-Path ([IO.Path]::GetTempPath()) ('VANTA-portable-test-' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($root) | Out-Null
try {
    # Reproduce the artifact's single ZIP boundary without modifying the tested app.
    $archive = Join-Path $root 'download.zip'
    $dest = Join-Path $root 'extracted'
    [IO.Compression.ZipFile]::CreateFromDirectory($source,$archive,[IO.Compression.CompressionLevel]::Optimal,$false)
    [IO.Compression.ZipFile]::ExtractToDirectory($archive,$dest)
    foreach ($required in @('Vanta.App.exe','Vanta.App.dll','Vanta.App.runtimeconfig.json','Vanta.App.deps.json','Vanta.Core.dll','Start-VANTA.ps1','package.json','START-HERE.txt')) {
        if (-not (Test-Path -LiteralPath (Join-Path $dest $required) -PathType Leaf)) { throw "Missing root file: $required" }
    }
    if (Get-ChildItem -LiteralPath $dest -Recurse -File -Filter '*.zip') { throw 'Nested archive in portable download.' }
    $manifest = Get-Content -LiteralPath (Join-Path $dest 'package.json') -Raw | ConvertFrom-Json
    if ($manifest.version -ne $Version -or $manifest.commit -ne $Commit -or $manifest.rid -ne 'win-x64') { throw 'Wrong portable build identity.' }
    $zip = [IO.Compression.ZipFile]::OpenRead($legacy)
    try {
        foreach ($entry in $zip.Entries) {
            if ($entry.FullName.EndsWith('/')) { continue }
            $file = Join-Path $dest $entry.FullName
            if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { throw "Portable file missing: $($entry.FullName)" }
            $s = $entry.Open()
            try { $expected = (Get-FileHash -InputStream $s -Algorithm SHA256).Hash }
            finally { $s.Dispose() }
            if ((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $expected) { throw "Portable file changed: $($entry.FullName)" }
        }
    } finally { $zip.Dispose() }
    & (Join-Path $dest 'Start-VANTA.ps1') -CheckOnly
    Write-Host 'Single extraction, exact commit, byte identity, no nested ZIP and dependency launcher passed.'
} finally {
    Remove-Item -LiteralPath $root -Recurse -Force
}
