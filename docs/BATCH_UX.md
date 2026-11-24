# Batch preview UX contract — 0.2.0

Owner requested multiple independent video jobs, convenience and UI/UX refinement on 2026-11-25. This extends the original single-video preview scope; keep the final product finite. No chunk encoding or concatenation of a single video. A maximum of two concurrent jobs is a provisional CPU/memory guard, not a throughput guarantee. Every job uses the same Encoder validation and audio/timestamp gates.

- Open or drag one video for granular manual settings; drag several or click Batch compression for a review queue. Add, remove, clear, choose common profile and concurrency 1 or 2. Each source gets its own metadata-dependent plan.
- No automatic start. Show destination and approximate size before the user explicitly starts. Missing profile/unsupported input is skipped with reason; never silently substitute an aggressive profile.
- Output beside source: clip_vanta.mp4; if occupied or reserved, clip_vanta_2.mp4 and so on. Case-insensitive reservation includes all queued input paths. Actual encoder refuses overwrite if a collision appears later. Completed files remain after cancellation or unrelated job failure.
- Each job shows queued/encoding percentage/validating/complete/failed/canceled, with details. Cancellation stops queued/active work and cleans temporary outputs; output publication already completed remains valid.
- Remaining UX priorities: per-row retry, destination folder choice, compact columns (filename plus tooltip/path), keyboard and high DPI review, explicit aggregate size and count, copyable diagnostics, tested close/cancel behavior. Do not claim interactive acceptance from compilation. Avoid scope creep such as a job history database or permanent background worker.
- Performance: compare serial and two concurrent jobs with the same source cohort; CPU oversubscription, thermal load, RAM and actual elapsed time remain unknown. Parallel does not necessarily mean faster. Default 2 can be changed to 1.

Implementation 14eebd0 pending exact-SHA Windows CI. Tests include reserved names, collisions and two successful jobs with an isolated failed job. GUI interaction and batch cancellation not yet directly tested.
