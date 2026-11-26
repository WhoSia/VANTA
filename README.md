# VANTA

**Video Adaptive Native Transcoding Architecture**

A lightweight, FFmpeg-powered video compressor with adaptive compression planning, granular encoding controls, and audio-video synchronization integrity.

## Status and download

0.4.0 — Adaptive Segmented Encoding & Measured Acceleration (owner-approved 2026-11-25 KST; in development). No tagged release. Latest verified preview 6a4afb4 passes Windows CI 45868618491 (one nullable warning, zero errors), decision v5 and repeated synthetic acceleration cohort 45868618561 (two of three groups qualify). Public confirmation and preset frontier blocked by external acquisition. 0.3.0 build 457eb66 remains rollback reference; interactive owner acceptance remains open.

**[Download the verified Windows x64 package (6a4afb4)](https://github.com/WhoSia/VANTA/actions/runs/45868618491/artifacts/15142081767).** The successful run provides only one user-facing package artifact. Extract both the downloaded ZIP and its inner application ZIP into a new folder, then run `Vanta.App.exe`. Optional dependency check: `./Start-VANTA.ps1 -CheckOnly` in the app directory. Requires Windows x64 .NET 10 Desktop Runtime and matching FFmpeg/FFprobe, not the SDK. Optional local installer refuses existing versions. New previews use separate folders. See [distribution](docs/DISTRIBUTION.md). Newer source may be unverified; consult [Actions](https://github.com/WhoSia/VANTA/actions). CI success does not establish interactive desktop acceptance. See the [five-case owner checklist](docs/DESKTOP_ACCEPTANCE.md#030-owner-pc-minimal-handoff-not-executed).

## Current preview

- Single video or independent multi-video batch, up to two concurrent encodes.
- Source-aware bitrate-budget tiers, editable H.264/HEVC codec, preset, height, video bitrate and target-size budget. Quality is not measured by the recommendation heuristic and final size can differ substantially.
- AAC mono/stereo/5.1/7.1 audio copy (or silent; explicit supported surround layouts), 8-bit SDR yuv420p, known stream timing; MP4 output. Unsupported streams or metadata, including display rotation/matrices, are rejected. Not all MP4/MOV/MKV inputs meet these conditions.
- Collision-safe _vanta.mp4 output names, common output directory, per-file overrides, local batch defaults, cancellation, explicit retry, aggregate actual savings, result playback, persistent single-result metrics, result folder and diagnostic copy.
- Continuous encoding default. Optional bounded CFR H.264 video-only split experiment; no consistent speedup demonstrated in matched tests, no automatic selection. Original audio is mapped once.
- Optional encoder thread request (not a CPU cap); explicit synthetic hardware encoder diagnostics only. GPU compression and FPS conversion are not enabled.
- Conservative estimated local Windows scratch-space admission (not a quota).
- Per-job elapsed and validation time, full decode and relative A/V timestamp validation before publication. No overwrite, no telemetry.

## Invariants

Never overwrite the source or existing output. Do not publish until validation passes. Preserve relative stream timing and copied audio; experimental segmentation never relaxes these gates. Distinguish measured, estimated and unknown values. Prefer shared trusted FFmpeg and FFprobe; no silent download or execution of unverified binaries. Record upstream provenance and license obligations before redistribution.

## Development

Main-first, small auditable commits, read-only Windows CI, no bot-authored commits. Documentation-only changes do not trigger CI. Finite release scope; no version, tag or release advancement without owner approval.

[0.4.0 acceptance plan](docs/ACCEPTANCE_0.4.0.md) · [0.3.0 acceptance plan](docs/ACCEPTANCE_0.3.0.md) · [0.2.0 acceptance](docs/ACCEPTANCE_0.2.0.md) · [Development loop](docs/DEVELOPMENT_LOOP.md) · [Validation](docs/VALIDATION.md) · [Assessment](docs/ASSESSMENT.md) · [Release scope](docs/RELEASE_SCOPE.md) · [Acceleration](docs/ACCELERATION.md) · [Recommendation evidence](docs/RECOMMENDATION_EVIDENCE.md) · [Batch UX](docs/BATCH_UX.md) · [Source consistency](docs/SOURCE_INTEGRITY.md)

### Slow compression troubleshooting

Try H.264 with fast (or veryfast), automatic encoder threads and experimental split disabled; compare actual output size and visual quality. HEVC medium may be compute-heavy; no universal speed/quality guarantee. Newer source adds explicit Speed first shortcut and per-job encoding/validation timing plus applied settings in copied results; check exact CI before using its package. Hardware diagnostics do not enable GPU encoding. Report actual stage times and settings when investigating owner performance.

### Media scale coverage

Target: hundreds of MB through tens of GB, typical 1080p, common fractional/integer rates and VFR, higher resolutions. See [scale evidence and limitations](docs/research/media-scale/README.md). Local FFmpeg 11 rate checks, physical ~297MB stress input and sparse 5/20GiB chunk offsets passed; new exact C# Windows rate/physical and Linux offset lanes pending. Sparse files do not demonstrate physical large-media throughput. Current supported formats remain restricted; no claim of universal media support yet.
