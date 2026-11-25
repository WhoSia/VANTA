# 0.2.0 acceptance ledger — PASS (core and development preview)

Approved version: 0.2.0 — Adaptive Compression Planning & Granular Control.

Accepted build inputs: 5a7f687dd548f067f43522cb72b20eabad0ec4d6. Windows CI [45521060956](https://github.com/WhoSia/VANTA/actions/runs/45521060956), job 138892717871, completed successfully, zero warnings and errors. Artifact 14980710817, 193397 compressed bytes (application only; excludes .NET runtime and FFmpeg). CI artifact is a development preview, not a signed or tagged release.

| Required gate | Exact CI evidence or source contract | Verdict |
|---|---|---|
| H.264/HEVC MP4 | All 16 codec/preset/fixture combinations | Pass |
| Source-aware distinct feasible budget plans or explicit shortage | Core smoke: distinct ordered budgets, codec availability, preferred codec, insufficient budget | Pass |
| Target-size solver | Inverse budget including copied audio; boundary tests | Pass |
| Editable codec, height, preset, bitrate | WPF build, individual editor routed-event tests, exact shared bitrate parser boundary tests | Pass |
| Honest estimates and evidence | Budget labels; measured quality unknown; signed actual savings; documented ABR uncertainty | Pass |
| A/V and output integrity | Delayed AAC, multi-audio VFR, NTSC, audio hashes, full decode, no overwrite, source lease, declared visual metadata and rotation rejection | Pass |
| Batch and per-file settings | Mixed codec/height, failure isolation, active/queued cancel, post-publication observer regression | Pass |
| Usable development preview | WPF control smoke, framework-dependent publish, dependency launcher check, uploaded artifact | Pass (build/package only) |
| Latest coherent build | Exact successful CI above; recursive tree audit found only docs/ACCEPTANCE_0.2.0.md and docs/VALIDATION.md changed between tested SHA and pre-seal HEAD 368ccd99495fc6aedac248221ebb3c26ea5a2aba | Pass |

Optional split: retain explicit opt-in, H.264 CFR only, shared validation. Matched tests did not demonstrate a speed advantage. Hardware diagnostics are not production GPU encoding. Full desktop interaction, real-world quality calibration, clean-machine dependency installation, signing, stable distribution and broader input/rotation/FPS support are not accepted by this gate. A/V validation is limited to supported source contracts and tested fixtures; no universal guarantee. A failed completion observer may lose a notification but cannot reclassify a published batch output as failed in the core.

Disposition: 0.2.0 approved core/development-preview scope accepted. Freeze development pending explicit owner approval of next version. Do not change version, tag or release. Proposed (not approved): **0.3.0 — Desktop Reliability & Distribution Readiness**. Prioritize manual/automated desktop acceptance, safe installer and verified dependencies, real-world compatibility and targeted UX defect fixes. GPU/rotation/FPS are separate optional scope decisions, not automatic inclusion. Pause the scheduled development loop at this gate.
