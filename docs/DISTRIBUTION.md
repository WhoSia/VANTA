# Distribution strategy — 0.2.0 preview

Use the exact successful Windows CI artifact for development previews, not a source archive. CI stays read-only (no release publishing or bot commits). Artifacts require GitHub sign-in and expire after seven days. A preview is not a tagged release.

For a successful preview run: open Actions, download VANTA-win-x64-development, extract to a new user-owned directory, and launch Vanta.App.exe. Do not overwrite an existing installation. Windows x64 .NET 10 Desktop Runtime and FFmpeg/ffprobe are external shared dependencies. Check existing installations first. If missing, use winget official community manifests: Microsoft.DotNet.DesktopRuntime.10 and Gyan.FFmpeg. Reopen PowerShell after PATH changes. Do not bypass installer hash checks. No SDK needed on the user PC. Preview is unsigned; no instructions to bypass Windows security warnings.

Stable distribution proposal: owner-approved GitHub Release with versioned framework-dependent win-x64 ZIP, SHA-256, release notes and dependency instructions. Optionally provide a reviewed PowerShell installer downloading the exact version and checking the checksum, then installing per-user; later consider a WinGet portable package if justified. No one-line remote script execution, no global dependency duplication, no automatic release/tag until approved. CI builds and measures artifacts; release asset upload is a separate owner-authorized action. Do not store FFmpeg binaries in git.

Preview baseline ee4623e produced 126203 compressed bytes (application only). New GUI build 93531cbd is pending CI; do not advertise it as tested until the run succeeds. Real GUI interaction and installation still need Windows acceptance. Current support: 8-bit SDR, H.264/HEVC MP4 output, AAC audio copy or no audio, original frame timing. Single-pass size is an estimate. A busy indicator is not a percentage progress meter.

## Known successful preview

CI 45473261311, exact SHA 93531cbd, uploaded 133396-byte compressed artifact 14959205335: https://github.com/WhoSia/VANTA/actions/runs/45473261311#artifacts . It contains GUI encoding and editing. Do not confuse with newer unverified CI. The user can extract and launch with external dependencies already installed. New toolchain discovery checks same-directory FFmpeg/FFprobe, matching version strings and listed H.264/HEVC encoders. Optional VANTA_FFMPEG_DIR is an absolute trusted binary folder; no downloads from the app. Next packaging task: a local-archive per-user installer, verified archive hash, staged extraction, no overwrite, no remote execution, tested on Windows CI. Stable publishing remains owner-approved.

## Dependency failure observed on the owner's PC

Owner launched the 0.2.0 GUI successfully; screenshot showed ffprobe process could not be located. This establishes launch, not successful encoding. Exact installed artifact not independently identified; error resembles the older PATH-only preview. CI 45475251399 succeeded at 8104a55. New SHA 46a42cc supports a trusted portable pair under tools/ffmpeg/bin relative to the app, or explicit VANTA_FFMPEG_DIR, or PATH. The artifact includes Start-VANTA.ps1; -CheckOnly does not install or launch, -InstallMissing explicitly authorizes WinGet dependency installation. No script execution-policy override, no remote script execution, no unverified binaries, no FFmpeg binaries committed. CI exercises presence checks only; missing-package installation on a clean PC remains unverified. An optional bundled distribution may be considered after verifying applicable FFmpeg GPL source/license obligations (https://ffmpeg.org/legal.html) and actual dependency sizes. GitHub Release per-file limit is 2 GiB (https://docs.github.com/en/repositories/releasing-projects-on-github/about-releases); git source history is not the binary distribution channel.

## Current verified preview and desktop acceptance

Exact a2200f3 CI 45506058355 succeeded (artifact 14973692485, 176321 compressed bytes): https://github.com/WhoSia/VANTA/actions/runs/45506058355#artifacts . Supersedes historical preview links and pending-status notes above; GPU diagnostics available, no GPU compression. Owner previously reported FFmpeg/FFprobe 9.1 full build both execute. New 9458789 recommendation selection CI pending full integration. Do not infer interactive GUI acceptance from CI. See [desktop checklist](DESKTOP_ACCEPTANCE.md).

## Accepted 0.2.0 development preview

Exact 5a7f687dd548f067f43522cb72b20eabad0ec4d6, CI 45521060956, artifact 14980710817, 193397 compressed bytes: https://github.com/WhoSia/VANTA/actions/runs/45521060956#artifacts . This supersedes previous preview links. CI build, WPF control smoke, FFmpeg integration, publish and dependency launcher presence check passed. Artifact expiry remains seven days; no permanent Release or tag created. Clean-machine installation and interactive owner acceptance remain separate gates.
