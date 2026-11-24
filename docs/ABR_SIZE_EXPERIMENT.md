# ABR size counterexample — 0.2.0

Executed 2026-11-25 KST in Linux development container, FFmpeg 7.1.5 Debian. Two 320x180 30 fps 6 s silent fixtures: color=c=gray and testsrc2, each first encoded to H.264 MP4 with 2 threads. Re-encoded 12 combinations with -b:v 180000, preset fast/medium/slow, -threads 2; x265 additionally pools=2:frame-threads=1:log-level=error. No C# execution, no quality measurement. Predicted 135675 bytes for all (6*180000/8*1.005).

| Fixture | Codec | Preset | Actual bytes | Signed error % |
|---|---|---|---:|---:|
| flat | H.264 | fast | 6704 | -95.06 |
| flat | H.264 | medium | 6882 | -94.93 |
| flat | H.264 | slow | 6965 | -94.87 |
| flat | HEVC | fast | 11299 | -91.67 |
| flat | HEVC | medium | 11285 | -91.68 |
| flat | HEVC | slow | 11295 | -91.67 |
| motion | H.264 | fast | 132453 | -2.37 |
| motion | H.264 | medium | 131701 | -2.93 |
| motion | H.264 | slow | 131508 | -3.07 |
| motion | HEVC | fast | 140008 | +3.19 |
| motion | HEVC | medium | 139800 | +3.04 |
| motion | HEVC | slow | 136513 | +0.62 |

Reproduce input: ffmpeg -hide_banner -loglevel error -y -f lavfi -i "color=c=gray:s=320x180:r=30:d=6" -c:v libx264 -threads 2 flat.mp4 (replace filter with testsrc2=s=320x180:r=30:d=6 for motion).
Output example: ffmpeg -hide_banner -loglevel error -y -i motion.mp4 -c:v libx265 -preset medium -b:v 180000 -threads 2 -x265-params pools=2:frame-threads=1:log-level=error out.mp4.

Decision: label prediction as bitrate budget, not exact size, interval, or upper bound. Do not calibrate global constants from two synthetic fixtures. Preserve unknown quality axis. Next Windows regression should run all 2x3 selectable codec/preset configurations with real Encoder, record actual signed errors without assuming they meet a fixed size tolerance. No change to synchronization acceptance. Owner FFmpeg 9.1 full build and ffprobe both launch successfully; that is dependency evidence, not VANTA encoding acceptance.
