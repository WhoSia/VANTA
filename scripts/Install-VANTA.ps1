[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Archive,
    [Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$ExpectedSha256,
    [Parameter(Mandatory)][ValidatePattern('^[0-9]+\.[0-9]+\.[0-9]+$')][string]$Version,
    [string]$InstallRoot = (Join-Path $env:LOCALAPPDATA 'VANTA\versions')
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

# User explicitly selects a trusted local archive, hash and destination root.
# Hash verification and extraction share one read-only handle. No network or elevation.
$root = [IO.Path]::GetFullPath($InstallRoot)
$destination = Join-Path $root $Version
if (Test-Path -LiteralPath $destination) { throw "Version already exists: $destination. Existing installations are not overwritten." }
$stage = $null
$stream = [IO.File]::Open([IO.Path]::GetFullPath($Archive),[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
try {
    if ($stream.Length -gt 64MB) { throw 'Archive exceeds the application-only package limit.' }
    $actual = (Get-FileHash -InputStream $stream -Algorithm SHA256).Hash
    if ($actual -ne $ExpectedSha256) { throw 'Archive SHA-256 mismatch. No installation performed.' }
    $stream.Position = 0
    $zip = [IO.Compression.ZipArchive]::new($stream,[IO.Compression.ZipArchiveMode]::Read,$true)
    try {
        if ($zip.Entries.Count -lt 1 -or $zip.Entries.Count -gt 4096) { throw 'Invalid archive entry count.' }
        $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        $expanded = 0L
        foreach ($entry in $zip.Entries) {
            $name = $entry.FullName
            $isDir = $name.EndsWith('/')
            $relative = $name.TrimEnd('/')
            if (-not $relative -or $name.Contains('\') -or $name.StartsWith('/') -or -not $seen.Add($relative)) { throw "Invalid or duplicate archive entry: $name" }
            foreach ($segment in ($relative -split '/')) {
                if ($segment -notmatch '^[A-Za-z0-9_. -]+$' -or $segment -in @('.','..') -or $segment.EndsWith('.') -or $segment.EndsWith(' ') -or $segment -match '^(CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])(\.|$)') {
                    throw "Unsafe archive entry: $name"
                }
            }
            if ($isDir -and $entry.Length -ne 0) { throw "Invalid directory entry: $name" }
            $expanded += $entry.Length
            if ($expanded -gt 128MB) { throw 'Expanded archive exceeds the application-only limit.' }
        }
        [IO.Directory]::CreateDirectory($root) | Out-Null
        $stage = Join-Path $root ('.vanta-install-' + [Guid]::NewGuid().ToString('N'))
        [IO.Directory]::CreateDirectory($stage) | Out-Null
        $prefix = [IO.Path]::GetFullPath($stage).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
        foreach ($entry in $zip.Entries) {
            $target = [IO.Path]::GetFullPath((Join-Path $stage $entry.FullName))
            if (-not $target.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)) { throw 'Archive entry escapes staging directory.' }
            if ($entry.FullName.EndsWith('/')) { [IO.Directory]::CreateDirectory($target) | Out-Null; continue }
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
            $source = $entry.Open()
            try {
                $out = [IO.File]::Open($target,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
                try {
                    # Copy data, never recreate archive symlinks or reparse points.
                    $buffer = New-Object byte[] 65536
                    $written = 0L
                    while (($n = $source.Read($buffer,0,$buffer.Length)) -gt 0) {
                        $written += $n
                        if ($written -gt $entry.Length) { throw 'Archive entry exceeds declared length.' }
                        $out.Write($buffer,0,$n)
                    }
                    if ($written -ne $entry.Length) { throw 'Archive entry length mismatch.' }
                } finally { $out.Dispose() }
            } finally { $source.Dispose() }
        }
        foreach ($required in @('Vanta.App.exe','Vanta.App.dll','Vanta.App.runtimeconfig.json','Vanta.App.deps.json','Vanta.Core.dll','Start-VANTA.ps1','package.json')) {
            if (-not (Test-Path -LiteralPath (Join-Path $stage $required) -PathType Leaf)) { throw "Incomplete package: missing $required" }
        }
        $manifest = Get-Content -LiteralPath (Join-Path $stage 'package.json') -Raw | ConvertFrom-Json
        if ($manifest.version -ne $Version -or $manifest.rid -ne 'win-x64' -or $manifest.commit -notmatch '^[a-f0-9]{40}$') { throw 'Package manifest does not match requested version/platform.' }
        # Same-parent directory rename is the publication point. Never replace a version.
        [IO.Directory]::Move($stage,$destination)
        $stage = $null
        Write-Host "Installed VANTA $Version to $destination"
        Write-Host 'Dependencies are checked separately by Start-VANTA.ps1. Previous versions remain untouched.'
    } finally { $zip.Dispose() }
} finally {
    $stream.Dispose()
    if ($stage -and (Test-Path -LiteralPath $stage)) { Remove-Item -LiteralPath $stage -Recurse -Force }
}
