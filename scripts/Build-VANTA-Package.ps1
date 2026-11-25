[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PublishedDirectory,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [Parameter(Mandatory)][ValidatePattern('^[0-9]+\.[0-9]+\.[0-9]+$')][string]$Version,
    [Parameter(Mandatory)][ValidatePattern('^[a-f0-9]{40}$')][string]$Commit
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$source = [IO.Path]::GetFullPath($PublishedDirectory)
$destination = [IO.Path]::GetFullPath($OutputDirectory)
if ($destination.StartsWith($source.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase) -or $destination -eq $source) { throw 'Package output must be outside the published application directory.' }
$exe = Join-Path $source 'Vanta.App.exe'
$dll = Join-Path $source 'Vanta.App.dll'
if (-not (Test-Path -LiteralPath $exe -PathType Leaf) -or -not (Test-Path -LiteralPath $dll -PathType Leaf)) { throw 'Published app is missing.' }
if ([Reflection.AssemblyName]::GetAssemblyName($dll).Version.ToString(3) -ne $Version) { throw 'Package version differs from application assembly.' }
if (Test-Path -LiteralPath $destination) { throw 'Package output directory already exists.' }
$manifest = [ordered]@{ version=$Version; rid='win-x64'; commit=$Commit; deployment='framework-dependent'; externalDependencies=@('.NET 10 Desktop Runtime','FFmpeg and FFprobe') }
$manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $source 'package.json') -Encoding UTF8
[IO.Directory]::CreateDirectory($destination) | Out-Null
try {
    $name = "VANTA-$Version-win-x64.zip"
    $zip = Join-Path $destination $name
    [IO.Compression.ZipFile]::CreateFromDirectory($source,$zip,[IO.Compression.CompressionLevel]::Optimal,$false)
    $hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $name" | Set-Content -LiteralPath (Join-Path $destination 'SHA256SUMS.txt') -Encoding ASCII
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Install-VANTA.ps1') -Destination $destination
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'START-HERE.txt') -Destination $destination
    Write-Host "PACKAGE $name $((Get-Item -LiteralPath $zip).Length) bytes SHA256=$hash"
} catch {
    Remove-Item -LiteralPath $destination -Recurse -Force
    throw
}
