# Desktop acceptance checklist (not executed)

Use a fresh extraction of an exact successful CI artifact. Record SHA, run ID, Windows version, FFmpeg/FFprobe version, source characteristics, result and observed error. Use copies of expendable test videos; no private video upload necessary. Owner launch/dependency availability reported; actual interactive encode acceptance remains open.

| Case | Action | Expected result |
|---|---|---|
| Launch | Start Vanta.App.exe with matching FFmpeg pair | No dependency error; normal initial screen |
| Single | Drop supported 8-bit SDR AAC MP4 | Source details, up to 3 plans or explanation |
| Budget | Apply target, edit bitrate/preset/height | Budget updates; impossible target rejected; actual size not guaranteed |
| Codec preference | On build containing 9458789, choose H.264 then HEVC | Plans regenerated for selection, previous manual settings reset; no silent substitution |
| Save | Encode to a new filename, then try existing filename | Output validated before publication; existing file unchanged |
| Progress | Encode and observe validation | Validation distinct from encoding; source guard candidate fixes premature rounded 100% when verified |
| Batch | Drop 3 clips, set 2 jobs, shared destination | Unique names, correct status, failure isolation, completed outputs preserved |
| Cancel | Cancel active and queued work | Unfinished outputs absent, temporary files cleaned |
| Retry | Retry selected failed/canceled row | No implicit retry on unrelated profile changes |
| Source change | Once source consistency candidate verified, modify expendable input after analysis | Stale plan rejected, reopen/re-add required |
| Hardware diagnostic | Click explicit diagnostic | Bounded sample result, no automatic GPU encoding |
| Optional split | Supported CFR H.264 source, explicit split checkbox | Same validation gate; unsupported VFR rejected, no speed guarantee |

Record pass/fail/not run individually. CI evidence is not manual acceptance. Hardware diagnostics need not pass on GPU-less hardware. Stop at version gate or genuine owner intervention; no new version/release without approval.
