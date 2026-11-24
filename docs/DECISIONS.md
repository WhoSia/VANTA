# 0.1.0 decisions

- C# / .NET 10 / WPF; core independent of Windows GUI.
- Continuous encoding only; no chunk assembly.
- Initial planner is a bitrate-budget heuristic, not measured quality. Candidate sizes are estimates, not guarantees. Pareto and geometric utility are primitives, not yet calibrated.
- FFmpeg binaries external; automatic acquisition requires pinned hashes and provenance.
- No Shutter Encoder source copied yet; audit required before reuse.
- Build, synchronization, HDR and Windows acceptance gates are open.
