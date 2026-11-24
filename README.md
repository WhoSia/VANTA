# VANTA

**Video Adaptive Native Transcoding Architecture**

A lightweight, FFmpeg-powered video compressor with adaptive compression planning, granular encoding controls, and audio-video synchronization integrity.

## Status

0.1.0 — Lightweight Compression Core & Temporal Integrity (development; not released).

## Scope

Windows desktop application in C# and WPF. Drag and drop video, inspect source streams, compare three adaptive compression plans, customize encoding parameters, estimate output size, encode and validate locally.

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

Main-first development; small auditable commits. No automated bot-authored commits. Windows acceptance and regression tests are required before release.
