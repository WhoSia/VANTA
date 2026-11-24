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
