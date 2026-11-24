# Development loop and approval gates

Current approved version: 0.2.0 — Adaptive Compression Planning & Granular Control.
Status: owner approved 0.2.0 on 2026-11-25 KST; active development. 0.1.0 core accepted at 7815ef5. No new version/tag/release without approval.

Repeat Evaluate → Prioritize → Implement → Verify → Reassess. Use vectors (user value, implementation coverage, evidence coverage, integrity confidence) in [0,1]; label subjective judgments. No linear sum. Failed hard gates outrank enhancements; prioritize blockers and dependencies, then Pareto tradeoffs and smallest useful changes. Record evidence against the exact SHA. No claims of verification from inspection alone.

## Commit and CI lanes

- Keep small, coherent, meaningful commits for source, tests, documentation, design specifications and assessment updates. No artificial commit-count targets.
- Separate docs-only changes (docs/**, README.md) from build-affecting changes. The Windows workflow ignores docs-only pushes; mixed changes still trigger CI. XAML, resources, build scripts and workflow edits are build-affecting, not docs-only.
- While a CI run is pending, work independently on documentation, test design, review and prioritization, then check results again. Do not cancel a useful run just to update evidence or formatting. Avoid competing source writes; check HEAD and use lease/non-force updates for grouped commits.
- Batch coupled code/test changes so each tested revision is coherent. Superseded runs are not evidence. Map docs-only revisions to the exact previously tested build inputs. Workflow changes require operational validation.
- All source commits authored and committed by WhoSia. Actions read-only; no bot-authored commits.

## Stop conditions

Stop only when user intervention is genuinely required or the currently approved version's gate is met. Report evidence, limitations, minimal requested action and next proposed numeric version/subtitle. Normal numeric versions only; no P/R sub-lineages. No implementation of the next version until explicit approval. Scheduled automation must pause at this gate; no background promises outside scheduled runs.

0.2.0 gate: executable H.264/HEVC MP4 configurations, source-aware feasible distinct plans or explicit shortage, target-size budget solver, bounded editable codec/height/preset/bitrate controls, honest size estimates and vector evidence labels, core/GUI build and integration regressions including preserved A/V timeline. Explicit FPS reduction requires separate timeline validation and is not exposed until verified. GUI compression activation, dependency acquisition and final desktop acceptance remain subsequent scope. No new version without owner approval.

Owner requested a usable installable preview during 0.2.0. Permit guarded desktop encoding and lightweight preview packaging now; final desktop acceptance and stable distribution remain later gates. Do not advance version or create release/tag without approval. Maintain CI read-only and distinguish artifact availability from installation/interactive acceptance.
