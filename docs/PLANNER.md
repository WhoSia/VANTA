# 0.2.0 planner contract

## Feasibility before preference
All candidate and edited settings must satisfy the same PlanRules used by the encoder. Only H.264/HEVC CPU MP4, fast/medium/slow presets, even height without upscaling, 64 kbit/s–100 Mbit/s video bitrate, continuous single pass, AAC copy/no audio and source frame timing are currently supported. The output codec and height must match the selected plan. The installed FFmpeg capability check is a later dependency gate; a syntactically valid plan is not proof that a given binary has the encoder.

Target size in decimal MB is a bitrate budget, not a size cap: floor(target bytes * 8 / duration / 1.005 - total audio bitrate). The copied audio budget cannot be traded away. Reject infeasible budgets rather than silently clamp them. Report negative savings if a manual plan is larger than the source. Never claim exact file size from ABR.

## Candidates
Initial source-aware bitrate fractions depend on source codec and estimated bits/pixel/frame; highly compressed sources use gentler fractions. No fabricated visual quality metric. Resolution can be reduced only for compact candidate when suitable; no automatic frame-rate change. Filter duplicates and candidates that do not estimate positive savings. A shortage is valid, not an error to hide.

## Vector preference
Use measured/derived quantities separately from unknown quantities. Candidate axes: estimated saving, retained pixel area, frame-timing fidelity, playback compatibility (explicit policy proxy), estimated encoding cost (uncalibrated), visual quality (unknown until measured). Keep unknown as unknown rather than assigning an arbitrary 0–1 value. Pareto comparisons require the same available axes and preference direction; profile-specific hard constraints (quality: source resolution; compact: smallest estimated bytes) take precedence. Geometric utilities may use only calibrated or explicitly labeled proxies. A larger radar polygon does not imply a better plan.

## UI
Expose recommended cards and one editor bound to a single immutable Plan snapshot. Editing creates a custom plan; selecting a card restores its complete settings. Show source size, estimated output, savings or growth, video bitrate, codec, preset, resolution, audio copy, source FPS. Target MB and video bitrate are alternative entry modes. Invalid inputs must clear stale estimates. Do not expose unimplemented controls or enable encoding yet.

Explicit FPS reduction changes the frame count and cannot use the 0.1 one-to-one video frame guard unchanged. Defer it until a filter-aware expected-frame timeline regression is designed. No segment concatenation or audio drift correction shortcuts.
