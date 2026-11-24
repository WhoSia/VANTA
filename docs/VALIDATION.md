# Validation status

Baseline 9a123e7: [Windows CI #1](https://github.com/WhoSia/VANTA/actions/runs/45468218339) succeeded. WPF build, smoke checks, delayed AAC integration, Unicode paths, SHA-256 source preservation, overwrite protection, pre-cancel, preview publish and upload all passed. FFmpeg 9.1 essentials (Gyan) was used. This evidence applies to that exact SHA only.

Current revision adds a streaming per-frame/packet timestamp comparison to existing metadata endpoint checks. It permits a constant stream shift subject to the endpoint guard but rejects changing offsets and count mismatch; it is not proof of perceptual sync or identical video content. New code is pending CI. GUI remains analysis-only. Supported input remains one 8-bit SDR yuv420p video, mono/stereo AAC or no audio, known stream timing, no extra streams. No chunk assembly. No release.

Run on Windows:
```powershell
dotnet run --project tests/Vanta.Smoke
dotnet run --project tests/Vanta.Integration
dotnet build src/Vanta.App
```
