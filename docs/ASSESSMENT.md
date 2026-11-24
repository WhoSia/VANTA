# Development assessment — 0.2.0

Axes: value / implementation / evidence / integrity, [0,1], subjective engineering scores, not probabilities. No weighted sum.

| Area | Vector | Evidence / next gate |
|---|---|---|
| CI | (1,.97,.96,.93) | 9a5f038 passed; c38deef pending |
| Continuous encoder | (1,.94,.95,.90) | prior 16-cell matrix and paired benchmark |
| Temporal integrity | (1,.94,.95,.90) | prior split, delayed AAC, frame markers |
| Size budget | (1,.89,.85,.65) | ABR uncertainty remains |
| Adaptive planner | (1,.65,.65,.55) | capability-aware planner on main; CI pending |
| Batch queue | (.99,.89,.88,.82) | thread budget integration passed |
| Batch UX | (.99,.86,.55,.60) | interactive acceptance open |
| Output naming | (.98,.92,.86,.82) | shared destination reservation passed |
| Veryfast | (.97,.95,.90,.85) | matrix passed |
| Experimental split | (.94,.77,.78,.65) | slower matched benchmark, opt-in |
| Performance instrumentation | (.98,.90,.85,.82) | successful phase accounting |
| Audio validation fusion | (.93,.72,.78,.65) | shadow Windows positive/negative results; legacy retained pending configuration/priming evidence |
| Optional encoder thread budget | (.98,.87,.80,.76) | Windows thread allocation and batch integration passed; performance device-dependent |
| Source timebase preservation | (.96,.15,.40,.40) | unsupported MKV evidence; not production |
| Hardware acceleration | (.95,.20,.25,.25) | local listing false-positive reproduced; diagnostic candidate staged, no GPU encoding |
| Toolchain | (1,.92,.85,.82) | owner dependency presence |
| Distribution | (.98,.78,.82,.65) | 172045-byte preview |

Priority: finish c38deef CI; candidate ed74f8a already rebased, do not replay. Staged diagnostic 57718c4 (parent c38deef) remains off main until planner CI succeeds; safe rebase instructions in ACCELERATION.md. Diagnostic is opt-in and does not enable GPU encoding. Review remaining finite 0.2.0 requirements and desktop usability before expanding scope. No version/tag/release change without approval.
