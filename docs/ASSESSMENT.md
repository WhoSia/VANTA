# Development assessment — 0.2.0

Axes: value / implementation / evidence / integrity, [0,1]. Subjective rubric, not measured reliability. No linear aggregate.

| Area | Vector | Evidence / next gate |
|---|---|---|
| CI | (1,.97,.96,.93) | 14eebd0 passed; new acceleration CI pending |
| Continuous encoder | (1,.94,.94,.90) | 12 prior combinations; veryfast extension pending |
| Temporal integrity | (1,.93,.93,.89) | VFR, multi-audio, 120s, relative timing and payload checks |
| Size budget | (1,.89,.85,.65) | ABR uncertainty remains |
| Adaptive planner | (1,.65,.65,.55) | metadata heuristic; no measured quality |
| Batch queue | (.99,.75,.70,.70) | failure isolation passed; GUI and active cancellation open |
| Output naming | (.98,.88,.80,.80) | collision tests passed |
| GUI | (.99,.85,.60,.60) | owner launch; actual encode acceptance open |
| Veryfast preset | (.97,.90,.20,.55) | extended matrix pending |
| Experimental split | (.94,.55,.20,.35) | one Linux benchmark and local trim trial; C# CI pending |
| Hardware acceleration | (.95,.05,.05,.15) | no device-specific validation |
| Toolchain | (1,.92,.85,.82) | previous launcher CI and owner dependency availability |
| Distribution | (.98,.75,.70,.65) | 157582-byte batch preview |

Priority: preserve current CI; verify split and veryfast, add boundary/cancel and comparable benchmark evidence, evaluate internal thread allocation before GPU changes. Experimental option never defaults on. Do not trade integrity for speed. If split cannot demonstrate benefit, keep disabled or remove from stable scope. No version change.
