# Development assessment — 0.2.0

Axes: value / implementation / evidence / integrity, [0,1], subjective engineering scores, not measured probabilities. No weighted sum.

| Area | Vector | Evidence / next gate |
|---|---|---|
| CI | (1,.97,.96,.93) | 123ce84 successful, 19f90b5 pending |
| Continuous encoder | (1,.94,.95,.90) | 16-cell matrix and paired benchmark |
| Temporal integrity | (1,.94,.95,.90) | odd frame markers, delayed AAC, split cancel, negative control |
| Size budget | (1,.89,.85,.65) | ABR uncertainty remains |
| Adaptive planner | (1,.65,.65,.55) | metadata heuristic; measured quality unknown |
| Batch queue | (.99,.82,.70,.70) | new cancellation and destination tests pending |
| Batch UX | (.99,.82,.35,.50) | 19f90b5 implemented, interactive acceptance open |
| Output naming | (.98,.90,.80,.80) | shared folder reservation pending |
| Veryfast | (.97,.95,.90,.85) | matrix passed |
| Experimental split | (.94,.77,.75,.65) | timing/marker regressions pass; no demonstrated speed advantage in matched Windows or single 1080p trial |
| Performance instrumentation | (.98,.40,.60,.65) | phase trial shows validation cost; no runtime telemetry yet |
| Hardware acceleration | (.95,.05,.05,.15) | device-specific validation required |
| Toolchain | (1,.92,.85,.82) | launcher and dependency presence |
| Distribution | (.98,.75,.75,.65) | 162690-byte preview |

Priority: finish 19f90b5 CI; phase timing for equivalent validation optimization; capability-driven plans and remaining UX. Keep split opt-in and avoid auto-selection until representative speed/quality benefit. No version change or release.
