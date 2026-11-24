[CmdletBinding()]
param([switch]$InstallMissing,[switch]$CheckOnly)
$ErrorActionPreference = 'Stop'
function Update-SearchPath {
    $machine = [Environment]::GetEnvironmentVariable('Path','Machine')
    $user = [Environment]::GetEnvironmentVariable('Path','User')
    $links = Join-Path $env:LOCALAPPDATA 'Microsoft\WinGet\Links'
    $env:Path = (@($env:Path,$machine,$user,$links) | Where-Object { $_ }) -join ';'
}
function Find-Runtime {
    $paths = @()
    if ($env:ProgramFiles) { $paths += Join-Path $env:ProgramFiles 'dotnet\dotnet.exe' }
    if ($env:ProgramW6432) { $paths += Join-Path $env:ProgramW6432 'dotnet\dotnet.exe' }
    foreach ($p in ($paths | Select-Object -Unique)) {
        if ((Test-Path -LiteralPath $p -PathType Leaf) -and ((& $p --list-runtimes) -match '^Microsoft\.WindowsDesktop\.App 10\.')) { return $true }
    }
    return $false
}
function Find-Tools {
    $dirs = @()
    if ($env:VANTA_FFMPEG_DIR) {
        if (-not [IO.Path]::IsPathRooted($env:VANTA_FFMPEG_DIR)) { throw 'VANTA_FFMPEG_DIR must be an absolute trusted directory.' }
        $dirs += $env:VANTA_FFMPEG_DIR
    } else {
        $dirs += Join-Path $PSScriptRoot 'tools\ffmpeg\bin'
        $dirs += @($env:Path -split ';' | ForEach-Object { $_.Trim().Trim('"') } | Where-Object { $_ -and [IO.Path]::IsPathRooted($_) })
    }
    foreach ($d in ($dirs | Select-Object -Unique)) {
        $ff = Join-Path $d 'ffmpeg.exe'
        $fp = Join-Path $d 'ffprobe.exe'
        if ((Test-Path -LiteralPath $ff -PathType Leaf) -and (Test-Path -LiteralPath $fp -PathType Leaf)) {
            # Application performs version and encoder checks; launcher checks presence only.
            return $d
        }
    }
    return $null
}
function Install-Package([string]$Id) {
    if (-not (Get-Command winget.exe -CommandType Application -ErrorAction SilentlyContinue)) { throw 'WinGet is not available. Install dependencies from their official sources.' }
    & winget.exe install --id $Id --exact --source winget
    if ($LASTEXITCODE -ne 0) { throw "WinGet installation failed for $Id (exit $LASTEXITCODE). Do not bypass hash checks." }
    Update-SearchPath
}
if ($CheckOnly -and $InstallMissing) { throw 'CheckOnly never installs dependencies.' }
Update-SearchPath
if (-not (Find-Runtime)) {
    if (-not $InstallMissing) { throw 'Missing .NET 10 Desktop Runtime. Run this script with -InstallMissing or install Microsoft.DotNet.DesktopRuntime.10 using WinGet.' }
    Install-Package 'Microsoft.DotNet.DesktopRuntime.10'
    if (-not (Find-Runtime)) { throw 'Runtime not detected. Reopen PowerShell and retry.' }
}
$dir = Find-Tools
if (-not $dir) {
    if ($env:VANTA_FFMPEG_DIR) { throw 'Configured VANTA_FFMPEG_DIR does not contain both executables. Correct or remove it.' }
    if (-not $InstallMissing) { throw 'FFmpeg and FFprobe not found. Run with -InstallMissing or install Gyan.FFmpeg using WinGet.' }
    Install-Package 'Gyan.FFmpeg'
    $dir = Find-Tools
    if (-not $dir) { throw 'FFmpeg not detected. Reopen PowerShell and retry.' }
}
$exe = Join-Path $PSScriptRoot 'Vanta.App.exe'
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw 'Vanta.App.exe is missing. Extract the complete preview ZIP.' }
Write-Host "Runtime and executable presence checks passed. FFmpeg directory: $dir"
if (-not $CheckOnly) {
    $env:VANTA_FFMPEG_DIR = $dir
    Start-Process -FilePath $exe -WorkingDirectory $PSScriptRoot
}
