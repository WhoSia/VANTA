# Development loop and approval gates

Current approved version: 0.1.0 — Lightweight Compression Core & Temporal Integrity.
Status: core acceptance passed at 7815ef5 (CI #4); awaiting owner approval before any next-version implementation. No new version/tag/release without approval.

Repeat Evaluate → Prioritize → Implement → Verify → Reassess. Use vectors (user value, implementation coverage, evidence coverage, integrity confidence) in [0,1]; label subjective judgments. No linear sum. Failed hard gates outrank enhancements; prioritize blockers and dependencies, then Pareto tradeoffs and smallest useful changes. Record evidence against the exact SHA. No claims of verification from inspection alone.

## Commit and CI lanes

- Keep small, coherent, meaningful commits for source, tests, documentation, design specifications and assessment updates. No artificial commit-count targets.
- Separate docs-only changes (docs/**, README.md) from build-affecting changes. The Windows workflow ignores docs-only pushes; mixed changes still trigger CI. XAML, resources, build scripts and workflow edits are build-affecting, not docs-only.
- While a CI run is pending, work independently on documentation, test design, review and prioritization, then check results again. Do not cancel a useful run just to update evidence or formatting. Avoid competing source writes; check HEAD and use lease/non-force updates for grouped commits.
- Batch coupled code/test changes so each tested revision is coherent. Superseded runs are not evidence. Map docs-only revisions to the exact previously tested build inputs. Workflow changes require operational validation.
- All source commits authored and committed by WhoSia. Actions read-only; no bot-authored commits.

## Stop conditions

Stop only when user intervention is genuinely required or the currently approved version's gate is met. Report evidence, limitations, minimal requested action and next proposed numeric version/subtitle. Normal numeric versions only; no P/R sub-lineages. No implementation of the next version until explicit approval. Scheduled automation must pause at this gate; no background promises outside scheduled runs.

0.1.0 gate: Windows build; core and real FFmpeg integration; guarded output publication; continuous encoding; relative endpoints and decoded timeline checks; active cancellation; extended/VFR and multiple-audio regression; documented input domain. GUI activation and richer plan controls remain subsequent scope. Release scope stays finite.
