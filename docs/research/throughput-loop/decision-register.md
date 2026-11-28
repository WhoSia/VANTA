# VANTA research decision register — 2026-11-28 KST

Purpose: reduce verified end-to-end wall time, not just encoder time, while making correct choices obvious to first-time users. This is a proposed research operating method, not a new product acceptance policy or version.

## External methods and adaptation

- NASA decision analysis (https://www.nasa.gov/reference/6-0-crosscutting-technical-management/): separate mandatory constraints and preferences, alternatives and assumptions; reduce uncertainty when it could change a decision. Here a device qualification failure is a blocker, not a speed score of zero.
- Microsoft Research trustworthy experimentation (https://www.microsoft.com/en-us/research/articles/patterns-of-trustworthy-experimentation-pre-experiment-stage/ and https://www.microsoft.com/en-us/research/articles/patterns-of-trustworthy-experimentation-post-experiment-stage/): set hypotheses, primary metrics and guardrails before running; inspect data quality and counterexamples before promotion.
- Google HEART (https://research.google/pubs/measuring-the-user-experience-on-a-large-scale-user-centered-metrics-for-web-applications/): map goals to observable signals and metrics. Here correct task completion, wrong execution path, time to start and need for assistance are more meaningful than clicks alone. No telemetry added.
- NIST randomized blocks (https://www.itl.nist.gov/div898/handbook/pri/section3/pri332.htm): pair treatments by input, host, codec, bitrate and repetition; counterbalance order; retain rejected pairs and thermal/resource context.
- Netflix per-title and Dynamic Optimizer (https://netflixtechblog.com/per-title-encode-optimization-7e99442b62a2 and https://netflixtechblog.com/dynamic-optimizer-a-perceptual-video-encoding-optimization-framework-e19f1e3a277f): content-specific rate/distortion operating points. VANTA differs from offline streaming: additional search time is charged to a one-off user encode. No brute-force production search without positive net benefit.

These are public methods, not a claim about private lab priorities.

## Decision policy

1. Fail closed on source integrity, timeline/audio/decode, output collision, cancellation/cleanup and unsupported media. A failed gate cannot be compensated by speed.
2. Separate hypotheses by decision: correctness fix, usable choice, evidence acquisition, or performance optimization. Mandatory regression corrections precede discretionary features.
3. Compare nondominated alternatives in user friction, wall time, output bytes, quality, uncertainty and engineering burden. Do not sum subjective scores.
4. For uncertain high-value decisions, buy the cheapest evidence that can change the next action. On a machine without usable hardware, repeated failed device initializations have low information value. Actual Arc 140T results unlock both device selection and queue overlap tests.
5. Preserve existing production gates and defaults. A candidate that improves time but worsens quality or misses the size gate is not a matched-speed success. Exact build CI and interactive acceptance are distinct. One synthetic run or three repeated pairs is screening, not generalization.
6. After evidence, record promote / retain explicit experiment / reject / blocked and an exact next test.

## Current priority map (ordinal, no weighted sum)

Values 0, .25, .5, .75, 1 only. U = user-friction leverage (none, marginal, moderate, major, directly addresses owner complaint); S = potential speed leverage (none, marginal, stage-local, material system gain, candidate major gain); E = strength of decision evidence (none, hypothesis or failure control, bounded prototype, repeated relevant proxy or exact CI, direct representative owner acceptance); I = information value (none, little, resolves local detail, can alter a decision, unlocks multiple decisions). S and U are engineering hypotheses, not measured probabilities or acceleration percentages. E=.25 for unavailable hardware means the negative control is known, not that hardware is 25% effective.

| Track | U | S | E | I | Constraint / next action |
|---|---:|---:|---:|---:|---|
| Guided tested GPU action | 1 | .75 | .5 | .75 | 29c8d02 implemented; exact desktop CI pending, test with mocked diagnostic evidence, actual device not proven |
| Single-extraction delivery + CPU label regression | 1 | 0 | .75 | .5 | 8d7236f failed WPF because runtime StartButton.Content overwrote resource; dacd9b1 + 859829f repair code + count assertion; portable artifact not yet tested |
| Arc 140T real-device screen | .75 | 1 | .25 | 1 | blocked on physical device; existing in-app synthetic screen, then representative paired source |
| Combined validation | .5 | .75 | .75 | .75 | local 12/12 faster fixed output, 26.6–49.2% group medians; physical GPU full-job measurement required |
| Stage overlap | .5 | .75 | .75 | .75 | Windows 3 exact pairs median 13.7% below 15% gate; keep explicit; real GPU paired screen |
| Content-aware rate/preset frontier | .75 | .75 | .5 | .75 | content-dependent counterexamples; prospectively defined matching, no full-search cost by default |
| AV1 device path | .5 | .75 | .25 | .5 | hardware unavailable; CPU SVT-AV1 fast screen unmatched; avoid product promotion |
| Rust orchestration rewrite | .25 | .25 | .25 | .25 | no measured managed-code bottleneck; defer until profiles show material cost |

Priority tiers: mandatory delivery/UX regression and guided action now; highest information-value device evidence in parallel when available; combined validation and stage overlap conditionally next; content frontier on CPU independently; AV1 after device evidence; rewrite deferred. Ties need a decision-changing experiment rather than arbitrary decimals. If GPU unavailable, advance CPU content/verification screens and UX while blocked, not repeated device-refusal benchmarks.

## Prospective UX task screen

No analytics or real participants measured yet. Compare 1533d91 against guided candidate on the same installed hardware and same video. Tasks: open single video, choose a budget, start CPU; inspect GPU and start a matching tested device; handle no matching device; handle multiple devices; change codec, split or threads after evidence; cancel; locate output; batch choose CPU or explicit GPU. Screen 5–8 consenting first-time users if available, on local synthetic/sample media, counterbalanced order; not a population estimate. Record task completion without assistance, actual backend in EncodeReport vs intended backend, time from loaded source to save dialog, setting mistakes and backtracks, brief explanation of estimate vs guarantee. Proposed screening thresholds: zero wrong-backend launches and zero unsafe publications, at least 4/5 unassisted success (if five users), median setup time lower than baseline; report individual outcomes and uncertainty. These are proposed UX screen rules, not adopted release criteria. Automated control tests only cover reachable states, not usability.

## Current exact implementation and evidence

29c8d02: short-test pass results, deduplicated known hardware names; only exactly one matching eligible backend exposes pinned GPU save. CPU remains explicit. No launch at diagnostic time, no speed claim, no silent fallback. Guided path uses device defaults, not the advanced fast checkbox; actual hardware trial requalifies. Clears evidence on new source or rerun, cancels do not produce passed evidence. Matching/threads/split/pending-target guard checked each time; multiple matching candidates not ranked. CPU preflight timing explicitly labeled. Desktop smoke injects synthetic diagnostic result states, tests unique/multiple/cleared and split gate, min-window layout. This is simulated qualification evidence, not GPU success.
1533d91 exact Windows 46030665849 completed success (WPF small-window + real media + integration + package); artifact 15213833997 308321 bytes SHA256 98d43abbdca4adc75e40ea714fd10c76d294d2bca49d9beb60498d8575ade2c7, still nested.
8d7236f exact Windows 46031595588 failed WPF control test "Batch has distinct primary action", before packaging. Runtime StartButton.Content assigned stale wording even after resource changed. dacd9b1 fixes idle and ready-count dynamic text; 859829f updates ready-count real-media assertion. New exact CI required; no single-extraction download verified yet. No change to encoder defaults, version, release, or publication gates.


## 2026-11-28 verified single-extraction checkpoint

Exact `859829f32f4d00b2f9d58242313e1310b53972f2` Windows CI [46032382823](https://github.com/WhoSia/VANTA/actions/runs/46032382823) completed successfully. Build, Core smoke, WPF control and real-media tests, FFmpeg integration, ten-bit/HDR refusal, local package installation, portable preparation and artifact upload all passed. [Portable download](https://github.com/WhoSia/VANTA/actions/runs/46032382823/artifacts/15214708794), 306599 bytes; Actions-reported SHA-256 `946b138b9b961538d94ef96163ab35ee5bdc0a2f708f5eeaa1417e3064832402`; expiry 2026-12-04T16:23:45Z. Extract once into a new folder. This supersedes previous download guidance; independent download-byte inspection and owner-PC acceptance were not performed. Physical GPU throughput remains unverified. No version/default/release change.


## Next UX experiment: co-locate speed choices and device checks

Move the existing GPU-check control into the speed section, with a persistent localized next-action guide for no source, no passed test, one/multiple matching devices, incompatible codec, split/manual threads and busy state. Retain explicit CPU action, hardware trial requalification, no speed ranking or automatic start/fallback. Test synthetic evidence states and keep minimum-window and real-media checks. No new dependency or default. This is a source candidate; actual WPF execution and user task success still require evidence.


## Stage-overlap reanalysis — 2026-11-28 (existing measurements, not new encodes)

Recomputed from stage-pipeline-a3ba38f.json (exact C# Windows 46028905424, 4 logical processors, two 180-frame synthetic AVC fixtures, same H.264 veryfast 810p/1791kbps and four requested threads, three paired repetitions). Every per-job output SHA matches within pair.

| Rep | Serial s | Overlap s | Wall-time reduction |
|---:|---:|---:|---:|
| 0 | 5.3089699 | 3.7174876 | 29.9772% |
| 1 | 4.2120480 | 4.0045758 | 4.9257% |
| 2 | 4.3167716 | 3.7242559 | 13.7259% |

Median reduction 13.7259%, below prospective 15% median screen. Three repetitions of the same two fixtures do not establish population-level evidence. Median per-job serial encoding share 55.1173%; validation share 39.9818%. Median stage durations serial -> overlapped: encoding 1.2723562 -> 1.39605045 s; validation .90553085 -> 1.08090805 s. This suggests resource contention during overlap, not a proven causal attribution. Batch makespan still improves in all three pairs; stage durations are not additive under overlap.

Conditional planning calculation for a serial job: if fraction f of total time belongs to one stage and that stage alone is made k times faster with all other costs fixed, reduction = f*(1-1/k). Median shares imply 19.9909% total reduction if validation halves, 27.5586% if encoding halves. These are hypothetical sensitivity results for the measured short fixtures, not predicted speedups, not additive to overlap results.

Decision: retain overlap as explicit experiment; no default promotion. Prioritize exact combined-validation paired full-job comparisons and Arc 140T qualification/throughput on representative media. Next discriminating factorial: serial vs overlap crossed with baseline vs combined validation; hold encoder settings, source and per-job output SHA constant, reverse order by block, track wall time, per-stage durations, CPU/RSS/I/O and verification wait. Repeat on long natural media, then separately on available real GPU; do not pool synthetic, derivative or device groups. Predeclare gate and retain all failures; compare relative gain and absolute time. If overlap loses due to CPU contention, test a lower-cost resource policy only with measured bottleneck evidence. No new execution in this reanalysis.
