# Validation status

The GUI is an analysis-only prototype. Encoding is intentionally not exposed in it. Encoder draft supports a single pass only; planner currently proposes two-pass targets and these cannot yet be executed. Duration and decode checks do not establish A/V synchronization. Full timeline and signal-based regression fixtures are required before enabling encoding. No Windows build or .NET tests executed yet.

Run on Windows with .NET 10 SDK:

```powershell
dotnet run --project tests/Vanta.Smoke
dotnet build src/Vanta.App
dotnet run --project src/Vanta.App
```
