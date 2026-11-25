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
$exe = Join-Path $PSScriptRoot 'Vanta.App.exe'
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw 'Vanta.App.exe is missing. Extract the complete preview ZIP.' }
function Test-Tools {
    # Use the published app's own bounded matching-pair and encoder checks.
    $p = Start-Process -FilePath $exe -ArgumentList '--check-dependencies' -WorkingDirectory $PSScriptRoot -Wait -PassThru
    return ($p.ExitCode -eq 0)
}
if (-not (Test-Tools)) {
    if ($env:VANTA_FFMPEG_DIR) { throw 'Configured VANTA_FFMPEG_DIR is invalid or lacks a compatible matching FFmpeg/FFprobe pair. Correct or remove it; no fallback.' }
    if (-not $InstallMissing) { throw 'Compatible FFmpeg/FFprobe with H.264 or HEVC encoder not found. Run with -InstallMissing or install a trusted matching pair.' }
    Install-Package 'Gyan.FFmpeg'
    if (-not (Test-Tools)) { throw 'Compatible FFmpeg/FFprobe still unavailable. Reopen PowerShell and retry.' }
}
Write-Host 'Runtime and compatible FFmpeg/FFprobe checks passed.'
if (-not $CheckOnly) {
    Start-Process -FilePath $exe -WorkingDirectory $PSScriptRoot
}
