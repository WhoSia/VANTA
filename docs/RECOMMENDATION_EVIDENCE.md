# Recommendation evidence — 0.2.0

2026-11-25 KST. Actual local FFmpeg 7.1.5 Debian; 5 visible CPUs, 4-CPU-equivalent cgroup quota, no dotnet executable. Two silent 640x360, 30 fps, 4s H.264 sources encoded using medium CRF18, two encoder threads, yuv420p. Source A testsrc2, source B same generator plus noise=alls=14:allf=t+u:all_seed=42. Source A 593103 bytes, video bitrate 1181832 bps, density .17098; B 5707228 bytes, 11409754 bps, density 1.65072. Both fall in the existing high-density branch. Emulated current planner's rate fractions (HEVC .80/.55/.30; AVC .95/.85/.75), integer truncation, same height, medium preset. All twelve trials executed once. H.264 and HEVC use -threads 2; HEVC additionally x265-params pools=2:frame-threads=1:log-level=error. Thus not equivalent total thread allocation; time includes encode subprocess only, excludes source generation, SSIM, VANTA validation. SSIM calculated against each compressed source, not pristine generator. No real-world corpus or perceptual guarantee.

| Fixture | Codec | Tier | Target bps | Budget bytes | Actual bytes | Encode s | SSIM |
|---|---|---|---:|---:|---:|---:|---:|
| Motion | HEVC | Quality | 945465 | 475096 | 474509 | 1.3579 | .996434 |
| Motion | HEVC | Balanced | 650007 | 326629 | 331924 | 1.1328 | .993300 |
| Motion | HEVC | Compact | 354549 | 178161 | 182883 | .9734 | .984610 |
| Motion | AVC | Quality | 1122740 | 564177 | 539077 | .5493 | .997819 |
| Motion | AVC | Balanced | 1004557 | 504790 | 486875 | .5250 | .997102 |
| Motion | AVC | Compact | 886374 | 445403 | 433690 | .5583 | .996171 |
| Noise | HEVC | Quality | 9127803 | 4586721 | 5431318 | 6.3019 | .950124 |
| Noise | HEVC | Balanced | 6275364 | 3153370 | 3505844 | 5.0810 | .908457 |
| Noise | HEVC | Compact | 3422926 | 1720020 | 1797912 | 3.5531 | .869031 |
| Noise | AVC | Quality | 10839266 | 5446731 | 6279922 | 2.2197 | .987359 |
| Noise | AVC | Balanced | 9698290 | 4873391 | 5596135 | 2.1247 | .971520 |
| Noise | AVC | Compact | 8557315 | 4300051 | 4726886 | 2.0654 | .947891 |

Reproduce input: ffmpeg -hide_banner -nostdin -v error -n -f lavfi -i "testsrc2=s=640x360:r=30:d=4" -an -c:v libx264 -preset medium -crf 18 -threads 2 -pix_fmt yuv420p source.mp4. Append noise filter above for B. ffprobe -v error -show_streams -of json source.mp4. Output: ffmpeg -hide_banner -nostdin -v error -n -i source.mp4 -map 0:v:0 -an -c:v libx265 -preset medium -b:v TARGET -threads 2 -fps_mode passthrough -x265-params pools=2:frame-threads=1:log-level=error output.mp4. Replace codec and omit x265 options for AVC. SSIM: ffmpeg -hide_banner -nostdin -i source.mp4 -i output.mp4 -filter_complex_threads 2 -filter_complex "[0:v][1:v]ssim" -an -f null -. 

Decision: retain heuristic as budget tier, not measured quality. Noise AVC Quality actually grows ~10%; the HEVC Quality SSIM drops to .950124. Two synthetic fixtures cannot justify global calibration or comparative codec superiority. Provide explicit codec selection for recommendations, do not silently substitute or force HEVC. Candidate 2abb61fd76c8768cfe9196af9aa6d3d04a63b2c6 (parent a2200f3be3f249d364daa1e82e8f03441011e8c9) adds Planner preferredCodec with no fallback for unavailable explicit selection, single and batch recommendation controls, clear budget labels, smoke tests, resets stale target and split option when rebuilding single plans. Auto still prefers HEVC; manual codec editing remains separate. Not on main while CI 45506058355 runs. After success, compare original and current blobs for Compression.cs, MainWindow.xaml/.cs, BatchWindow.xaml/.cs, tests/Vanta.Smoke/Program.cs; ensure new src/Vanta.App/RecommendationChoices.cs absent. Rebase intended changes onto current HEAD with lease, preserve docs. If failure fix first. No new branch or release. Interactive UI acceptance remains open.

Candidate 2abb61f was rebased on main as 94587891afa5761cbdb40121d634221a7c2cf2e6 after exact recursive tree comparisons confirmed seven changed paths had no concurrent changes, including absent new file. Previous rebase instructions are historical; do not replay. CI 45506676529 pending full integration; WPF and smoke passed. No new quality measurement or calibrated model claimed.
