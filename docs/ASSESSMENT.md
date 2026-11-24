# Development assessment — 0.2.0

Axes: value / implementation / evidence / integrity, [0,1], subjective engineering scores, not probabilities. No weighted sum.

| Area | Vector | Evidence / next gate |
|---|---|---|
| CI | (1,.97,.96,.93) | 8961056 passed; 9a5f038 pending |
| Continuous encoder | (1,.94,.95,.90) | prior 16-cell matrix and paired benchmark |
| Temporal integrity | (1,.94,.95,.90) | prior split, delayed AAC, frame markers |
| Size budget | (1,.89,.85,.65) | ABR uncertainty remains |
| Adaptive planner | (1,.65,.65,.55) | capability-aware candidate staged, not applied |
| Batch queue | (.99,.88,.84,.80) | optional thread budget pending |
| Batch UX | (.99,.86,.55,.60) | interactive acceptance open |
| Output naming | (.98,.92,.86,.82) | shared destination reservation passed |
| Veryfast | (.97,.95,.90,.85) | matrix passed |
| Experimental split | (.94,.77,.78,.65) | slower matched benchmark, opt-in |
| Performance instrumentation | (.98,.90,.85,.82) | successful phase accounting |
| Audio validation fusion | (.93,.72,.78,.65) | shadow Windows positive/negative results; legacy retained pending configuration/priming evidence |
| Optional encoder thread budget | (.98,.80,.60,.65) | code, build/smoke, actual command-level resource tradeoff; integration pending |
| Source timebase preservation | (.96,.15,.40,.40) | unsupported MKV evidence; not production |
| Hardware acceleration | (.95,.05,.05,.15) | device-specific validation required |
| Toolchain | (1,.92,.85,.82) | owner dependency presence |
| Distribution | (.98,.78,.82,.65) | 169354-byte preview |

Priority: finish 9a5f038 CI. Staged capability-aware planner candidate ed74f8a81ddaa3c385661d52e5349229c861ea5c (parent 6fef0ed8c3ef5246025311cf35a862ac8cd75471) changes Compression.cs, MainWindow.xaml.cs, BatchWindow.xaml.cs and smoke tests. After current CI succeeds, compare these four base blob SHAs against main, rebase only these changes onto current HEAD using a non-force lease; do not replay if superseded. It selects supported codec before heuristic plan construction, conservative AVC-only fractions .95/.85/.75 (design heuristic, not measured quality); explicitly labels codecs, rejects absent supported encoders. No new branch. Keep automatic encoder behavior default; manual budget is not CPU limit. Do not claim acceleration across diverse workloads or equivalent quality. Return to finite 0.2.0 acceptance and user usability; no version/tag/release change without approval.
