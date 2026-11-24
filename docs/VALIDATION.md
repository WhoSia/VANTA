# Validation status — 0.1.0

Exact baseline df3161e: [Windows CI #3](https://github.com/WhoSia/VANTA/actions/runs/45468987261) passed with zero compiler warnings/errors. FFmpeg 9.1 essentials. Delayed AAC, Unicode paths, source SHA-256, no overwrite, pre-cancel, VFR with two independently offset audio streams, 120-second NTSC timeline, active cancellation/cleanup and artifact publish all passed. Compressed preview artifact: 124699 bytes; excludes external .NET Desktop Runtime and FFmpeg.

Counterexample found after baseline: the old packet comparison independently normalized every stream. A 20 ms constant audio shift can pass both the 50 ms endpoint floor and the independent packet comparison. Reproduced with FFmpeg 7.1.5 on Linux using 29.97 fps H.264/AAC fixture; video start 0, audio start .178 -> .198, unchanged duration. The new guard uses video first-frame shift as a common reference for all audio packets and rejects ffprobe error diagnostics. Added negative regression and no-audio/scaling fixture; pending exact-SHA CI.

This verifies timestamp/payload invariants, not perceptual synchronization, all possible formats or hour-scale performance. GUI still analysis-only. No release/version change without approval.
