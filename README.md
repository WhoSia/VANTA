# VANTA

**Video Adaptive Native Transcoding Architecture**

A lightweight, FFmpeg-powered video compressor with adaptive compression planning, granular encoding controls, and audio-video synchronization integrity.

## Status

0.1.0 — Lightweight Compression Core & Temporal Integrity.

Core acceptance passed at `7815ef5` ([Windows CI #4](https://github.com/WhoSia/VANTA/actions/runs/45470160397)). Awaiting owner approval for the next version. Not a public release. The GUI is an analysis-only prototype; compression is verified through integration tests, not yet enabled in the GUI.

## Scope

Windows desktop application in C# and WPF. Intended final workflow: drag and drop video, inspect source streams, compare adaptive compression plans, customize supported encoding parameters, estimate output size, encode and validate locally.

Current tested core: single continuous HEVC MP4 encode, compatible AAC audio stream copy (or no audio), 8-bit SDR yuv420p, frame timing preservation, guarded output publication and cancellation. Unsupported streams are rejected rather than silently lost.

## Invariants

- Never overwrite the source.
- No chunked final encoding or concatenation for speed.
- Preserve relative stream timing; validate before publishing output.
- Distinguish measured, estimated and unknown values.
- Reject unsupported codec/encoder/container combinations.
- Prefer shared FFmpeg and ffprobe binaries; do not silently download or execute unverified binaries.
- Evaluate feasible plans as vectors, Pareto-filter them, and apply profile-specific nonlinear preferences only where justified.
- Record upstream provenance and license obligations before incorporating third-party code.

## Development

Main-first, small auditable commits. Read-only Windows CI. No automated bot-authored commits. Documentation-only changes do not trigger Windows CI. Windows acceptance and regression tests are required before final release.

[Development loop](docs/DEVELOPMENT_LOOP.md) · [Validation](docs/VALIDATION.md) · [Assessment](docs/ASSESSMENT.md) · [Release scope](docs/RELEASE_SCOPE.md)
