# Validation status — 0.2.0

**0.1.0 core acceptance: PASS; 0.2.0 approved and in development.** Exact tested source SHA: `7815ef54b7579599f3b8f419a3fd615be5f2ced1`. [Windows CI #4](https://github.com/WhoSia/VANTA/actions/runs/45470160397) succeeded on 2026-11-25 KST.

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

## 0.2.0 current evidence

CI policy change `6ca2b55` passed [CI #5](https://github.com/WhoSia/VANTA/actions/runs/45471166925). The 0.2.0 source revision `ee4623eb9ab81e9510951bddf1c9538430704750` changes the plan validator, target bitrate budget, source-aware heuristic and H.264/HEVC encoding options; [Windows CI](https://github.com/WhoSia/VANTA/actions/runs/45472107616) is pending. The version-only run was superseded and is not evidence. Documentation commits do not modify those build inputs. No measured quality score, exact size guarantee or FPS conversion support yet.

## Preview development

Exact core SHA ee4623eb9ab81e9510951bddf1c9538430704750 passed [CI 45472107616](https://github.com/WhoSia/VANTA/actions/runs/45472107616): zero build warnings/errors, smoke budget and rules tests, H.264 fast preset, prior HEVC and timeline tests, artifact 14958745346 (126203 compressed bytes). New GUI SHA 93531cbd01b45c158c83aa395cab3f57e5303480 adds editable settings, target input, candidate evidence and guarded encoding via existing validated core. [CI 45473261311](https://github.com/WhoSia/VANTA/actions/runs/45473261311) pending. User requested an installable working preview; GUI execution is brought forward for preview only, not formal release acceptance. Interactive GUI behavior, actual installation and dependencies on the user's machine remain unverified. No claim of percentage progress, measured visual quality or exact output size.

Exact preview SHA 93531cbd passed [CI 45473261311](https://github.com/WhoSia/VANTA/actions/runs/45473261311): WPF build 0 warnings/errors, smoke and FFmpeg integration tests, artifact 14959205335 (133396 compressed bytes). This proves compilation and core regressions, not interactive GUI acceptance. Next SHA 8104a55 adds absolute-path paired FFmpeg/FFprobe discovery, encoder listing, progress parsing and GUI stage updates; pending its own CI. Discovery is not binary authentication; reuse only trusted installations. Source timing and audio hash guards are unchanged.
