# Shutter Encoder 20.5 initial audit

Upstream tag: https://github.com/paulpacifico/shutter-encoder/tree/20.5
Tree cbb7d4df3990b4a0cef2b1314de2dd1b25443bf2.

Inspected source landmarks (not exhaustive review):
- src/shutterencoder/functions/settings/BitratesAdjustement.java (c3cc514dd7e36fee15029b5957ee505812c29837): pass selection and passlog naming.
- src/shutterencoder/functions/VideoEncoders.java (d9ce91dd1404492f60bd88ec70ea67dbd1a02d23): assembles codec, bitrate, frame rate, resolution, color and mapping options; second pass dispatch.
- src/shutterencoder/library/FFmpeg.java (73800a3b455bba3a9f676e2fab5a8c14c5541a21): process lifecycle and pass-specific logic.
- LICENSE.txt: GPLv3 text.

VANTA independently implements a narrow argument-list based pipeline. No Java source copied in this revision. Audit specific behavior before porting; preserve provenance and comply with applicable license before redistribution.

## 2026-11-25 comparative acceleration audit

Public upstream documentation/source landmarks (not full source audit or code import):

| Upstream | Mechanism | VANTA decision |
|---|---|---|
| [Av1an encoding](https://rust-av.github.io/Av1an/Cli/encoding.html#chunk-order---chunk-order) | Independent chunk workers, longest-first default; alternate sequential/random order; different frame-accurate and keyframe-only chunk methods | Test length-based scheduling separate from chronological merge. Do not infer frame accuracy from keyframe alone. |
| [Av1an scene detection](https://rust-av.github.io/Av1an/Cli/scene_detection.html) | Cost-based or pixel-difference scene detection, bounded extra splits and minimum scene length | Compare scene-aware candidates against existing keyframe minimax; charge analysis cost, retain safe seek/trim. |
| [x265 threading](https://x265.readthedocs.io/en/master/threading.html) | WPP, frame threads, lookahead pools; more parallelism can trade compression efficiency for speed | Treat requested encoder threads as a request, not total CPU limit; measure continuous native scaling before extra processes. |
| [SVT-AV1 parameters](https://github.com/AOMediaCodec/SVT-AV1/blob/main/Docs/Parameters.md) and [design](https://github.com/AOMediaCodec/SVT-AV1/blob/main/Docs/svt-av1-encoder-design.md) | Pipeline parallelism and level-of-parallelism increase in-flight pictures and memory | Memory-aware codec-specific policy; no AV1 production activation or Rust port implied. |
| [FFmpeg CLI](https://ffmpeg.org/ffmpeg.html) and [formats](https://ffmpeg.org/ffmpeg-formats.html) | Accurate seek can decode/discard preroll; concat timing and compatible stream constraints | Keep source grid, half-open intervals, explicit durations, original audio mapped once and final validation. |
| [HandBrake encavcodec.c](https://github.com/HandBrake/HandBrake/blob/master/libhb/encavcodec.c) | Encoder packet reception, frame timestamp lookup and delayed-packet handling | Retain explicit timing and codec-state validation rather than assuming packet order. |

Implementation 4f985a8: original C# research-only longest-first dispatch with deterministic ties, unchanged numbered part filenames and chronological manifest. No upstream code copied or dependency added. Length is a cost proxy, not measured complexity. See ACCELERATION.md for benchmark and counterevidence. Upstream documentation is mutable; this is a mechanism-level audit, not a pinned revision/license clearance for reuse.

## Indexed source access follow-up

- [FFMS2 index API](https://github.com/FFMS/ffms2/blob/master/doc/ffms2-api.md): index keyframes/timecodes, validate index ownership before reuse. [FFMS2 VapourSynth](https://github.com/FFMS/ffms2/blob/master/doc/ffms2-vapoursynth.md): persistent index and frame access. VANTA already scans source grid under a source lease, so investigate reusing that scan before introducing persistent cache/dependency.
- [L-SMASH Works](https://github.com/HomeOfAviSynthPlusEvolution/L-SMASH-Works/blob/master/VapourSynth/README.md): indexed seeking and random-access verification options. [BestSource](https://github.com/vapoursynth/bestsource/blob/master/README.md): cached preroll. A keyframe flag alone is not proof that a cut is independently decodable.
- [Av1an scene detection](https://rust-av.github.io/Av1an/Cli/scene_detection.html): cost-based scene cuts, additional splits, minimum lengths. Reuse the architecture, not necessarily code or its runtime stack. See [encoding options](https://rust-av.github.io/Av1an/Cli/encoding.html) for indexed source methods.

New original research harness docs/research/indexed_seek_screen.py compares guarded early seek against source-index timestamp seek with noaccurate_seek and midpoint trim. No upstream code copied or production settings changed. Closed-GOP result promising; open-GOP strict decoder error on BOTH modes is important negative evidence. Do not admit arbitrary keyframes as random-access points. Next test closed/open GOP and frame content against independent lossless reference, vary timebase and seek bounds, then exact C# CI. No source dependency added.
