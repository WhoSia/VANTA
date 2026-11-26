# Development loop and approval gates

Current approved version: 0.4.0 — Adaptive Segmented Encoding & Measured Acceleration.
Status: owner explicitly approved 0.4.0 on 2026-11-25 KST and requested continued development. 0.3.0 last verified build 457eb66, interactive owner acceptance pending; retain that limitation. 0.2.0 core/development-preview gate accepted at 5a7f687 (CI 45521060956); 0.1.0 core accepted at 7815ef5. No new version/tag/release without approval.

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

Owner explicitly requested multi-video parallel processing and detailed UX convenience within 0.2.0. Permit bounded independent batch preview, per-video plan review, safe output naming and cancellation; require tests and do not relax temporal integrity. Replace single-video-only product restriction with single-file and bounded batch modes. No chunk concatenation. No version advancement or release/tag without approval.

Owner requested optional single-video acceleration on 2026-11-25. Allow a separately labeled opt-in, strictly validated two-part CFR experiment; default continuous encoding and continuous batch jobs remain unchanged. This supersedes the blanket exclusion of experimental chunk concatenation, not the integrity gate. No auto selection or default enablement; reject unsupported input and do not publish failed validation. A speedup must be measured end-to-end with quality, size and resource costs; no version/tag/release change without approval.

## Current 0.3.0 gate

See ACCEPTANCE_0.3.0.md. Develop a finite desktop-reliability and distribution-readiness increment, keeping all 0.2.0 regressions. Prefer verified local archive installation, dependency diagnostics, desktop workflow tests, and real usability fixes. No new release/tag, GPU encoding, FPS conversion or rotation normalization implied by approval. A genuine owner-PC acceptance requirement stops the loop with a minimal checklist; no fabricated manual test. Historical 0.2.0 instructions above describe prior approved scope.

## Current 0.4.0 gate

Follow ACCEPTANCE_0.4.0.md and approved PROPOSAL_0.4.0.md. Repair failed inherited CI first. Bounded internal multi-split, matched-quality/time/size and resource research, adaptive admission, validation-preserving acceleration; do not activate unproven defaults. Keep 0.3.0 fallback and all integrity checks. No tagged release or 0.5.0 without separate approval. Stop at useful CI wait or actual owner-only blocker; pause automation at acceptance gate.

## Comparative engineering rule (owner requested 2026-11-26)

Treat original VANTA algorithms, upstream implementations and simplest baselines symmetrically. Implementation novelty, author identity, number of commits and lines changed are not acceptance evidence. Prefer the simpler existing implementation when measured benefit does not justify complexity. Read primary upstream documentation/source; record precise behavior, provenance, applicable license and differences before importing code. Do not label a design comparison as an executed comparison against another application. Benchmark the existing default, upstream-native knobs and candidates using the same inputs, versions, measurement boundaries and quality/size criteria. Report all attempts including acquisition and correctness failures, all nonmatched pairs and repeated measurements, with raw evidence accessible. Do not cherry-pick winners, silently drop failures, pool duplicated content as independent samples, or change thresholds after looking at results. Separate engineering judgment from empirical measurements. Use frequent coherent commits with regression tests; group compatible pending commits into one non-force promotion to avoid superseding useful CI. Preserve existing integrity gates and finite version scope.

## First-pass decision protocol and native-language lane (2026-11-26)

Use docs/decision/README.md, inputs.json, model.mjs, results.json and EMPIRICAL.md. Separate hard gates, subjective multi-attribute scenario sensitivity, empirical paired results, and deployment decisions. Twelve 0–1 axes in four groups; report interval, first/top-three rank frequency, pairwise, regret, profiles and sensitivity, not a falsely precise priority scalar. Report provenance and assumptions. Correlate repeated content/resolution measurements by content group; preserve failures. Recompute on new evidence, not to rationalize a preferred choice. A required gate takes priority regardless of score. Native code candidates may be developed now in an isolated lane using C/Rust plus existing C#, but no production loading/distribution until differential, Windows ABI, end-to-end and maintenance gates. Benchmark upstream encoders including rav1e and SVT-AV1, do not assume a language is faster. No version bump or RITHM project changes implied.

## Universal media requirement (owner 2026-11-27)

Treat ordinary hundreds-of-MB to tens-of-GB workloads, typical 1080p and common fractional/integer rates through 120 fps including VFR, common containers and audio as required product coverage, not optional utility. Do not require owner-specific hardware or clip information to continue general engineering. Distinguish sparse offset proof, physical byte throughput, decoded duration and independent content coverage. Preserve all safety/integrity gates, explicit unsupported handling, finite approved version and verified release requirements.
