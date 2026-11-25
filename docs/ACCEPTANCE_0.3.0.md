# 0.3.0 — Desktop Reliability & Distribution Readiness

Owner approval 2026-11-25 KST; in development. Baseline accepted 0.2.0 build 5a7f687. Latest fully tested build inputs f04964c49025bf923174753b03222d0001514922, CI 45549751279, job 138987821755, zero warnings/errors. Subsequent source ebc9d7c is not yet accepted.

| Required gate | Current evidence | Status |
|---|---|---|
| Version and coherent build | Exact f04964c CI | Pass |
| Local distribution package | One versioned artifact, inner SHA-256, manifest, measured bytes | Pass |
| Safe local installation | Positive, wrong hash, collision, version mismatch, traversal and cleanup CI | Pass |
| Dependency diagnosis | Core same-directory matching version and codec checks; launcher presence/runtime check only, clean-machine install untested | Partial |
| Desktop workflow | Actual FFmpeg single/batch, language state, cancellation/result, logical layout test | Automated pass; interactive owner test open |
| Supported media reliability | A/V, visual, source lease, codec/preset, collision and cancellation regressions | Pass at f04964c; stricter error checks pending |
| User acceptance | Fresh extraction, actual interaction, DPI and keyboard on owner PC | Pending owner evidence |
| Distribution handoff | Verified single package, bilingual instructions, side-by-side extraction and limitations | Pass for development preview |

Do not treat CI as interactive acceptance. Remaining blocking work: exact new CI, dependency diagnostic consistency and minimal owner-PC checklist. Optional AV1, adaptive threads and native acceleration remain research, not mandatory production additions. No version/tag/release advancement. Once remaining automated gates pass and owner-PC action is the sole blocker, pause automation and request the minimal checklist.

## Historical checkpoints (superseded by current table)

Development prioritization: subjective (value, implementation, evidence, integrity) vectors, hard gates first. Installer and package are local-only; no automatic release, background service, telemetry or network download. Clean-PC dependency installation/signing not represented as proven by CI; document limitations and stop at genuine owner action. Rotation normalization, FPS conversion and GPU production encoding remain separate optional decisions. Final 1.0 gates are not silently waived. When this gate is met or owner intervention is essential, pause automation and ask approval before any next version.

Owner UX expansion approved for prioritized development: Korean/English, quick examples/manual values, more-compression button, intuitive multi-file workflow. See [UX expansion](UX_EXPANSION_0.3.0.md). Include functional regression and language consistency before acceptance. AV1 and dynamic Auto resource strategy are measured research candidates, not required production features. Version baseline 8e7139d CI 45522448591 passed; package cb7f7da CI 45525030762 pending. UX f35f3a6 staged, not on main.

2026-11-25 update: native UI ee1d163 CI 45527578928 succeeded; bilingual 193e402 CI 45534589424 all stages passed, zero warnings/errors; package artifact 14985877592 (207220 outer bytes; 205215 inner ZIP bytes). Supersedes pending notes above. 2b9ec5a removes redundant raw preview artifact and adds bilingual START-HERE.txt, d69d58a updates download docs, 9d6f4aa localizes batch summary and adds a WPF check. Latest exact CI 45541599414 in progress. Verify only one user-facing package is uploaded. Actual owner PC acceptance, loaded-source language preservation and remaining app-owned diagnostics still open; no version advancement.

Latest checkpoint: exact 9d6f4aa CI 45541599414 succeeded with one package artifact 14989901527, zero warnings/errors. Korean batch summary and package verification passed. Real media WPF workflow 325af5935412d2c888aae5854148ae4c19af2acb is on main, CI 45546909751 pending. This tests shared application methods on STA Dispatcher with real FFmpeg media, not interactive dialogs/rendering or owner-PC acceptance. Auto scheduling local comparison in ASSESSMENT.md did not beat encoder-default Auto; retain default. No advancement.

Real media WPF workflow 325af59 exact CI 45546909751 passed with zero warnings/errors; includes analyzed source, loaded-language settings, validated output and collision UI. Latest verified package artifact 14991847535. 0685a93 adds distinct single/batch command, shared speed preset and actual two-file WPF regression; CI 45547843938 pending. Owner-PC interaction, rendering/DPI, and additional reliability checks remain open. Preset speed screening supports explicit choice, not a guaranteed speedup; keep medium default. No advancement.

Checkpoint: 0685a93 CI 45547843938 passed, including shared preset, old preferences migration, actual two-video WPF batch and installer. 40087b4 authoritative terminal outcomes, elapsed wall time, cancellation reconciliation and tests now on main; CI 45549388454 pending. UI rendering/DPI and manual owner acceptance remain open. Performance policy unchanged, fused comparator shadow only, experimental split opt-in. Next: exact CI; if successful check real viewport and cancellation/selection usability, evaluate validation equivalence rather than weakening gates. No version advancement.

Checkpoint: exact 40087b4 CI 45549388454 passed terminal outcome/cancellation and complete prior regressions, package artifact 14992757593. f04964c narrow-window layout candidate is now main, CI 45549751279 pending. Rust screening in ACCELERATION.md supports deferring new native dependency until managed profiling identifies a material hot path; no Rust benchmark was executed. Actual user-PC acceptance and real DPI/display tests remain separate. No version advancement.


Latest: f04964c CI passed; artifact 14993331013. 7492d50 adds actual managed parser differential/benchmark screen; ebc9d7c strict error-level stderr and required visual metadata validation. CI 45551368705 queued at record creation; no measured C# result yet, no production parser switch. Existing full decode, time axis, audio hash and source protection retained.
