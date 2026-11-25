# Owner UX expansion and performance review — 0.3.0

2026-11-25 KST. Owner requested Korean/English, example resolution/bitrate choices with manual entry, additional compression beyond three recommendations, more intuitive multiple-video workflow, later AV1 evaluation and adaptive Auto thread efficiency with stability. Prioritize via subjective (value, urgency, feasibility, verifiability), no weighted sum; integrity gates noncompensable.

| Task | Vector | Decision |
|---|---|---|
| Direct multi-file opening and clear batch workflow | (1,.98,.98,.95) | first direct multiselect; then simplify actual queue, visible job counts and row actions |
| Quick settings and additional compression | (.99,.98,.98,.95) | source-bounded example heights/bitrates, manual edit retained; explicit button lowers current video bitrate budget by 25%, clamps 64 kbps; no quality claim or encode |
| Korean/English | (.99,.95,.90,.90) | next coherent implementation: WPF resources, language preference shared across windows; translate controls, statuses, labels, errors where known; keep machine diagnostics unchanged with context; verify both languages and UI state. No partial translation advertised as full support |
| Package and install validation | (1,1,.95,.95) | hard gate, preserve ongoing CI |
| Dynamic Auto threads | (.97,.85,.80,.80) | measure whole-job throughput, per-process/aggregate resource and quality; adapt budgets at job launch or between segments only, not live FFmpeg worker mutation; no simultaneous split+batch without validation; manual unchanged |
| AV1 research | (.91,.55,.75,.80) | experimental only; no default or production path without MP4 playback, codec parameters, timing/audio, metadata, decode, quality/size and Windows device evidence |

8e7139d CI 45522448591 succeeded. Package candidate f610043 rebased as cb7f7da369f5730298c2e38a26e14e8078a8bc5f (4 changed paths, checked exact base blobs). CI 45525030762 pending. Do not replay f610043. UX candidate f35f3a6655f909b340af8964ac933dbc02ba2e53 parent cb7f7da prepared off main: QuickSettings.cs, single and individual batch editor XAML/code, core and WPF tests; main file dialog multiselect routes one file to single and multiple to batch. After exact package CI success, compare candidate changed paths against parent and current main, reuse exact blobs via lease update. If package CI fails, fix first. No new version/tag/release. Existing batch UI not fully redesigned yet.

## AV1 screening — actual local command-level test

Debian FFmpeg 7.1.5, 5 visible logical processors, cgroup quota 4 CPUs. Silent 640x360 30fps 3s testsrc2 H264 medium CRF18, 2 threads, 442896 bytes. All outputs requested 500 kbps, fps passthrough, H264 medium threads 2; HEVC medium threads 2 plus x265 pools=2:frame-threads=1:log-level=error; SVT-AV1 preset 8, svtav1-params lp=2. These are not matched total thread or quality settings. Two alternating repetitions, encode subprocess wall time, bytes, SSIM against encoded input, ffprobe count_frames. All 90 frames. No audio, no full VANTA validation, no Windows playback.

| rep | codec | s | bytes | SSIM |
|---|---|---:|---:|---:|
| 0 | H264 | .3508 | 182222 | .988883 |
| 0 | HEVC | .8779 | 187951 | .989519 |
| 0 | SVT-AV1 | .8537 | 311583 | .994712 |
| 1 | SVT-AV1 | .8059 | 312572 | .994775 |
| 1 | HEVC | .7899 | 187951 | .989519 |
| 1 | H264 | .3597 | 182222 | .988883 |

SVT-AV1 averaged 312077.5 bytes vs H264 182222, HEVC 187951, higher SSIM but not size/quality matched. 500 kbps request does not imply equal final sizes. Do not infer general AV1 inferiority/superiority. AV1 remains research, not executable VANTA plan.

Ponytail: reuse PlanRules and existing edit fields, no added dependency. Fast-anything: upstream CLI, Rust and dotnet absent in local Linux; independent FFmpeg benchmark only. Existing phase measurements point to FFmpeg encoding/validation rather than C# UI as hot path; do not port to Rust without profiling. MCP-Hub workflow read; testing skill discovery call returned internal tool error twice; no successful Gateway result claimed. Use direct GitHub and native local tooling instead.

## Native desktop design direction — owner confirmed

Keep the existing C# WPF application and restrained desktop styling: native selectable controls, compact typography, one subdued selection accent, no decorative gradients, unnecessary animation, web shell, or new UI framework. Prioritize three selectable budget tiles showing name, estimated size, height/codec/bitrate and budget savings; quick-choice and direct-input labels; visible selected-video actions; clear ready-count start button; Korean/English consistent text in later coherent localization increment. Retain keyboard selection, validation and manual input. Test minimum window sizes, DPI scaling, long Korean labels, and keyboard focus on actual Windows separately.

Exact cb7f7da Windows CI 45525030762 passed all stages, job 138906128337, zero warnings/errors. Versioned ZIP 194342 bytes SHA256 b546542877d564284d17df0bdf29255dcaae04381b4bc3dfb239ce10cf7116ab. Package artifact 14980993894 (outer 196364 bytes). Positive installation, incorrect hash, existing version, manifest mismatch and traversal rejection tests passed. Not owner-PC acceptance. Quick settings f35f3a6 safely rebased as ec9f518b7d3bb408709bcbc8b878281ed595e5b4; do not replay. Native plan tiles and selection-aware batch toolbar candidate 7c7dd9257fc8139166cf140d6ecb91aae781a9e0 parent ec9f518, staged not main. Requires successful quick-settings CI and exact changed-blob comparison before applying. MCP-Hub design discovery invocation returned internal error; direct GitHub path used. No claim of rendered visual review or full localization.
