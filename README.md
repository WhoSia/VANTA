# VANTA

**Video Adaptive Native Transcoding Architecture**

A lightweight, FFmpeg-powered video compressor with adaptive compression planning, granular encoding controls, and audio-video synchronization integrity.

## Status and download

0.4.0 — Adaptive Segmented Encoding & Measured Acceleration (owner-approved 2026-11-25 KST; in development). No tagged release. Latest verified 0.4.0 development preview includes the second corpus harness d709217 (ordinary CI 45659393097; public corpus failed); 0.3.0 build 457eb66 remains rollback reference; interactive owner acceptance remains open.

**[Download the verified Windows x64 package (d709217)](https://github.com/WhoSia/VANTA/actions/runs/45659393097/artifacts/15045395150).** The successful run provides only one user-facing package artifact. Extract both the downloaded ZIP and its inner application ZIP into a new folder, then run `Vanta.App.exe`. Optional dependency check: `./Start-VANTA.ps1 -CheckOnly` in the app directory. Requires Windows x64 .NET 10 Desktop Runtime and matching FFmpeg/FFprobe, not the SDK. Optional local installer refuses existing versions. New previews use separate folders. See [distribution](docs/DISTRIBUTION.md). Newer source may be unverified; consult [Actions](https://github.com/WhoSia/VANTA/actions). CI success does not establish interactive desktop acceptance. See the [five-case owner checklist](docs/DESKTOP_ACCEPTANCE.md#030-owner-pc-minimal-handoff-not-executed).

## Current preview

- Single video or independent multi-video batch, up to two concurrent encodes.
- Source-aware bitrate-budget tiers, editable H.264/HEVC codec, preset, height, video bitrate and target-size budget. Quality is not measured by the recommendation heuristic and final size can differ substantially.
- AAC mono/stereo/5.1/7.1 audio copy (or silent; multichannel requires explicit supported layout), 8-bit SDR yuv420p, known stream timing; MP4 output. Unsupported streams or metadata, including display rotation/matrices, are rejected. Not all MP4/MOV/MKV inputs meet these conditions.
- Collision-safe _vanta.mp4 output names, common output directory, per-file overrides, local batch defaults, cancellation, explicit retry, aggregate actual savings, result folder and diagnostic copy.
- Continuous encoding default. Optional bounded CFR H.264 video-only split experiment; no consistent speedup demonstrated in matched tests, no automatic selection. Original audio is mapped once.
- Optional encoder thread request (not a CPU cap); explicit synthetic hardware encoder diagnostics only. GPU compression and FPS conversion are not enabled.
- Per-job elapsed and validation time, full decode and relative A/V timestamp validation before publication. No overwrite, no telemetry.

## Invariants

Never overwrite the source or existing output. Do not publish until validation passes. Preserve relative stream timing and copied audio; experimental segmentation never relaxes these gates. Distinguish measured, estimated and unknown values. Prefer shared trusted FFmpeg and FFprobe; no silent download or execution of unverified binaries. Record upstream provenance and license obligations before redistribution.

## Development

Main-first, small auditable commits, read-only Windows CI, no bot-authored commits. Documentation-only changes do not trigger CI. Finite release scope; no version, tag or release advancement without owner approval.

[0.4.0 acceptance plan](docs/ACCEPTANCE_0.4.0.md) · [0.3.0 acceptance plan](docs/ACCEPTANCE_0.3.0.md) · [0.2.0 acceptance](docs/ACCEPTANCE_0.2.0.md) · [Development loop](docs/DEVELOPMENT_LOOP.md) · [Validation](docs/VALIDATION.md) · [Assessment](docs/ASSESSMENT.md) · [Release scope](docs/RELEASE_SCOPE.md) · [Acceleration](docs/ACCELERATION.md) · [Recommendation evidence](docs/RECOMMENDATION_EVIDENCE.md) · [Batch UX](docs/BATCH_UX.md) · [Source consistency](docs/SOURCE_INTEGRITY.md)
