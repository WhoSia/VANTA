# Acceleration — 0.2.0

Owner explicitly requested optional single-video splitting on 2026-11-25. Continuous encoding stays default, independent batch jobs stay continuous; experimental split is off by default and resets on new source. No automatic switch. The following experiment is not proof of perceptual quality, universal speedup or Windows acceptance.

## First local benchmark

Linux FFmpeg 7.1.5, CPU quota 4 cores (5 logical CPUs visible), 640x360 testsrc2 30000/1001 fps, 360 frames (12.012 s), AAC sine source offset 0.2 s (11.812 s audio input), encoded source H.264 ultrafast. H.264 medium output, 500000 bps, yuv420p, frame passthrough, 90000 track timescale. Continuous uses 4 encoder threads. Split uses 2 processes x 2 encoder threads, 180 frames each with accurate seek at 0 and 6.006 s, explicit concat duration 6.006 s, original AAC mapped once. Time includes encoding and split mux, not full VANTA post-validation. One repetition.

| Mode | Seconds | Bytes | Frames | Max video PTS drift (ms) | Audio packets | Max relative audio PTS drift (ms) | Audio payload SHA-256 |
|---|---:|---:|---:|---:|---:|---:|---|
| Continuous | 0.960 | 862033 | 360 | 0 | 555 | 0 | Match |
| Split | 0.916 | 841772 | 360 | 0 | 555 | 0 | Match |

A subsequent local command trial used early seek with copyts, trim at midpoint between source frames, setpts and exact frame count; two segments had 180 frames each and final remux had 360 frames, 12.012s video, 555 audio frames. This trial does not establish full PTS/content identity. The implementation uses this more explicit trim approach, not the exact benchmark command. End-to-end C# timing pending. No quality measurement yet.

Reproduction outline: ffmpeg -f lavfi -i "testsrc2=s=640x360:r=30000/1001:d=12.012" -itsoffset 0.2 -f lavfi -i "sine=frequency=440:sample_rate=48000:duration=11.812" -c:v libx264 -preset ultrafast -threads 4 -pix_fmt yuv420p -c:a aac src.mp4; encode with -c:v libx264 -preset medium -b:v 500000 -fps_mode passthrough -video_track_timescale 90000. Split benchmark: two concurrent -ss 0/6.006 -i src.mp4 -map 0:v:0 -an -frames:v 180 commands with 2 encoder threads, then concat demuxer and map original audio once. Compare decoded frame PTS and audio packet PTS, counts, streamhash.

## Prototype 81e4217

Only zero-start CFR H.264 MP4 input, H.264 output at original height, known regular grid (100 microsecond tolerance), 60–2,000,000 frames and 1–120 fps. Exactly two workers; source frame timestamps determine boundaries, source audio is never segmented, final output goes through all existing endpoint, per-frame/packet timestamp, audio hash and full decode validation. Cancel/failed validation must not publish output. Explicit opt-in, no automatic fallback yet; disable option and retry normally. Not supported for batch. Veryfast added separately as continuous preset for both codecs. No guarantee that timestamps prove frame content identity.

Next gates: exact Windows CI, delayed audio and 120s split, VFR rejection, active split cancellation, boundary identity test (unique frame marker), repeat end-to-end benchmarks with same codec/preset and thread budget, actual bytes and SSIM/PSNR or optional VMAF. Compare speed, quality, size, peak resources and validation cost as separate axes; do not declare speedup from single short run. Hardware acceleration is a separate capability-tested option, not inferred from encoder listing.

References: https://ffmpeg.org/ffmpeg-formats.html (concat duration/segment); https://en.bandisoft.com/bandizip/help/multicorecompress/ and https://en.bandisoft.com/bandizip/help/fastarchiving/ (multicore and avoiding unproductive compression). No verified comparative archive product ranking.
