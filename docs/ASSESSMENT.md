# Development assessment — 0.2.0

Axes: user value / implementation coverage / evidence coverage / integrity confidence, [0,1], subjective engineering estimates, not measured reliability. No weighted sum. Hard gates take precedence.

| Area | Vector | Evidence / next gate |
|---|---|---|
| CI | (1,.97,.96,.93) | 9458789 passed; b27dbca pending |
| Continuous encoder | (1,.94,.95,.90) | previous matrix and regressions |
| A/V integrity | (1,.94,.95,.90) | delayed AAC, VFR, markers, split cancellation |
| Size budget | (1,.89,.85,.65) | ABR uncertainty; honest labels |
| Adaptive planner | (1,.89,.85,.72) | explicit preference CI passed; heuristic quality unmeasured per input |
| Batch queue | (.99,.89,.88,.82) | previous thread-budget and cancellation tests |
| Batch UX | (.99,.87,.55,.60) | individual override candidate staged; interactive acceptance open |
| Source consistency | (1,.65,.15,.45) | guard on main; Windows share and stale tests pending |
| Output naming | (.98,.92,.86,.82) | collision tests passed; dialog corrections pending CI |
| Progress | (.99,.88,.80,.75) | rounding correction on main, CI pending |
| Experimental split | (.94,.77,.78,.65) | slower tested, opt-in |
| Performance instrumentation | (.98,.90,.85,.82) | phase accounting |
| Audio validation fusion | (.93,.72,.78,.65) | shadow only; legacy retained |
| Optional encoder budget | (.98,.87,.80,.76) | Windows tests passed |
| Hardware diagnostics | (.97,.90,.80,.75) | CPU positives, six hardware failures classified; actual GPU success untested |
| GPU compression | (.95,.05,.05,.15) | deferred until working device evidence and approved scope |
| Toolchain | (1,.92,.85,.82) | owner dependency presence |
| Distribution | (.98,.78,.82,.65) | 177748-byte preview |

Priority: exact b27dbca CI; 7ad9630 already rebased. Staged per-file settings 651a9fc after source consistency CI success, safe rebase instructions in BATCH_UX.md. Verify mixed codec/height and override precedence, then finite 0.2.0 gate review. GPU compression and FPS conversion deferred until independent verified scope. No new version/tag/release before approval.

2026-11-25 update (supersedes earlier pending notes): source lease b27dbca, individual settings f680b4d, batch results 49ec6f2, preferences 10fcfa3 all passed exact CI. Subjective vectors: source consistency (1,.90,.92,.85), individual batch (.98,.90,.86,.80), batch results (.98,.89,.85,.80), preferences (.97,.90,.85,.80). WPF control smoke and single result access 0d8818a on main pending CI; GUI verification (.99,.65,.15,.40). Prioritize metadata contract, then desktop acceptance and finite scope. No version advancement.

Update: 0d8818a exact CI passed. GUI control smoke (.99,.75,.65,.65); interactive acceptance still open. Visual metadata (.99,.70,.35,.50): 067196a on main, CI pending, local synthetic anamorphic scaling evidence only. Rotation fixture creation unsuccessful; transform support not established. Prioritize actual rotation fixture and color tag regression, then finite desktop acceptance.

## Final 0.2.0 assessment (supersedes earlier pending entries)

Exact 5a7f687 CI passed; all nine mandatory core/preview gates accepted. Subjective vectors (value, implementation, evidence, integrity): planner (1,.89,.88,.75); continuous encoder (1,.95,.95,.90); A/V (1,.95,.95,.90); source guard (1,.90,.92,.85); visual metadata (.99,.82,.86,.82); per-file batch (.98,.93,.90,.85); preferences (.97,.90,.85,.80); GUI controls (.99,.80,.70,.70); distribution (.98,.80,.85,.70); full interactive acceptance (1,.35,.15,.35). No weighted aggregate; these are judgment scores, not reliability measurements. Accepted core/preview does not imply 1.0 or clean-PC installation acceptance. Freeze at 0.2.0. Proposed next version 0.3.0 — Desktop Reliability & Distribution Readiness, awaiting owner approval.
