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
