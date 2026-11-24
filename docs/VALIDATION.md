# Validation status — 0.1.0

**Core acceptance: PASS; owner version approval pending.** Exact tested source SHA: `7815ef54b7579599f3b8f419a3fd615be5f2ced1`. [Windows CI #4](https://github.com/WhoSia/VANTA/actions/runs/45470160397) succeeded on 2026-11-25 KST.

| Gate | Evidence |
|---|---|
| Windows WPF build | 0 errors, 0 warnings |
| Core smoke tests | Passed |
| Continuous FFmpeg HEVC encoding | Passed with FFmpeg 9.1 essentials |
| Original file integrity and no overwrite | Passed, SHA-256 and collision checks |
| Relative stream timing and audio payload | Passed |
| Constant 20 ms relative audio offset | Rejected as required |
| VFR and two offset audio streams | Passed |
| 120-second 30000/1001 fps fixture | Passed |
| Silent video and scaling | Passed |
| Unicode path and active/pre-cancellation | Passed; temporary file cleanup checked |
| Development artifact | Uploaded, 125043 compressed bytes; excludes .NET runtime and FFmpeg |

Earlier baseline `df3161e` passed [CI #3](https://github.com/WhoSia/VANTA/actions/runs/45468987261). The independently normalized packet-timeline counterexample was fixed by sharing the first video frame shift across streams, with a 2 ms tolerance. CI #4 confirms the negative regression.

Supported core domain: one 8-bit SDR yuv420p video, even dimensions, mono/stereo AAC with known timing and bitrate or no audio, no extra streams, HEVC MP4 output, audio copy, frame-rate passthrough, single pass. No guarantee of perceptual sync, hour-scale behavior, every format or interactive desktop acceptance. GUI remains analysis-only. This is a core milestone, not a finished application.

Subsequent documentation-only changes do not change tested source bytes; CI policy changes require separate operational verification. No new version, release or tag without owner approval.
