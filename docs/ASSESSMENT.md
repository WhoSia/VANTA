# Development assessment — 0.2.0

Axes: value / implementation / evidence / integrity, each [0,1]; subjective rubric, not measured reliability. No linear sum.

| Area | Vector | Evidence / next gate |
|---|---|---|
| CI | (1,.97,.96,.93) | 81e4217 passed; cd99fbf failed integration compile; corrective 123ce84 pending |
| Continuous encoder | (1,.94,.95,.90) | 16 matrix cells passed |
| Temporal integrity | (1,.93,.94,.89) | existing timeline and payload tests, 120s split passed |
| Size budget | (1,.89,.85,.65) | ABR uncertainty remains |
| Adaptive planner | (1,.65,.65,.55) | metadata heuristic; no measured quality |
| Batch queue | (.99,.75,.70,.70) | failure isolation passed; active batch cancel/UX open |
| Output naming | (.98,.88,.80,.80) | collision tests passed |
| GUI | (.99,.85,.60,.60) | owner launch; encoding acceptance open |
| Veryfast | (.97,.95,.90,.85) | 16-cell matrix passed |
| Experimental split | (.94,.72,.58,.55) | Windows 120s and VFR rejection passed; frame ID/cancel/paired benchmark still pending after CS4007 fix |
| Hardware acceleration | (.95,.05,.05,.15) | device-specific tests needed |
| Toolchain | (1,.92,.85,.82) | launcher and dependency presence |
| Distribution | (.98,.75,.75,.65) | 162477-byte preview |

Priority: verify corrective CI 45481255837; preserve exact failure evidence. Local continuous thread/preset trials and serial/parallel throughput trial recorded, no universal speedup. UX candidate 6b30fae staged, not main; see BATCH_UX.md for safe rebase after CI. Reassess split with comparable end-to-end data; keep it opt-in. No version change.
