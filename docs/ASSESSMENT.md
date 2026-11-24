# Development assessment — 0.2.0

Axes: value / implementation / evidence / integrity, each [0,1]. Subjective rubric, not measured probabilities. No weighted sum.

| Area | Vector | Evidence / next gate |
|---|---|---|
| CI | (1,.97,.96,.93) | 8104a55 passed; launcher CI pending |
| Continuous encoder | (1,.94,.90,.88) | previous real regressions; all preset matrix pending |
| Temporal integrity | (1,.93,.91,.88) | prior VFR, multi-audio, 20ms rejection, 120s |
| Plan rules | (1,.85,.70,.75) | bounded choices; matrix pending |
| Size estimator | (1,.83,.75,.55) | 12 Linux ABR trials reveal -95.06% to +3.19% signed error; no guarantee |
| Adaptive planner | (1,.55,.55,.50) | source heuristic; stream-specific budgets next |
| Candidate vectors | (.95,.45,.35,.45) | measured quality remains unknown |
| GUI | (.99,.82,.60,.60) | owner launch confirmed; compression not confirmed |
| Toolchain | (1,.90,.80,.80) | owner ffmpeg/ffprobe 9.1 full launch; portable and bootstrap CI pending |
| Progress | (.97,.80,.75,.70) | 8104a55 CI passed |
| Distribution | (.98,.70,.55,.55) | launcher implemented; clean install not verified |

Priority: preserve running launcher CI; document counterexamples; implement stream-duration/bitrate-aware budget and Windows 2x3 codec/preset regression next. No calibration from two synthetic sources, no speculative quality scores, no version change.
