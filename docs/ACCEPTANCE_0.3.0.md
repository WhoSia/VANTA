# 0.3.0 — Desktop Reliability & Distribution Readiness

Explicit owner approval 2026-11-25 KST. In development. Baseline accepted 0.2.0 build 5a7f687, CI 45521060956, artifact 14980710817. Retain baseline regressions; no release/tag without separate approval.

| Required gate | Evidence required | Status |
|---|---|---|
| 0.3.0 version and coherent build | app/core/visible UI agree; exact Windows CI | Pass at 193e402 |
| Local distribution package | versioned ZIP, SHA-256, manifest or documented contents, measured app footprint | Pass at 193e402; single artifact change pending |
| Safe local installation | explicit hash, new version directory only, no overwrite or partial publication, traversal rejection, Windows positive and negative regression | Pass on CI at 193e402 |
| Dependency diagnosis | matching executable pair and runtime; actionable failure; no implicit install or unverified downloads | Partial from 0.2.0 |
| Desktop workflow | WPF state transitions and actual supported encode/cancel/result path tested; manual owner-PC checklist recorded separately | Partial from 0.2.0 |
| Supported media reliability | baseline A/V, visual metadata, source lease, batch and output collision regression; clear unsupported-input policy | Pass at 193e402; latest CI pending |
| User acceptance | fresh extraction/install on owner PC, actual single and batch encode, result and cancel observations; no claims from CI alone | Pending owner evidence |
| Distribution handoff | exact verified artifact, instructions and rollback/side-by-side update, explicit limitations | Pending |

Development prioritization: subjective (value, implementation, evidence, integrity) vectors, hard gates first. Installer and package are local-only; no automatic release, background service, telemetry or network download. Clean-PC dependency installation/signing not represented as proven by CI; document limitations and stop at genuine owner action. Rotation normalization, FPS conversion and GPU production encoding remain separate optional decisions. Final 1.0 gates are not silently waived. When this gate is met or owner intervention is essential, pause automation and ask approval before any next version.

Owner UX expansion approved for prioritized development: Korean/English, quick examples/manual values, more-compression button, intuitive multi-file workflow. See [UX expansion](UX_EXPANSION_0.3.0.md). Include functional regression and language consistency before acceptance. AV1 and dynamic Auto resource strategy are measured research candidates, not required production features. Version baseline 8e7139d CI 45522448591 passed; package cb7f7da CI 45525030762 pending. UX f35f3a6 staged, not on main.

2026-11-25 update: native UI ee1d163 CI 45527578928 succeeded; bilingual 193e402 CI 45534589424 all stages passed, zero warnings/errors; package artifact 14985877592 (207220 outer bytes; 205215 inner ZIP bytes). Supersedes pending notes above. 2b9ec5a removes redundant raw preview artifact and adds bilingual START-HERE.txt, d69d58a updates download docs, 9d6f4aa localizes batch summary and adds a WPF check. Latest exact CI 45541599414 in progress. Verify only one user-facing package is uploaded. Actual owner PC acceptance, loaded-source language preservation and remaining app-owned diagnostics still open; no version advancement.

Latest checkpoint: exact 9d6f4aa CI 45541599414 succeeded with one package artifact 14989901527, zero warnings/errors. Korean batch summary and package verification passed. Real media WPF workflow 325af5935412d2c888aae5854148ae4c19af2acb is on main, CI 45546909751 pending. This tests shared application methods on STA Dispatcher with real FFmpeg media, not interactive dialogs/rendering or owner-PC acceptance. Auto scheduling local comparison in ASSESSMENT.md did not beat encoder-default Auto; retain default. No advancement.
