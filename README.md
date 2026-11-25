# VANTA

**Video Adaptive Native Transcoding Architecture**

A lightweight, FFmpeg-powered video compressor with adaptive compression planning, granular encoding controls, and audio-video synchronization integrity.

## Status and download

0.2.0 — Adaptive Compression Planning & Granular Control (core/development preview accepted; next version awaiting approval). No tagged release.

Latest fully verified preview at this documentation revision: [Windows x64 CI artifact (5a7f687)](https://github.com/WhoSia/VANTA/actions/runs/45521060956#artifacts), 193397 compressed bytes, excluding shared dependencies. Newer source may be unverified; consult [Actions](https://github.com/WhoSia/VANTA/actions) and the exact build SHA. Extract the development ZIP into a new directory and launch Vanta.App.exe. Requires Windows x64 .NET 10 Desktop Runtime and a matching FFmpeg/FFprobe pair. See [distribution](docs/DISTRIBUTION.md). CI success does not establish interactive desktop acceptance.

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

[0.2.0 acceptance](docs/ACCEPTANCE_0.2.0.md) · [Development loop](docs/DEVELOPMENT_LOOP.md) · [Validation](docs/VALIDATION.md) · [Assessment](docs/ASSESSMENT.md) · [Release scope](docs/RELEASE_SCOPE.md) · [Acceleration](docs/ACCELERATION.md) · [Recommendation evidence](docs/RECOMMENDATION_EVIDENCE.md) · [Batch UX](docs/BATCH_UX.md) · [Source consistency](docs/SOURCE_INTEGRITY.md)
