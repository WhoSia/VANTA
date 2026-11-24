# Delayed AAC timing experiment (0.1.0)

Executed the following equivalent FFmpeg operations in a Linux container; not a C# integration test.

Input fixture: 320x180 testsrc2, 30000/1001 fps, 5 seconds; 48 kHz sine, 4.8 seconds, input offset 0.2 seconds. Encoded input H.264/AAC MP4. Transcoded video to HEVC, 180000 bit/s, medium, fps_mode passthrough, MP4 hvc1. Compared AAC re-encode at 96000 bit/s vs audio stream copy. Both outputs fully decoded without error.

| Stream measurement (s) | Input | AAC re-encode | AAC copy |
|---|---:|---:|---:|
| Video start | 0 | 0 | 0 |
| Video duration | 5.005 | 5.005 | 5.005 |
| Audio start | .178 | .156 | .178 |
| Audio duration | 4.821333 | 4.842667 | 4.821333 |

Decision: audio copy by default and only for supported AAC in this prototype. Re-encode can shift container-visible endpoints; this is not itself proof of audible drift. Endpoint equality is insufficient for perceptual synchronization. Longer, VFR and signal-based regression tests remain open.

Reproduce:
```sh
ffmpeg -v error -f lavfi -i 'testsrc2=s=320x180:r=30000/1001:d=5' -itsoffset 0.2 -f lavfi -i 'sine=frequency=440:sample_rate=48000:duration=4.8' -c:v libx264 -pix_fmt yuv420p -c:a aac in.mp4
ffmpeg -v error -nostdin -n -xerror -i in.mp4 -map 0:V:0 -c:v libx265 -preset medium -b:v 180000 -fps_mode passthrough -map '0:a?' -c:a copy -tag:v hvc1 -movflags +faststart out.mp4
ffprobe -v error -show_entries stream=codec_type,start_time,duration,avg_frame_rate -of json out.mp4
ffmpeg -v error -xerror -nostdin -i out.mp4 -map 0:V:0 -map '0:a?' -f null -
```
