# Development loop and approval gates

Operate within the currently approved version: 0.1.0 — Lightweight Compression Core & Temporal Integrity.

Repeat Evaluate → Prioritize → Implement → Verify → Reassess within active development work. Use vectors (user value, implementation coverage, evidence coverage, integrity confidence) in [0,1]; label subjective judgments. Do not collapse into a linear sum. Failed hard gates outrank enhancements; prioritize blockers and dependencies, then Pareto tradeoffs and smallest useful changes. Record evidence against the exact SHA. No claims of verification from code inspection alone.

Stop for user intervention only when genuinely needed (e.g. inaccessible resource, permission, final desktop acceptance) or when the current version's gates are met. Report the exact blocker and minimal action. No version number, subtitle, tag or release change without explicit owner approval. Ordinary commits do not change the version. Use normal numeric versions, not P/R sub-lineages. No unattended promise: CI runs independently; agent development loops execute only during active sessions unless explicitly scheduled.

0.1.0 gate: Windows build; core and real FFmpeg integration; guarded output publication; full continuous encoding; relative stream endpoint and decoded timeline checks; cancellation during active encoding; long/VFR and multiple-audio regression; documented supported input domain. GUI compression activation and richer plan controls remain subsequent scope. Release scope stays finite; no unrelated feature additions.

All source commits must be authored and committed by WhoSia. Actions run read-only; no bot-authored commits.
