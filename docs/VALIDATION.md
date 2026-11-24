# Validation status

GUI analysis-only; encoding deliberately disabled pending regression and Windows acceptance. Planner and encoder now agree on single pass. Encoder restricts input to a single 8-bit SDR video and mono/stereo audio with known timing; refuses extra streams rather than dropping them. Relative stream endpoint comparison and full decoding are preliminary checks, not proof of perceptual sync. No C# tests run yet.

Run on Windows with .NET 10 SDK:

```powershell
dotnet run --project tests/Vanta.Smoke
dotnet build src/Vanta.App
dotnet run --project src/Vanta.App
```
