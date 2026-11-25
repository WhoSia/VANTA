# 0.3.0 — Desktop Reliability & Distribution Readiness

Explicit owner approval 2026-11-25 KST. In development. Baseline accepted 0.2.0 build 5a7f687, CI 45521060956, artifact 14980710817. Retain baseline regressions; no release/tag without separate approval.

| Required gate | Evidence required | Status |
|---|---|---|
| 0.3.0 version and coherent build | app/core/visible UI agree; exact Windows CI | Pending |
| Local distribution package | versioned ZIP, SHA-256, manifest or documented contents, measured app footprint | Pending |
| Safe local installation | explicit hash, new version directory only, no overwrite or partial publication, traversal rejection, Windows positive and negative regression | Pending |
| Dependency diagnosis | matching executable pair and runtime; actionable failure; no implicit install or unverified downloads | Partial from 0.2.0 |
| Desktop workflow | WPF state transitions and actual supported encode/cancel/result path tested; manual owner-PC checklist recorded separately | Partial from 0.2.0 |
| Supported media reliability | baseline A/V, visual metadata, source lease, batch and output collision regression; clear unsupported-input policy | Baseline pass; reverify |
| User acceptance | fresh extraction/install on owner PC, actual single and batch encode, result and cancel observations; no claims from CI alone | Pending owner evidence |
| Distribution handoff | exact verified artifact, instructions and rollback/side-by-side update, explicit limitations | Pending |

Development prioritization: subjective (value, implementation, evidence, integrity) vectors, hard gates first. Installer and package are local-only; no automatic release, background service, telemetry or network download. Clean-PC dependency installation/signing not represented as proven by CI; document limitations and stop at genuine owner action. Rotation normalization, FPS conversion and GPU production encoding remain separate optional decisions. Final 1.0 gates are not silently waived. When this gate is met or owner intervention is essential, pause automation and ask approval before any next version.

Owner UX expansion approved for prioritized development: Korean/English, quick examples/manual values, more-compression button, intuitive multi-file workflow. See [UX expansion](UX_EXPANSION_0.3.0.md). Include functional regression and language consistency before acceptance. AV1 and dynamic Auto resource strategy are measured research candidates, not required production features. Version baseline 8e7139d CI 45522448591 passed; package cb7f7da CI 45525030762 pending. UX f35f3a6 staged, not on main.
