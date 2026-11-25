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

## Shadow validation CI and optional encoder thread budgets

CI 45488083106 at 8961056 passed. Timeline + aggregate hash: 1.5660/1.5510s, fused packet validation: 1.3598/1.3661s. Mean 1.5585 vs 1.36295s (12.55% reduction for this validation stage). Positive delayed AAC, multi-audio VFR and MOV, negative shifted/substituted audio passed. No production replacement: AAC extradata/priming and other supported-domain equivalence remain open. Artifact 14964738250, 169354 bytes.

9a5f038 adds explicit optional encoder-thread requests, default null (existing automatic behavior). Single continuous 1..64, explicit split even 2..64; batch floor(total / requested concurrency) per job, reject total below concurrency. UI choices bounded by Environment.ProcessorCount and 64. Unused remainder and unused slot capacity are intentionally not redistributed in this first static policy. FFmpeg -threads is an encoder request, NOT a process CPU or memory cap; libx265 internal pools/decoder/filter workers are not controlled. No automatic change to defaults. Smoke tests cover validation/allocation; integration uses one-thread AVC and two concurrent jobs sharing four requested encoder threads. CI pending.

Actual local trial 2026-11-25: Linux FFmpeg 7.1.5-0+deb13u1, 5 visible CPUs, cgroup quota 4 CPU equivalents, 2 silent 960x540 30fps 4s sources generated with lavfi testsrc2=s=960x540:r=30:d=4 and testsrc2=s=960x540:r=30:d=4,noise=alls=8:allf=t+u:all_seed=42, source H.264 ultrafast -threads 2 -pix_fmt yuv420p. Outputs H.264 medium, 1000000bps, passthrough, no audio. Serial 4 encoder threads per job, parallel 2+2, parallel auto (omit -threads). Alternating order, two repetitions. Wall time for pair excludes input generation and VANTA validation. Per-process max RSS from /usr/bin/time, NOT simultaneous aggregate peak; summed CPU time user+system.

| Rep | Mode | Pair wall s | Per-job CPU s | Per-job max RSS KiB | Output bytes |
|---:|---|---:|---|---|---|
| 0 | Serial 4 | 1.3678 | 1.86/1.94 | 209236/210952 | 497909/484883 |
| 0 | Parallel 2+2 | 1.1081 | 1.77/1.90 | 196888/196352 | 498465/484747 |
| 0 | Parallel auto | 1.0328 | 1.73/2.00 | 236868/236384 | 498975/484034 |
| 1 | Parallel auto | .9450 | 1.68/1.82 | 236732/236232 | 498975/484034 |
| 1 | Parallel 2+2 | 1.0484 | 1.60/1.82 | 196424/196488 | 498465/484736 |
| 1 | Serial 4 | 1.3003 | 1.67/1.90 | 209140/208928 | 497909/484883 |

Means 1.33405/1.07825/.98890s. Bounded 2+2 was about 19% faster than serial here, auto about 26% faster than serial; bounded was about 9% slower than auto, with about 17% lower mean per-process peak RSS. No perceptual-quality check, no throughput generalization, no GPU testing. Retain auto and explicit budget as choices. Reproduce output command: ffmpeg -v error -nostdin -i source.mp4 -map 0:v:0 -an -c:v libx264 -preset medium -b:v 1000000 -fps_mode passthrough -threads 2 output.mp4 (replace thread setting; run two subprocesses concurrently or sequentially).

## Actual hardware encoder presence counterexample

Local Linux development container, FFmpeg 7.1.5 (no dotnet, no /dev/dri). Listing contains h264_nvenc, hevc_nvenc, h264_qsv, hevc_qsv and VAAPI; AMF absent from the queried list. Executed six separate FFmpeg commands: ffmpeg -hide_banner -nostdin -v error -xerror -n -f lavfi -i testsrc2=s=320x180:r=30:d=0.4 -an -c:v ENCODER -pix_fmt yuv420p -threads 2 -b:v 250000 OUTPUT.mp4. On success ffprobe -v error -select_streams v:0 -count_frames -show_entries stream=codec_name,nb_read_frames -of compact=p=0 OUTPUT.mp4. Temporary directory removed.

| Encoder | Exit | Observation |
|---|---:|---|
| libx264 | 0 | 12 frames H.264, 9383 bytes |
| libx265 | 0 | 12 frames HEVC, 10702 bytes |
| h264_nvenc | 255 | Cannot load libcuda.so.1 |
| hevc_nvenc | 255 | Cannot load libcuda.so.1 |
| h264_qsv | 171 | Error creating MFX session: -9 |
| hevc_qsv | 171 | Error creating MFX session: -9 |

This is a local command-level negative control, not a Windows or user GPU assessment. No throughput comparison; sample success would not establish support at other resolutions/settings. Candidate 57718c449c6dbd1f43f44f3e0c2c1c4ad818155a, parent c38deefea4db6502d1645298570db0c473f1e111, adds explicitly invoked hardware diagnostics (NVENC/QSV/AMF for H.264/HEVC), list check, bounded 10s per listed encoder including probe/decode, 12-frame codec/count check, cancellation and temporary cleanup. UI only reports sample passed/failed/not listed/timed out; no hardware plan or encoding route enabled. Tests use actual software positive controls, hardware state classification without requiring a GPU, pre-cancel. Candidate not on main while planner CI runs. After planner success compare affected base blobs (MainWindow.xaml, MainWindow.xaml.cs, tests/Vanta.Integration/Program.cs) against current main and confirm EncoderDiagnostics.cs does not already exist; rebase with lease preserving docs. If failure fix first. Diagnostic sample is intentionally limited and may fail on otherwise usable hardware with different settings. No automatic hardware recommendation.

## Preset speed screening — 2026-11-25

Actual local FFmpeg 7.1.5 Debian, 5 visible processors, cgroup quota 4 CPUs. 960x540 30fps 4s testsrc2 plus sine 48kHz AAC 128 kbps. H264 fast CRF18 source, 2 requested encoder threads, yuv420p; source 1297423 bytes. Output H264 or HEVC MP4, 1 Mbps video budget, original AAC copy, passthrough timestamps, 2 requested encoder threads; x265 additionally pools=2:frame-threads=1:log-level=error. Alternating medium/veryfast then veryfast/medium; encode subprocess wall time and independent full decode time. SSIM compared with decoded input. No matched perceptual-quality or full VANTA timestamp/validation comparison.

| Codec | Rep | Preset | Encode s | Decode s | Bytes | SSIM |
|---|---:|---|---:|---:|---:|---:|
| H264 | 0 | medium | .9523 | .1500 | 561964 | .989305 |
| H264 | 0 | veryfast | .3876 | .1267 | 562233 | .988511 |
| H264 | 1 | veryfast | .3908 | .1200 | 562233 | .988511 |
| H264 | 1 | medium | .7966 | .1184 | 561964 | .989305 |
| HEVC | 0 | medium | 1.7814 | .1691 | 576699 | .989716 |
| HEVC | 0 | veryfast | 1.3466 | .1694 | 584615 | .988126 |
| HEVC | 1 | veryfast | 1.3480 | .1608 | 584615 | .988126 |
| HEVC | 1 | medium | 1.7139 | .1582 | 576699 | .989716 |

All eight outputs full-decoded successfully, ffprobe counted 120 video frames, and copied AAC streamhash SHA256 matched source. Means H264 .87445 -> .38920s (-55.49% encode time), HEVC 1.74765 -> 1.34730s (-22.91%). Single synthetic source, no real hardware generalization. At fixed requested bitrate SSIM decreases and output size can increase. Do not silently change medium default; expose explicit shared preset preserving per-file overrides. 0685a93 implements shared preset and preference migration, bilingual hint, fixed single footer, distinct batch command and real two-file WPF test. CI 45547843938 pending.

Windows CI 45546909751 job 138978616567 continuous phase two-rep mean total 5.3425s, encode 3.50795s, packet timeline .91335s, audio hash .28835s, decode .48605s; split mean 6.43025s. Prioritize preset choices, then equivalence-tested reduction of repeated validation IO. Never drop timestamp, copied audio or full decode checks; prior fused packet hash shadow work does not yet prove extradata/priming equivalence. Do not automatically enable split or alter default threads on this evidence.

Latest exact 0685a93 Windows CI 45547843938: PACKET_SHADOW baseline .9531/.9145 s (mean .9338); fused .8875/.8739 s (mean .8807), 5.69% stage-time improvement. Same fixture within same run, not cross-run comparison. Continuous total 2.9829/3.1280 s (mean 3.05545); split 3.8146/3.7724 (mean 3.7935), 24.15% slower. SSIM continuous .980958/.980934, split .980890/.980876. This does not justify dropping existing audio streamhash or enabling split by default. Continue exact equivalence tests for packet payload, timestamps, AAC configuration/priming, and decode. No new benchmark execution in this turn; values retrieved from actual CI logs.

## Rust / fast-anything decision screen

2026-11-25, actual local Debian FFmpeg 7.1.5-0+deb13u1, nproc 5, cgroup cpu.max 400000 100000 (4 CPU equivalents). Checked command availability: fast-anything, rustc, cargo and dotnet absent from PATH; no upstream CLI or Rust compilation performed. Read Ponytail and independent fast-anything skill. Native FFmpeg/Python fallback is a screening test, not C# profiling or a Rust benchmark.

Synthetic fixture generated with ffmpeg -v error -f lavfi -i testsrc2=s=640x360:r=30:d=20 -c:v libx264 -preset ultrafast -threads 2 sample.mp4. Three repetitions of ffprobe -v error -select_streams v:0 -show_frames -show_entries frame=best_effort_timestamp_time -of compact=p=0:nk=0 sample.mp4. Measure wall time of subprocess.check_output, then mean of 100 repetitions parsing captured bytes in Python (splitlines, split on pipe, extract float field, count frames). All 600 timestamps, 22014 output bytes. No user data, no audio. Temporary fixture removed.

| Rep | ffprobe wall s | cached Python parse s |
|---:|---:|---:|
| 0 | .225334 | .000170 |
| 1 | .230272 | .000183 |
| 2 | .220113 | .000157 |

Mean .225240 vs .000170. Python parse is roughly .075% of these two measured stages combined; this is NOT a bound on managed C# streaming, allocations, or whole VANTA runtime. It makes a standalone Rust timestamp-text parser low priority until actual .NET profiling shows otherwise. A Rust wrapper invoking the same FFmpeg executable cannot eliminate its native codec work; direct libav integration could avoid process/IO duplication but requires ABI, native library redistribution/licensing, cancellation, security/update and correctness validation. Prefer existing C# orchestration, native FFmpeg codec implementations, measured algorithmic reductions and explicit preset selection. No Rust dependency added. Candidate future port only if measured representative C# hot path has material end-to-end share and passes differential testing plus packaging/rollback. No estimated speedup presented as measured.

Exact 40087b4 CI 45549388454: baseline PACKET_SHADOW 1.1516/1.2376 s mean 1.1946; fused 1.1621/1.0199 mean 1.0910, about 8.67% stage reduction; one pair slower. Continuous mean 4.15035s, split mean 5.0140s, about 20.81% slower. Keep legacy audio hash and split opt-in; no new performance path enabled.

## Managed-parser differential screen (7492d50, pending CI)

Production parser semantics unchanged: existing Split/FirstOrDefault/InvariantCulture implementation extracted into internal ParseTimestamp for direct measurement. Test-only SpanCandidate scans same pipe-separated fields without per-field allocations. Integration reads actual 120s NTSC fixture FFprobe frame output, compares every row and malformed/missing/duplicate/nonfinite/negative/scientific-notation edges, warms both, runs four interleaved pairs of 100 cached-text passes, logs wall time, current-thread managed allocated bytes, count and checksum. Separate probe wall time logged. This is cached parser microbenchmark, not whole VANTA profiling; normalize by 100 before comparing to one probe invocation. No Rust build or production candidate selection. Current CI 45551368705 pending. Require exact differential success, measured end-to-end impact and justified complexity before switching. Latest f04964c CI PACKET_SHADOW baseline mean 1.5725s, fused mean 1.37765s (~12.39% stage reduction); split continuous mean 5.50945s vs split 6.71525s (~21.89% slower). No validation removal.

### Measured managed parser result and bounded adoption

Exact ebc9d7c CI 45551368705, job 138993072083 succeeded. MANAGED_PARSE_FIXTURE frames=3597 rows=3598 probe_seconds=.413563. For 100 cached-text passes, Split/LINQ .062426/.035409/.035069/.035495s (mean .04209975), Span .047160/.044982/.023141/.020483s (mean .0339415); roughly 19.38% lower mean parser wall time, with one pair slower and JIT/ordering noise. Thread allocation 63835200 bytes vs zero on each 100-pass trial, count and checksum identical (359700, 21579578.01999996). One-pass means .421/.339ms, vs single probe .413563s. No material whole-program speedup established. e5d2e00 adopts the measured Span implementation to reduce avoidable allocations, retains independent old parser as test reference; production change pending exact CI. Rust port not justified by this evidence; no Rust dependency. Latest PACKET_SHADOW 1.1660/1.1675s baseline, 1.0401/1.0403s fused (10.85% stage saving); legacy validation remains.

### Production Span adoption verified

Exact b77ea83 CI 45559737020 logs: MANAGED_PARSE_FIXTURE 3597 frames, 3598 rows, probe .535027s. Cached 100-pass Split reference .106009/.058794/.058426/.055379s (mean .069652); Span .018755/.018664/.018962/.018678s (mean .01876475), 73.06% lower mean cached parsing time, identical count/checksum. Reference managed thread allocations 86862400/86862400/86862400/77627456 bytes; Span zero in all four. One-pass means .69652/.1876475ms; no demonstrated whole-job speedup. Retain production Span; no Rust dependency. PACKET_SHADOW baseline 1.5693/1.5959s mean 1.5826, fused 1.3169/1.3302s mean 1.32355 (~16.37% stage reduction). Continuous totals 5.4598/8.8433s mean 7.15155; split 6.6137/6.7310s mean 6.67235. One continuous encode is unusually slow relative to its paired run, so this run alone does not demonstrate consistent split acceleration. Preserve legacy audio hash and default continuous path. No new experiments executed during this handoff; actual CI log analysis only. Future optional acceleration work awaits separate version approval.

## Independent audio-hash parallelism screen — 2026-11-25

Owner asks for more algorithmic acceleration. Local FFmpeg 7.1.5-0+deb13u1 Debian, 5 visible CPUs, cgroup quota 4 CPU equivalents; rustc/dotnet/fast-anything absent. Generated synthetic 120s 320x180 30fps H264 ultrafast with 48kHz AAC sine; source 12398584 bytes; H264 veryfast 180k target output 3875948 bytes with copied AAC. Same existing FFmpeg streamhash command (-v error -nostdin -i path -map 0:a -c:a copy -f streamhash -hash sha256 -), verify exit 0, empty stderr and equality. Compare sequential source/output invocation versus two simultaneous subprocesses using Python ThreadPoolExecutor(2), four alternating paired repetitions. Seconds: serial .169435/.166963/.159752/.160660; parallel .094352/.081689/.081465/.097101. Means .1642025/.0886517, 46.01% stage wall-time reduction; all hashes matched. No whole-encode time, peak memory, cold-cache, Windows, multi-track or concurrent-batch evidence; fixture deleted. No production code change. Next candidate: shadow benchmark actual C# ValidateAudioAsync sequential vs Task.WhenAll on representative fixtures, include altered audio negative control and cancellation; consider serial fallback for concurrent jobs/disk contention, and measure full workflow before enabling. Preserve original audio hash, timeline and full decode checks. Avoid creating a second production scheduler or native module without evidence. Rust remains deferred. This is separate independent local screen, not fast-anything CLI execution.

Exact 3c8dfb4 CI 45562021321, job 139027743210, all stages succeeded, zero warnings/errors, real-media single and batch language switching while running, original setting preservation, existing regression and installation checks. Artifact 14998900876, outer 214884 bytes SHA256 afc645b86e81656638d209289421591fd6c7df46c549be440aa26b9a050a15cc; inner 211701 bytes SHA256 05e350d3f5740a0ee724d809ec2785777157bbb19a7d2a23a6bf8fba62638b9c. Latest verified download https://github.com/WhoSia/VANTA/actions/runs/45562021321/artifacts/14998900876.
740cea9 adds actual C# shadow benchmark; current production ValidateAudioAsync defaults to sequential, independent parallel tasks joined via Task.WhenAll only when internal test flag is true. Two-job case combines long one-track and short two-track existing fixtures. This tests overlapping hash stages, not full concurrent batch or cold-cache HDD/SSD performance; pre-cancel only, active cancellation and resource peaks not measured. Compare within-run, no default switch without evidence. Latest 3c8dfb4 CI PACKET_SHADOW serial mean 1.67825s, fused mean 1.4410s; continuous mean 5.50565s vs split 6.7183s. No validation removal. Exact CI 45563493986 pending.

## Audio hash scheduling evidence — 740cea9

Exact 740cea91353d1c8374b435fa79ddc3bbb5c57aa5 CI 45563493986, job 139032587660, succeeded, zero warnings/errors; WPF accessibility/header and live language tests, real media workflow, audio hash serial/parallel positive two-track, substitution negative, pre-cancel, all previous regressions and package install passed. Artifact 14998214717 outer 215812 bytes SHA256 d8437631f6dc5c0ea32ec45f02d370a3717b82802bb42ad35fb68e939be7aa14, inner 212602 bytes SHA256 818405d5a1c1b7925c37bedd89519040b5323fa62ea849e091e399db9e376a31. Verified download https://github.com/WhoSia/VANTA/actions/runs/45563493986/artifacts/14998214717.
C# actual validator, Windows CI: serial and parallel one job respectively .249969/.239686/.237377/.240660 and .138184/.135255/.145254/.132735s (means .241923/.137857, 43.02% stage wall-time reduction). Two overlapping hash jobs serial .252955/.260018/.263739/.255890, parallel .212672/.176380/.177953/.169576 (means .2581505/.18414525, 28.67% stage wall-time reduction). All paired comparisons improved; unchanged hash equality, two-track positive, substituted audio rejection, pre-cancel passed. Does not establish full concurrent encode speed, active process cancellation or peak resources. Exact CI continuous totals 5.5668/5.4325s, split 7.7403/8.3552s; fused shadow 1.3687/1.3612 vs legacy 1.5689/1.5585s. Maintain serial default until full workflow and active cancellation evidence.

Independent local full-workflow proxy, FFmpeg 7.1.5 Debian, nproc 5, cgroup quota 4 CPU equivalents. dotnet/rustc/fast-anything CLI unavailable; Python orchestration, NOT VANTA production or Windows. Synthetic testsrc2 640x360 30fps 12s and 6s, sine 440/880 Hz 48kHz, source libx264 ultrafast -threads 2 -pix_fmt yuv420p AAC; output libx264 veryfast 500k -threads 2 -fps_mode passthrough AAC copy MP4 faststart. Each job: encode, concurrently extract source/output video best_effort_timestamp_time via ffprobe -show_frames, then audio pts_time via -show_packets; require matching nonzero counts and <=2.001ms residual relative to common video origin; serial or concurrently execute identical FFmpeg streamhash SHA256 audio-copy commands; full decode with -xerror and empty stderr. No source metadata/lease/atomic publication or complete VANTA validation. Three alternating paired repetitions for one (12s) and two (12s+6s) concurrent jobs; fixture creation excluded. All 18 outputs passed proxy timing/hash/decode, source byte SHA256 unchanged; matching output sizes 860788/427634 bytes. Temporary fixtures removed. Total wall time (s):

| Jobs | Rep | Serial | Parallel |
|---:|---:|---:|---:|
|1|0|1.252211|1.117610|
|1|1|1.201044|1.034224|
|1|2|1.189418|1.100785|
|2|0|1.397084|1.313608|
|2|1|1.199885|1.152859|
|2|2|1.223424|1.148058|

Means 1.214224/1.084206 (10.71% reduction), 1.273464/1.204842 (5.39%). Resource peaks, cold cache, representative source diversity, actual C# end-to-end, active cancellation still unmeasured. This small proxy supports next C# whole-workflow shadow experiment, not an immediate production switch or guaranteed speedup. Preserve all strict existing validation. No additional code changes while target guard CI running.

## Multi-split screening: segment count N independent of worker count W

457eb66 exact CI 45566399287 job 139042144637 passed, zero warnings/errors, target guard and all prior regressions. Artifact 15000586159 outer 216494 bytes SHA256 9d5fe751331d79a8499d7bd003029b69c4a27c548003482316c66235c59c4fef; inner 213318 bytes SHA256 d95223875f3d05dbf72eff61516b36f858d93d0fdce1606653fc8eaba3b1511d. Current verified package https://github.com/WhoSia/VANTA/actions/runs/45566399287/artifacts/15000586159. Owner resize observation only; actual owner compression acceptance remains open.
Actual local FFmpeg 7.1.5 Debian, 5 visible CPUs, cgroup quota 4 CPU equivalents; dotnet, rustc/cargo and fast-anything CLI absent. Python independent proxy, not C# production. 8s 960x540 30fps testsrc2 H264 ultrafast CRF18 GOP60 fixed (-g 60 -keyint_min 60 -sc_threshold 0), 2 encoder threads, yuv420p, 48kHz AAC sine 440Hz, source 5808792 bytes SHA256 f1a29c250cc4498f401f5c6abc6c73a89c64330a2d2125364b828455517e6475, 240 video frames, 376 audio packets. All outputs target libx264 medium 900k; fixed total requested encoder threads 4, per-worker floor(4/W). For N>1, initial full CFR-grid probe; equal half-open intervals floor(F*i/N), seek 1 second early then copyts trim at (first-.5)/fps, reset PTS, exact frame count, no audio per segment, output video timescale 90000; per-part grid checks; concat safe 1 by index with explicit durations, original AAC copied once, faststart MP4. N=1 normal continuous. Full proxy validates source/output video and audio counts, <=2ms residual relative to common video origin, audio streamhash SHA256 equality, strict full decode, source SHA unchanged. Total excludes fixture creation, separate SSIM calculation, and baseline cached source timestamps/hash; not full source lease/metadata/atomic-publication contract. Two opposite-order repetitions; all 12 outputs passed proxy checks. No resource peak or quality-matched comparison. Raw results:

| rep | N | W | encode incl grid/parts/mux s | total s | bytes | SSIM |
|---:|---:|---:|---:|---:|---:|---:|
|0|1|1|1.01442|1.58454|974272|.988114|
|0|2|2|1.48198|2.09451|778018|.971187|
|0|4|2|2.07740|2.70407|577808|.949558|
|0|4|4|1.62149|2.22235|497202|.946421|
|0|8|2|2.25336|2.76620|410813|.915934|
|0|8|4|1.71997|2.24401|388997|.915382|
|1|8|4|1.81452|2.33071|388997|.915382|
|1|8|2|2.28601|2.84569|410813|.915934|
|1|4|4|1.46634|1.99110|497202|.946421|
|1|4|2|1.92438|2.48656|577808|.949558|
|1|2|2|1.57105|2.17441|776939|.971183|
|1|1|1|.98945|1.61130|974272|.988114|

Mean total s: 1/1 1.59792, 2/2 2.13446, 4/2 2.595315, 4/4 2.106725, 8/2 2.805945, 8/4 2.28736. None beat continuous. More workers help given equal segment count here, yet restart/seek/decode/validation/mux overhead and ABR size/quality changes make shorter segments worse. Causal attribution to any single component is unproven; compare matched size/quality, longer high-resolution and heterogeneous scene fixtures, keyframe-aware partition vs equal intervals. Do not interpret reduced output size as equal-quality speedup. Keep default continuous and 2-way user experiment. Local probe timing and source integrity checks not a universal frame-content guarantee. No Rust/fast-anything runtime executed. 
457eb66 exact CI 45566399287 job 139042144637 passed, zero warnings/errors, target guard and all prior regressions. Artifact 15000586159 outer 216494 bytes SHA256 9d5fe751331d79a8499d7bd003029b69c4a27c548003482316c66235c59c4fef; inner 213318 bytes SHA256 d95223875f3d05dbf72eff61516b36f858d93d0fdce1606653fc8eaba3b1511d. Current verified package https://github.com/WhoSia/VANTA/actions/runs/45566399287/artifacts/15000586159. Owner resize observation only; actual owner compression acceptance remains open.
45b7737 generalizes internal split research from fixed 2 to 2–16 segments and 1–8 bounded workers (workers <= segments; explicit encoder threads >= workers). Integer frame interval boundaries, worker queue, linked cancellation, index-ordered concat, mandatory existing production publication checks retained. Public EncodeAsync signature and default continuous / explicit 2-part, serial audio hash behavior unchanged; no user-facing arbitrary split selector. Internal research entrypoint also permits existing parallel audio validator, default false. CI 45567684805 in_progress: 3/4/8 unique marker frame identities, insufficient worker budget rejection, 12 actual full Encoder trials (1/1,2/2,4/2,4/4,8/2,8/4; two reverse-order repetitions) with SSIM, active cancellation and staging cleanup. Actual Windows results pending. No new version/tag/release. 0.4.0 proposal only; see PROPOSAL_0.4.0.md.


## CRF and keyframe alignment screen — 2026-11-25

Actual independent local FFmpeg 7.1.5-0+deb13u1, 5 visible logical CPUs, cgroup cpu.max 400000 100000 (4 equivalents). dotnet, rustc and fast-anything not on PATH; Python orchestration, not Windows VANTA or upstream fast-anything execution. Synthetic source: 640x360 30fps 10s testsrc2 + 440 Hz 48kHz sine, libx264 ultrafast CRF18 GOP60 (-g 60 -keyint_min 60 -sc_threshold 0), threads2, yuv420p, AAC. Source 3300548 bytes SHA256 cdc5669ae2d99de7a4b34f568c3ce3028ad056853a54bfc2457874a0df83cc8e, 300 frames, 470 audio packets. Actual decoded keyframes 0,60,120,180,240. Compare continuous medium CRF23, 4 requested encoder threads vs 4 segments / 2 workers each 2 requested threads, medium CRF23. Equal boundaries 0,75,150,225,300; nearest-keyframe boundaries 0,60,120,240,300 (ties earlier); aligned uneven lengths 60,60,120,60. Early seek max(0, first/30-1); exact-key candidate seek first/30. All parts use copyts, trim at (first-.5)/30 or 0, setpts=PTS-STARTPTS, exact frame counts, video-only 90000 timescale; safe concat by index with explicit durations, original AAC muxed once, faststart. Split timings include initial full grid probe, part encoding and grid checks, merge; continuous normal. Final check exact frame/audio packet counts, max 2ms residual relative to video start shift, audio streamhash equality, strict full decode. Source timestamps/audio hash cached before clock, source SHA rechecked after all trials. Total excludes fixture creation, SSIM measurement, initial cached source checks and temp deletion; not full VANTA lease/metadata/atomic publication. SSIM global and per-frame via stats_file, boundary minimum across frames b-2..b+1; continuous boundary_min field is whole-video minimum (not directly comparable as boundary metric). Two opposite-order repetitions, all 8 outputs passed, temporary files removed.

| rep | mode | encode/grid/mux s | total s | bytes | SSIM | min frame | min boundary |
|---:|---|---:|---:|---:|---:|---:|---:|
|0|continuous|.976385|1.542330|1037921|.994666|.992703|n/a|
|0|equal early|1.584666|2.098257|1043815|.994609|.992574|.992764|
|0|key early|1.415509|1.919472|1042532|.994603|.992655|.992655|
|0|key exact|1.381042|1.899371|1042532|.994603|.992655|.992655|
|1|key exact|1.373753|1.931282|1042532|.994603|.992655|.992655|
|1|key early|1.455542|1.998089|1042532|.994603|.992655|.992655|
|1|equal early|1.271671|1.771916|1043815|.994609|.992574|.992764|
|1|continuous|.721581|1.285846|1037921|.994666|.992703|n/a|

Mean total seconds continuous 1.414088; equal early 1.9350865; key early 1.9587805; key exact 1.9153265. All segmented outputs within 0.57% of continuous bytes and 0.000063 global SSIM; unlike earlier ABR mismatch, this is a near-matched-size/SSIM screen, not universal visual equivalence or a target-size guarantee. Continuous wins both repetitions. Exact-key seek improves over key-early in both pairs, but mean ~2.22% difference and only two trials; aligned vs equal reverses direction across reps, no robust partition win. Unequal segment lengths may hurt load balance. Do not activate CRF or exact seeking in production from one synthetic screen; exact source keyframe flag is not proof of closed GOP or safe random access for all files. Next after passing inherited CI: constrained partition planner (minimum interval, unique ordered cuts, seek safety), compare on longer 1080p/high-resolution heterogeneous GOP workloads with five matched trials, record resources and actual C# pipeline. Keep public continuous/2-part behavior and target bitrate contracts. No Rust dependency.
