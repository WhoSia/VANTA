# Development assessment — 0.1.0

Axes: value / implementation / evidence / integrity. Subjective estimates except explicit CI evidence, not measured probabilities. No weighted sum.

| Area | Vector | Evidence / next gate |
|---|---|---|
| CI | (1,.95,.85,.85) | 9a123e7 Windows CI passed; latest changes must rerun |
| Process runner | (1,.85,.55,.65) | pre-cancel and real execution passed; active cancel pending |
| Probe | (.99,.78,.55,.65) | delayed AAC fixture passed; extra streams rejected by design |
| Encoder | (1,.78,.45,.60) | baseline successful; new packet and audio hash gates pending |
| Temporal integrity | (1,.75,.40,.55) | endpoint fixture passed; VFR, 120s, multi-audio pending |
| Planner | (.99,.40,.35,.40) | smoke tests passed; bitrate heuristic only |
| GUI | (.97,.30,.25,.25) | WPF compiled; no interactive acceptance |
| Dependencies | (.96,.15,.35,.25) | runner FFmpeg only; end-user discovery pending |

Priority: prove new core gates before expanding GUI or changing version. No new version without approval. 120 seconds is an extended regression fixture, not evidence of hour-scale or perceptual sync.
