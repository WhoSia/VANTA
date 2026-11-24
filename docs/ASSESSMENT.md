# Development assessment — 0.2.0

Axes: value / implementation / evidence / integrity, [0,1], subjective engineering scores, not measured probabilities. No weighted sum.

| Area | Vector | Evidence / next gate |
|---|---|---|
| CI | (1,.97,.96,.93) | 19f90b5 passed; bc0554e pending |
| Continuous encoder | (1,.94,.95,.90) | existing matrix and paired benchmarks passed |
| Temporal integrity | (1,.94,.95,.90) | split, delayed AAC, frame markers; no gate relaxed |
| Size budget | (1,.89,.85,.65) | ABR uncertainty remains |
| Adaptive planner | (1,.65,.65,.55) | metadata heuristic; no measured quality |
| Batch queue | (.99,.86,.84,.80) | active/queued cancellation passed |
| Batch UX | (.99,.86,.55,.60) | build and core tests; interactive acceptance open |
| Output naming | (.98,.92,.86,.82) | shared destination reservation passed |
| Veryfast | (.97,.95,.90,.85) | matrix passed |
| Experimental split | (.94,.77,.78,.65) | repeated matched benchmark slower, remains opt-in |
| Performance instrumentation | (.98,.82,.45,.65) | phase report implemented; exact CI pending |
| Audio validation fusion | (.93,.15,.45,.45) | local 2-track packet hash positive/negative control, no production substitution |
| Source timebase preservation | (.96,.15,.40,.40) | local MKV counterexample and demux timebase trial, unsupported metadata remains |
| Hardware acceleration | (.95,.05,.05,.15) | device-specific tests needed |
| Toolchain | (1,.92,.85,.82) | owner dependency presence |
| Distribution | (.98,.78,.78,.65) | 165087-byte preview |

Priority: exact bc0554e CI and phase measurements; test shadow streaming packet hash comparator, preserve old streamhash/full decode until equivalence shown. Source timebase option must not broaden supported domain without evidence. Reassess finite 0.2.0 gates; CPU scheduling and hardware acceleration must be measured, not speculative. Keep split opt-in. No new version or release.
