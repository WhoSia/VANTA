# 0.2.0 acceptance ledger — provisional

Approved scope: Adaptive Compression Planning & Granular Control. Do not advance version or publish a release without owner approval. This ledger distinguishes the approved 0.2.0 core/preview gate from final 1.0 desktop acceptance. Not an acceptance declaration.

| Gate | Evidence | Status |
|---|---|---|
| Executable H.264/HEVC MP4 | 16-cell matrix; 45520375931 exact regression | Pass |
| Source-aware distinct feasible bitrate tiers or shortage | capability and explicit codec smoke; heuristic limits documented | Pass |
| Target-size budget solver | inverse-budget smoke, copied audio included, not guaranteed size | Pass |
| Editable codec/height/preset/bitrate | WPF construction/editor tests; exact bitrate parser correction at 5a7f687 | Pending latest regression |
| Honest estimates and evidence | explicit bitrate budget labels, measured quality unknown, negative actual savings retained | Pass within stated heuristic scope |
| A/V preservation and publication | delayed AAC, VFR, NTSC, multi-track, decoded output, hashes, no overwrite, rotation rejection | Pass at cd13a25 |
| Independent batch and individual settings | mixed codec/height, cancellation and failure isolation; post-publication observer correction 0641a78 | Pending latest regression |
| Usable preview | WPF control tests and artifact, external dependencies, owner previously reported launch/dependency availability | Pass for build/package; manual desktop acceptance open |
| Optional split | bounded CFR experiment with same validation; matched trials slower | Retain explicit opt-in only; not performance claim |
| Latest coherent build | 5a7f687 CI 45521060956 | Pending |

Latest fully verified build at record creation: cd13a25, CI 45520375931, artifact 14979553753, 193273 compressed bytes excluding runtime and FFmpeg. Exact newest source changes: 0641a78 (completion notification cannot cause contradictory Failed after publication), 5a7f687 (shared exact bitrate parser and stale target cleanup, WPF and core boundary tests). Require exact successful full CI before declaring the gate met. The observer change does not establish reliable delivery of notifications that throw; it prevents false encoding failure after publication.

Remaining scope: verify latest CI and audit acceptance against actual source and evidence; fix any blocking defects, not speculative enhancements. If gates pass, record exact acceptance SHA and artifact, pause scheduled development, present owner with next proposed version/subtitle and explicit approval request. Final interactive Windows acceptance, release signing/installer and broader input support are distinct future gates per DEVELOPMENT_LOOP.md and RELEASE_SCOPE.md. GPU encoding, rotation normalization, FPS conversion and unverified acceleration are deferred. No claim of completed manual desktop testing.
