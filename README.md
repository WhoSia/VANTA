# VANTA

**Video Adaptive Native Transcoding Architecture**

A lightweight, FFmpeg-powered video compressor with adaptive compression planning, granular encoding controls, and audio-video synchronization integrity.

## Status and download

0.3.0 — Desktop Reliability & Distribution Readiness (owner-approved, in development). No tagged release.

**[Download the verified Windows x64 package (325af59)](https://github.com/WhoSia/VANTA/actions/runs/45546909751/artifacts/14991847535).** The successful run provides only one user-facing package artifact. Extract both the downloaded ZIP and its inner application ZIP into a new folder, then run `Vanta.App.exe`. Optional dependency check: `./Start-VANTA.ps1 -CheckOnly` in the app directory. Requires Windows x64 .NET 10 Desktop Runtime and matching FFmpeg/FFprobe, not the SDK. Optional local installer refuses existing versions. New previews use separate folders. See [distribution](docs/DISTRIBUTION.md). Newer source may be unverified; consult [Actions](https://github.com/WhoSia/VANTA/actions). CI success does not establish interactive desktop acceptance.

## Current preview

- Single video or independent multi-video batch, up to two concurrent encodes.
- Source-aware bitrate-budget tiers, editable H.264/HEVC codec, preset, height, video bitrate and target-size budget. Quality is not measured by the recommendation heuristic and final size can differ substantially.
- AAC mono/stereo audio copy (or silent), 8-bit SDR yuv420p, known stream timing; MP4 output. Unsupported streams or metadata, including display rotation/matrices, are rejected. Not all MP4/MOV/MKV inputs meet these conditions.
- Collision-safe _vanta.mp4 output names, common output directory, per-file overrides, local batch defaults, cancellation, explicit retry, aggregate actual savings, result folder and diagnostic copy.
- Continuous encoding default. Optional bounded CFR H.264 video-only split experiment; not demonstrated faster in matched tests, no automatic selection. Original audio is mapped once.
- Optional encoder thread request (not a CPU cap); explicit synthetic hardware encoder diagnostics only. GPU compression and FPS conversion are not enabled.
- Per-job elapsed and validation time, full decode and relative A/V timestamp validation before publication. No overwrite, no telemetry.

## Invariants

Never overwrite the source or existing output. Do not publish until validation passes. Preserve relative stream timing and copied audio; experimental segmentation never relaxes these gates. Distinguish measured, estimated and unknown values. Prefer shared trusted FFmpeg and FFprobe; no silent download or execution of unverified binaries. Record upstream provenance and license obligations before redistribution.

## Development

Main-first, small auditable commits, read-only Windows CI, no bot-authored commits. Documentation-only changes do not trigger CI. Finite release scope; no version, tag or release advancement without owner approval.

[0.3.0 acceptance plan](docs/ACCEPTANCE_0.3.0.md) · [0.2.0 acceptance](docs/ACCEPTANCE_0.2.0.md) · [Development loop](docs/DEVELOPMENT_LOOP.md) · [Validation](docs/VALIDATION.md) · [Assessment](docs/ASSESSMENT.md) · [Release scope](docs/RELEASE_SCOPE.md) · [Acceleration](docs/ACCELERATION.md) · [Recommendation evidence](docs/RECOMMENDATION_EVIDENCE.md) · [Batch UX](docs/BATCH_UX.md) · [Source consistency](docs/SOURCE_INTEGRITY.md)
