# Development assessment — 0.2.0

Axes: value / implementation / evidence / integrity, each [0,1]. Subjective engineering rubric, not measured probabilities. No weighted sum. Prioritize hard gates and dependencies.

| Area | Vector | Evidence / next gate |
|---|---|---|
| CI | (1,.97,.96,.93) | ee4623e passed CI 45472107616 |
| Continuous encoder | (1,.94,.88,.88) | H.264 fast and HEVC regression passed |
| Temporal integrity | (1,.93,.90,.87) | 20 ms rejection, VFR, 120s, copied audio |
| Plan rules | (1,.85,.70,.75) | budget and codec validation smoke passed; further combinations pending |
| Size estimator | (1,.83,.70,.70) | target budget tests passed; actual sizes not guaranteed |
| Adaptive planner | (1,.55,.55,.50) | metadata heuristic tested; quality unknown |
| Candidate vectors | (.95,.45,.20,.40) | pixel retention and savings computed; quality null, pending CI |
| GUI controls | (.99,.75,.25,.40) | editable preview and guarded execution added, pending CI and interaction |
| Dependencies | (.96,.25,.35,.30) | PATH and documented shared install; discovery automation pending |
| Distribution | (.98,.45,.45,.45) | 126203-byte prior artifact; preview install not accepted |

The two known vector axes are insufficient to Pareto-eliminate higher bitrate candidates with unmeasured quality. Keep profiles separate; do not manufacture quality scores. Next: check exact preview CI, validate configuration matrix and preview interaction, correct failures. Version remains 0.2.0; no release tag.
