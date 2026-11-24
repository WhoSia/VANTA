# Development assessment — 0.2.0

Axes: user value / implementation coverage / evidence coverage / integrity confidence, [0,1], subjective engineering estimates, not measured reliability. No weighted sum. Hard gates take precedence.

| Area | Vector | Evidence / next gate |
|---|---|---|
| CI | (1,.97,.96,.93) | a2200f3 passed; 9458789 integration pending |
| Continuous encoder | (1,.94,.95,.90) | previous matrix and regressions |
| A/V integrity | (1,.94,.95,.90) | delayed AAC, VFR, markers, split cancellation |
| Size budget | (1,.89,.85,.65) | ABR uncertainty; honest labels |
| Adaptive planner | (1,.85,.78,.68) | explicit preference on main, build/smoke passed; heuristic quality unmeasured per input |
| Batch queue | (.99,.89,.88,.82) | previous thread-budget and cancellation tests |
| Batch UX | (.99,.87,.55,.60) | interactive acceptance open |
| Source consistency | (1,.65,.15,.45) | new staged guard; Windows share and stale tests unrun |
| Output naming | (.98,.92,.86,.82) | collision tests passed; dialog corrections staged |
| Progress | (.99,.88,.80,.75) | rounding bug found; correction staged |
| Experimental split | (.94,.77,.78,.65) | slower tested, opt-in |
| Performance instrumentation | (.98,.90,.85,.82) | phase accounting |
| Audio validation fusion | (.93,.72,.78,.65) | shadow only; legacy retained |
| Optional encoder budget | (.98,.87,.80,.76) | Windows tests passed |
| Hardware diagnostics | (.97,.90,.80,.75) | CPU positives, six hardware failures classified; actual GPU success untested |
| GPU compression | (.95,.05,.05,.15) | deferred until working device evidence and approved scope |
| Toolchain | (1,.92,.85,.82) | owner dependency presence |
| Distribution | (.98,.78,.82,.65) | 176321-byte preview |

Priority: finish exact 9458789 CI; 2abb61f already rebased. Staged source consistency candidate 7ad9630 parent 9458789, rebase instructions in SOURCE_INTEGRITY.md. Then review GUI acceptance checklist, honest per-input candidate evidence, and finite approved 0.2.0 gates. Hardware diagnostics are accepted at a2200f3, not production GPU compression. FPS conversion requires independent timing contract; no unvalidated change. No new version/tag/release before owner approval.
