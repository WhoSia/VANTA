# Development assessment — 0.2.0

Axes: value / implementation / evidence / integrity, [0,1], subjective engineering scores, not measured probabilities. No weighted sum.

| Area | Vector | Evidence / next gate |
|---|---|---|
| CI | (1,.97,.96,.93) | bc0554e passed; 8961056 pending |
| Continuous encoder | (1,.94,.95,.90) | existing matrix and paired benchmarks passed |
| Temporal integrity | (1,.94,.95,.90) | split, delayed AAC, frame markers; no gate relaxed |
| Size budget | (1,.89,.85,.65) | ABR uncertainty remains |
| Adaptive planner | (1,.65,.65,.55) | metadata heuristic; no measured quality |
| Batch queue | (.99,.86,.84,.80) | active/queued cancellation passed |
| Batch UX | (.99,.86,.55,.60) | build and core tests; interactive acceptance open |
| Output naming | (.98,.92,.86,.82) | shared destination reservation passed |
| Veryfast | (.97,.95,.90,.85) | matrix passed |
| Experimental split | (.94,.77,.78,.65) | repeated matched benchmark slower, remains opt-in |
| Performance instrumentation | (.98,.90,.85,.82) | phase accounting and actual CI measurements passed |
| Audio validation fusion | (.93,.60,.50,.50) | bounded streaming shadow comparator and tests implemented; CI pending, no production substitution |
| Source timebase preservation | (.96,.15,.40,.40) | local MKV counterexample and demux timebase trial, unsupported metadata remains |
| Hardware acceleration | (.95,.05,.05,.15) | device-specific tests needed |
| Toolchain | (1,.92,.85,.82) | owner dependency presence |
| Distribution | (.98,.78,.80,.65) | 167459-byte preview |

Priority: verify 8961056 shadow CI; compare matched full timeline + aggregate hash vs fused per-packet validation, preserve production checks until evidence. Audio hash phase ~3.6% of measured continuous total, so potential end-to-end speedup is modest. Investigate AAC extradata/priming and metadata equivalence before replacing gates. Then return to capability-aware plans and finite 0.2.0 acceptance, CPU budget and desktop usability. Keep split opt-in. No new version or release.
