# Stratified sampling counterexample

Experiment executed in Linux, FFmpeg 7.1.5 Debian; not VANTA C# or Windows integration. Source: 12 s, 320x180, 30 fps, H.264 yuv420p, 4 s gray + 4 s testsrc2 + 4 s blue. Synthetic fixture concatenation is input creation only, not VANTA final encoding. Re-encode entire source or 2 s samples starting at 1, 5, 9 s; libx264/libx265, medium, 180000 bps, threads 2, x265 pools=2:frame-threads=1:log-level=error. Sum video packet sizes via ffprobe. Sample extrapolation: first sample x6; sum of three x2. No audio, no quality measurement.

| Codec | Full payload bytes | Sample 1 | Sample 2 | Sample 3 | First extrapolation | Stratified extrapolation | Signed first error | Signed stratified error |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| H.264 | 304683 | 1756 | 36725 | 1775 | 10536 | 80512 | -96.54% | -73.58% |
| HEVC | 98610 | 1851 | 42640 | 1860 | 11106 | 92702 | -88.74% | -5.99% |

Full MP4 sizes: 309857 / 106135 bytes. Local elapsed seconds (single run, not portable benchmark): full .394/.728; three sample total .476/.866. Both sample strategies underestimate in this fixture; even stratification is not sufficient. Independent ABR state, GOP boundaries and short sample overhead differ from continuous encoding. Cannot establish which codec always performs better or extrapolate this single example to real video distribution.

Reproduce source:
```sh
ffmpeg -f lavfi -i 'color=c=gray:s=320x180:r=30:d=4' -f lavfi -i 'testsrc2=s=320x180:r=30:d=4' -f lavfi -i 'color=c=blue:s=320x180:r=30:d=4' -filter_complex '[0:v][1:v][2:v]concat=n=3:v=1:a=0[v]' -map '[v]' -c:v libx264 -threads 2 -pix_fmt yuv420p source.mp4
ffmpeg -i source.mp4 -c:v libx264 -b:v 180000 -preset medium -threads 2 full.mp4
ffmpeg -ss 1 -i source.mp4 -t 2 -c:v libx264 -b:v 180000 -preset medium -threads 2 sample.mp4
ffprobe -v error -select_streams v:0 -show_entries packet=size -of json sample.mp4
```
Repeat sample starts 5 and 9 and libx265 with the parameters above. Decision: no automatic sample-based size prediction in 0.2.0 absent better evidence; preserve clear bitrate budget labels. Optional future analysis can report sample-only measurements, not full-video guarantees. Focus current gate on executable plans, capability filtering, honest evidence and regressions.
