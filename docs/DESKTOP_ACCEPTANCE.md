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

## 0.3.0 owner-PC minimal handoff (not executed)

Use the latest successful package linked in README, record exact package commit from package.json. Check these five cases using expendable supported test videos, not private footage: (1) fresh extraction and normal launch with CheckOnly, (2) select three plans, quick and manual settings, language switch, save and play a single output; (3) add two or more videos, select shared preset/individual override and destination, start, cancel or finish, check original and completed files; (4) at normal and Windows 125%/150% display scale, shrink window and use Tab/keyboard to reach primary action and settings; (5) close/reopen language preference, verify earlier extracted version remains available. Report pass/fail/not run and exact observed issue. CI already exercises the underlying workflows and logical layout; do not ask owner to repeat entire integration suite. Automated CI passed at b77ea83 (45559737020). Use [this verified package](https://github.com/WhoSia/VANTA/actions/runs/45559737020/artifacts/14996559713). Owner checklist remains unexecuted; report five case results (pass/fail/not run) and any error. No need to install SDK or reinstall working FFmpeg/runtime. A clean-machine dependency installation is not required if the owner already has working dependencies; record that limitation.
