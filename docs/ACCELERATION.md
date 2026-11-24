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

## Windows baseline and frame-ID regression

Exact 81e4217 passed Windows CI 45478754330: 16 selectable matrix cells, 120-second NTSC split (6.829 s end-to-end, 3871318 bytes), VFR rejection and previous regressions. This single split duration has no same-codec continuous comparator. Artifact 14960022732 (162477 bytes compressed).

Local FFmpeg 7.1.5 marker trial: 121 CFR frames (160x96, 30 fps), each frame contains its frame index encoded as eight vertical luma stripes. Source encoded H.264; split 60/61 frames with early seek, trim, exact frame count, 90000 timescale and explicit concat durations. Decode to 8x2 gray, threshold 128, framemd5. All 121 input IDs distinct. At 180 kbps, six decoded marker hashes differed (first three frames and first three frames of second segment); at 2 Mbps all 121 matched in sequence. This is a decoded content error in the low-rate synthetic fixture, not proof of a seek/order defect: quantization/rate-control startup is a plausible cause, not isolated. Timestamp checks alone do not catch content damage. No general visual-quality claim from these markers.

New cd99fbf adds Windows odd-count marker identity at 2 Mbps, a reverse-order negative control with preserved timestamps, delayed AAC split and active split cancellation. It adds a matched encoder-thread budget (4 continuous, 2+2 split), two repetitions in alternating order on the same 120s fixture, records wall time including all validation, output bytes and SSIM against source. Decoder/filter/process overhead is not bounded by encoder threads; this is not identical total CPU allocation. No speed or SSIM threshold as a correctness gate for the benchmark. CI pending. Default GUI remains opt-in, no new controls or version changes. If split cannot outperform continuous on representative sources at comparable quality and size, prefer continuous.

## Local continuous encoder thread/preset trial — 2026-11-25

Actually executed in Linux FFmpeg 7.1.5-0+deb13u1, 5 visible logical CPUs, cgroup quota 4 CPU equivalents. One 640x360 30fps 12s testsrc2 source, silent H.264 ultrafast with 4 encoder threads. Target 500000 bps, H.264 output, FPS passthrough. Each trial's wall time measured from FFmpeg process launch through encoding completion, excludes source creation, SSIM measurement, and VANTA integrity checks. Two repeats, second reverses configuration order. SSIM computed afterward against source with filter_complex_threads=2.

| Repeat | Preset | Encoder threads | Seconds | Bytes | SSIM |
|---:|---|---:|---:|---:|---:|
| 0 | medium | 2 | 1.263 | 749557 | .988237 |
| 0 | medium | 4 | .867 | 748216 | .988171 |
| 0 | veryfast | 2 | .548 | 747146 | .986108 |
| 0 | veryfast | 4 | .515 | 746428 | .985950 |
| 1 | veryfast | 4 | .460 | 746428 | .985950 |
| 1 | veryfast | 2 | .520 | 747146 | .986108 |
| 1 | medium | 4 | .901 | 748216 | .988171 |
| 1 | medium | 2 | 1.091 | 749557 | .988237 |

Reproduce: ffmpeg -v error -f lavfi -i "testsrc2=s=640x360:r=30:d=12" -c:v libx264 -preset ultrafast -threads 4 -pix_fmt yuv420p source.mp4; ffmpeg -v error -i source.mp4 -map 0:v:0 -an -c:v libx264 -preset medium -b:v 500000 -threads 2 -fps_mode passthrough out.mp4; repeat with four thread/preset combinations; ffmpeg -i source.mp4 -i out.mp4 -filter_complex_threads 2 -filter_complex "[0:v][1:v]ssim" -an -f null -. Mean seconds 1.177/.884/.534/.4875. No representative corpus, CPU peak or memory measurements; no universal speed claim. Do not change default thread policy solely from this result.

## Matched Windows benchmark and 1080p follow-up

Corrective SHA 123ce84 passed CI 45481255837. On the 120-second 320x180 NTSC fixture, H.264 medium, total 4 encoder threads, continuous 5.482/5.514s (3868999 bytes, SSIM .980908), split 6.709/6.649s (3871318 bytes, SSIM .980840). Mean 5.498 versus 6.679s; split 21.48% slower. Delayed AAC, all 121 odd-split marker IDs, negative reverse-order control and active split cancellation passed. Artifact 14961728104, 162690 bytes. No general speed or visual-quality guarantee.

Additional actual local command-level trial: Linux FFmpeg 7.1.5-0+deb13u1, 5 logical CPUs visible, cgroup quota 4 equivalents. 1920x1080 testsrc2, 30fps, 4s (120 frames), source H.264 ultrafast, AAC sine delayed 0.2s, 7797721 source bytes. H.264 medium at 3000000bps, continuous 4 encoder threads versus two 60-frame workers at 2 threads each; early seek one second before split, copyts, midpoint trim, setpts, timescale 90000, explicit concat duration 2s, original audio mapped once. Each segment checked by decoded PTS grid. Both modes compare all decoded video timestamps and audio packet timestamps under a common shift, check copied audio SHA-256, and run full output decode. Unlike C# Encoder this standalone Python subprocess harness omits metadata probe/endpoint gate and publication overhead. One run per mode (continuous first); no quality metric or peak resource measurement.

| Mode | Encode including split preparation/merge (s) | Timeline/audio checks (s) | Full decode (s) | Total (s) | Bytes | Max relative video/audio PTS drift |
|---|---:|---:|---:|---:|---:|---:|
| Continuous | 1.8994 | 1.0774 | .2797 | 3.2565 | 1529764 | 0 / 0 ms |
| Split | 2.5875 | 1.0509 | .2157 | 3.8541 | 1061339 | 0 / 0 ms |

Split components: input frame-grid .3129s, parallel workers including per-segment decode check 2.1769s, merge .0973s. Both audio hashes match. Split was 18.35% slower, and its output was 30.62% smaller; without quality evidence this is not a quality-matched comparison. Do not infer that split always loses. Prioritize phase timing and avoid duplicate work only where equivalent integrity is demonstrated; do not delete checks merely to improve benchmarks. Keep split opt-in.

## Audio validation fusion feasibility — 2026-11-25

Actual local command-level experiments, FFmpeg/ffprobe 7.1.5-0+deb13u1, Linux (5 visible logical CPUs; quota 4 equivalents). No .NET SDK in this environment. Fixture: testsrc2 160x90, 30000/1001 fps, 120s; AAC 48kHz mono sine tracks at 440 Hz (120s) and 880 Hz (119.85s input, offset 0.15s). Source H.264 ultrafast, two threads; destination H.264 veryfast 180000bps, two threads, passthrough, audio copied. Separate method: sequential JSON ffprobe packet PTS extraction per track/source/output and two FFmpeg aggregate streamhash invocations; combined method: identical probes with packet data_hash and SHA-256, compare per-packet PTS and hash. Python parsing included, no full video decode/timeline timing included. Not equivalent performance measurement to C# streaming concurrent probes.

| Mode | Repeat seconds | Mean seconds | Packet counts | Max audio PTS delta |
|---|---|---:|---|---|
| Separate | .64144 / .60974 | .62559 | 5626 / 5619 | 0 ms |
| Combined | .55347 / .54386 | .548665 | 5626 / 5619 | 0 ms |

Order separate/combined/combined/separate. All payload comparisons passed. Combined audio-only wall time 12.30% lower, two repetitions, warm cached synthetic input only. Negative control: independently encoded 660 Hz 120s AAC replaces 440 Hz track by stream copy; all 5626 PTS equal, zero packet hashes equal, aggregate streamhash differs. Per-packet hash catches this content substitution. Do not remove streamhash before streaming comparator tests across containers, codec configuration/extradata and supported input domain; keep full audio/video decode. Packet hashes are not a perceptual metric.

Cross-container follow-up: remux fixture to MOV and MKV, then same transcode to MP4. MOV: both tracks' counts and every packet hash equal, relative PTS delta 0 ms. MKV: both tracks' counts/hashes equal, but video first PTS shifts 21 to 33 ms; relative audio max drift 12.167 / 12.333 ms despite aggregate streamhash equality. This exceeds VANTA's 2ms gate. This particular MKV is already unsupported by Probe due to missing stream durations/audio bitrate; do not claim actual C# acceptance. Command-level test adding -enc_time_base demux to MKV transcode: 3597 decoded video frames on both sides, common shift 0, max relative video PTS delta 0ms, all 11245 audio packet hashes equal, max relative audio deltas .500/.333ms. This is evidence of a possible timebase-preserving solution, not a general fix: endpoint metadata, audio duration, VFR, other source timebases, encoder compatibility and Windows tests still needed. No production command change.

Reproduction: generate fixture with ffmpeg -f lavfi -i "testsrc2=s=160x90:r=30000/1001:d=120" -f lavfi -i "sine=frequency=440:sample_rate=48000:duration=120" -itsoffset 0.15 -f lavfi -i "sine=frequency=880:sample_rate=48000:duration=119.85" -map 0:v -map 1:a -map 2:a -c:v libx264 -preset ultrafast -threads 2 -pix_fmt yuv420p -c:a aac src.mp4. Transcode: ffmpeg -i src.mp4 -map 0:v -map 0:a -c:v libx264 -preset veryfast -threads 2 -b:v 180000 -c:a copy -fps_mode passthrough dst.mp4. Probe: ffprobe -v error -select_streams a:0 -show_packets -show_entries packet=pts_time,data_hash -show_data_hash sha256 -of json src.mp4 (repeat a:1 and dst). Aggregate: ffmpeg -v error -nostdin -i src.mp4 -map 0:a -c:a copy -f streamhash -hash sha256 -. Negative: generate alt.m4a with lavfi sine 660Hz 48kHz 120s and AAC, then ffmpeg -i src.mp4 -i alt.m4a -map 0:v -map 1:a -map 0:a:1 -c copy changed.mp4. Cross-container: ffmpeg -i src.mp4 -map 0 -c copy src.mkv (or src.mov); run same transcode, optionally add -enc_time_base demux; compare decoded video best_effort_timestamp_time and audio packet PTS relative to common first-video shift. All raw observations recorded; no fabricated Windows performance claim.

## Production phase evidence and shadow comparator

CI 45487124583 (bc0554e) succeeded. Continuous total 7.3521/7.3919s; split 8.4264/9.3955s. Continuous average output probe/timeline/audio hash/full decode = .096/1.21875/.26815/.68385 seconds. Audio aggregate hash is only about 3.64% of total; eliminating its entire measured wall time alone would reduce total to approximately 7.104 seconds, before any extra packet-hash cost. Do not promise a 12% end-to-end improvement from earlier audio-only test. Local parser format check performed this turn with an actual 0.2s 48kHz AAC sample: ffprobe compact=p=1:nk=0 emits explicit packet rows with pts_time and SHA256:<64 hex> data_hash, including first packet skip-samples side data. This verifies the local output syntax, not Windows 9.1 compatibility. 8961056 adds test-only shadow comparison with streaming packet records, common video origin and 2ms tolerance. Fail closed on absent, duplicate or malformed fields. Legacy aggregate hash and final decode remain mandatory in product. Packet hashes do not establish equivalence of AAC codec extradata or priming side data; inspect those separately before changing validation. Paired Windows test measures full timeline plus old hash vs fused packet hashes; not a full encode benchmark. See CI 45488083106.
