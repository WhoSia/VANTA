# Development assessment — 0.2.0

Axes: user value / implementation coverage / evidence coverage / integrity confidence, [0,1], subjective engineering estimates, not measured reliability. No weighted sum. Hard gates take precedence.

| Area | Vector | Evidence / next gate |
|---|---|---|
| CI | (1,.97,.96,.93) | 9458789 passed; b27dbca pending |
| Continuous encoder | (1,.94,.95,.90) | previous matrix and regressions |
| A/V integrity | (1,.94,.95,.90) | delayed AAC, VFR, markers, split cancellation |
| Size budget | (1,.89,.85,.65) | ABR uncertainty; honest labels |
| Adaptive planner | (1,.89,.85,.72) | explicit preference CI passed; heuristic quality unmeasured per input |
| Batch queue | (.99,.89,.88,.82) | previous thread-budget and cancellation tests |
| Batch UX | (.99,.87,.55,.60) | individual override candidate staged; interactive acceptance open |
| Source consistency | (1,.65,.15,.45) | guard on main; Windows share and stale tests pending |
| Output naming | (.98,.92,.86,.82) | collision tests passed; dialog corrections pending CI |
| Progress | (.99,.88,.80,.75) | rounding correction on main, CI pending |
| Experimental split | (.94,.77,.78,.65) | slower tested, opt-in |
| Performance instrumentation | (.98,.90,.85,.82) | phase accounting |
| Audio validation fusion | (.93,.72,.78,.65) | shadow only; legacy retained |
| Optional encoder budget | (.98,.87,.80,.76) | Windows tests passed |
| Hardware diagnostics | (.97,.90,.80,.75) | CPU positives, six hardware failures classified; actual GPU success untested |
| GPU compression | (.95,.05,.05,.15) | deferred until working device evidence and approved scope |
| Toolchain | (1,.92,.85,.82) | owner dependency presence |
| Distribution | (.98,.78,.82,.65) | 177748-byte preview |

Priority: exact b27dbca CI; 7ad9630 already rebased. Staged per-file settings 651a9fc after source consistency CI success, safe rebase instructions in BATCH_UX.md. Verify mixed codec/height and override precedence, then finite 0.2.0 gate review. GPU compression and FPS conversion deferred until independent verified scope. No new version/tag/release before approval.

2026-11-25 update (supersedes earlier pending notes): source lease b27dbca, individual settings f680b4d, batch results 49ec6f2, preferences 10fcfa3 all passed exact CI. Subjective vectors: source consistency (1,.90,.92,.85), individual batch (.98,.90,.86,.80), batch results (.98,.89,.85,.80), preferences (.97,.90,.85,.80). WPF control smoke and single result access 0d8818a on main pending CI; GUI verification (.99,.65,.15,.40). Prioritize metadata contract, then desktop acceptance and finite scope. No version advancement.

Update: 0d8818a exact CI passed. GUI control smoke (.99,.75,.65,.65); interactive acceptance still open. Visual metadata (.99,.70,.35,.50): 067196a on main, CI pending, local synthetic anamorphic scaling evidence only. Rotation fixture creation unsuccessful; transform support not established. Prioritize actual rotation fixture and color tag regression, then finite desktop acceptance.

## Final 0.2.0 assessment (supersedes earlier pending entries)

Exact 5a7f687 CI passed; all nine mandatory core/preview gates accepted. Subjective vectors (value, implementation, evidence, integrity): planner (1,.89,.88,.75); continuous encoder (1,.95,.95,.90); A/V (1,.95,.95,.90); source guard (1,.90,.92,.85); visual metadata (.99,.82,.86,.82); per-file batch (.98,.93,.90,.85); preferences (.97,.90,.85,.80); GUI controls (.99,.80,.70,.70); distribution (.98,.80,.85,.70); full interactive acceptance (1,.35,.15,.35). No weighted aggregate; these are judgment scores, not reliability measurements. Accepted core/preview does not imply 1.0 or clean-PC installation acceptance. Freeze at 0.2.0. Proposed next version 0.3.0 — Desktop Reliability & Distribution Readiness, awaiting owner approval.

## 0.3.0 opening assessment

Owner approval recorded at 8e7139d. Vectors (value, implementation, evidence, integrity), subjective and limited to 0.3.0 goals: version coherence (1,.90,.25,.75); local package (.99,.75,.10,.45); safe installer (1,.75,.10,.45); dependency diagnostics (1,.65,.55,.65); desktop workflows (1,.60,.40,.55); supported-media regressions (1,.95,.90,.90 baseline); manual owner acceptance (1,.35,.15,.35); distribution handoff (.98,.65,.35,.55). First hard gate: exact version CI, then staged package candidate f6100439 and Windows installation regressions. Next independent axes: dependency diagnostics, desktop workflow tests, UX defects. No GPU/rotation/FPS feature expansion without separate scope. Do not claim clean-machine install or trusted publisher signature from hash-only package verification.

## 2026-11-25 adaptive scheduling screening

Independent local FFmpeg 7.1.5 Debian, 5 visible logical CPUs, cgroup quota 4 CPUs. Same silent 960x540 30fps 3s testsrc2 H264 fast CRF18 source, 3 jobs each encoding H264 medium 800 kbps passthrough. Two alternating repetitions (fixed2,wave4,auto; auto,wave4,fixed2). Fixed2 rolling 2 concurrent jobs with 2 requested encoder threads/job: 1.3272,1.3281s; wave4 waits for first two 2-thread jobs then runs final job with 4 threads: 1.2609,1.2189s; FFmpeg default auto rolling up to 2 jobs: 1.1555,1.2179s. Wall timing includes encodes and scheduling, excludes fixture creation and ffprobe frame-count checks. All 18 outputs decode/count 90 frames (ffprobe -count_frames); output bytes fixed2 291028..291599, wave4 290948..292423, auto 292441. No SSIM, audio, peak resource, Windows or complete VANTA validation in this screen. Means 1.32765,1.23990,1.18670s. Wave policy improves fixed2 by about 6.6% but is about 4.5% slower than default auto; insufficient evidence to change default. Future tests vary source complexity/length, compare quality and resource peaks, full integrity gate. Do not modify running FFmpeg threads. Subjective scheduling vector (.97,.20,.25,.35). Native GUI (.99,.90,.80,.85), bilingual (.99,.92,.82,.80), distribution (1,.96,.92,.88); real media UI workflow (1,.75,.20,.55) 325af59 CI pending. No claim of measured reliability.

## Current prioritization (post-f04964c)

Subjective (value, implementation, evidence, integrity): continuous encoder (1,.95,.95,.90); batch terminal state (1,.96,.93,.90); bilingual/quick settings (.99,.94,.90,.86); native layout (.99,.93,.85,.85); dependency diagnosis (1,.80,.75,.75); distribution handoff (1,.96,.93,.88); owner-PC acceptance (1,.35,.15,.35); fused validation (.98,.65,.70,.65); managed parser candidate (.85,.60,.15,.55); adaptive threads (.97,.25,.30,.45); AV1 (.91,.15,.20,.35); Rust port (.80,.10,.10,.30). Hard gates precede optional performance work. No aggregate completion probability. Actual managed screen and stricter validation pending exact CI; verify then address launcher/app consistency and remaining owner evidence. No speculative new modules.

## Automated gate handoff — b77ea83

Subjective (value, implementation, evidence, integrity): encoder (1,.97,.96,.95), batch (1,.97,.95,.94), bilingual/quick UI (.99,.94,.92,.91), logical layout (.99,.94,.90,.90), distribution (1,.98,.97,.95), dependency diagnosis (1,.97,.95,.92), Span parser (.85,.98,.95,.94), owner-PC acceptance (1,.35,.15,.35), fused validation (.98,.65,.80,.65), adaptive threads (.97,.25,.30,.45), AV1 (.91,.15,.20,.35), Rust (.80,.10,.10,.30). Automated gate passed at exact b77ea83; sole remaining required blocker is owner-PC interaction. Stop scheduled development; request five-case checklist. Optional performance research remains unimplemented/experimental, not a release blocker. Proposed next version 0.4.0 — Measured Acceleration & Adaptive Resource Scheduling requires separate approval after 0.3.0 acceptance. No tags/releases. Scores are subjective, not probabilities.

## Multi-axis review — 2026-11-25

Subjective score (implementation, evidence, priority): correctness (.97,.95,1); failure/cancellation (.96,.94,1); speed (.65,.70,.98); visual hierarchy/consistency (.85,.50,.95); interaction (.92,.78,.97); accessibility (.72,.45,.98); Korean/English (.95,.91,.96); maintainability (.90,.80,.92); packaging (.97,.95,.91). No aggregate score used; hard integrity gates override cosmetic or performance gains. 3c8dfb4 verified; 2df2eba/740cea9 pending. Design is a source-level assessment, not a pixel-level judgment; no rendered screenshots or assistive-tech evidence. Fixed language header reduces navigation, explicit headers and field names reduce ambiguity; next test native contrast, visible keyboard focus, tab order, minimum viewport actual DPI and actual owner acceptance. Existing fixed footer and card layout retained based on owner resize observation. MCP-Hub grouped discovery/health call failed internally; do not claim Impeccable execution. Parallel hash research is bounded shadow only. Avoid speculative custom chrome, framework migration and native dependencies. No new version.

## Review after 740cea9 CI

Subjective implementation/evidence/priority: accuracy (.97,.95,1), failure/cancel (.96,.94,1), performance (.67,.76,.98), visual hierarchy (.87,.55,.93), interaction (.94,.83,.98), accessibility (.76,.60,.96), localization (.95,.92,.94), maintainability (.91,.82,.92), distribution (.97,.95,.90). Target pending/invalid guard 457eb66 (implementation .90, evidence .25, priority 1) pending exact CI. Audio parallel candidate: stage improvement repeated in Windows C# and local proxy whole workflow, production not enabled; priority .97, evidence .70, active cancellation and full production batch comparison needed. Native visual aesthetics still source-level only. Next: verify exact target guard CI, then full C# workflow comparison, active cancellation/cleanup and resource contention; actual owner PC required for final acceptance. No new dependencies or version.

## 0.4.0 research proposal priorities

Subjective (value, feasibility, current evidence, priority): quality-matched multi-split (1,.75,.35,1), independent segment/worker planning (1,.90,.45,.99), bounded active cancellation (.99,.95,.65,1), keyframe-aware cost partition (.97,.80,.20,.97), scene-complexity budget (.98,.70,.15,.96), actual C# audio parallel end-to-end (.95,.90,.70,.95), shared decode validation (.98,.65,.35,.94), adaptive batch/segment global budget (.99,.75,.25,.97), AV1 (.90,.70,.20,.82), Rust (.80,.55,.10,.70). Current local multi-split screen did not outperform continuous or preserve equivalent quality, so research priority is high, production readiness low. Current 0.3.0 is not formally owner-accepted. See PROPOSAL_0.4.0.md for finite gates and no version advancement.

## 0.4.0 first corrective loop

Hard gate priority 1.00: exact CI 45571468942 after CS4007 test-only correction. Local CRF/partition screen: comparable bytes and global SSIM on one synthetic source, no speed win over continuous, exact-key seek small uncertain reduction vs aligned early. Subjective (value, implementation, evidence, integrity): keyframe partition (0.98,.20,.35,.35), matched-size/quality measurement (1,.35,.40,.55), independent N/W (.99,.75,.30,.55), global resource planning (.99,.20,.20,.45), parallel audio (.95,.65,.76,.70). Source-level implementation and local proxy do not prove Windows correctness. Continue after exact CI; no new production default or release.
## First 0.4.0 verified build / keyframe research

Subjective (value, implementation, evidence, integrity): independent N/W (1,.85,.80,.84), bounded keyframe partition (.98,.70,.35,.55), matched quality/size (.99,.55,.55,.65), adaptive resource scheduling (.99,.20,.25,.45), parallel audio (.95,.65,.76,.70), baseline desktop and packaging (.97,.95,.92,.92), owner-PC acceptance (1,.35,.15,.35). Actual 78ab876 Windows matrix verified frame order/cancel, no speed win. 1080p local proxy comparable sizes/SSIM but longer time and increased sampled RSS with splitting; keyframe alignment is not automatically better. Priority shifts toward verified full-workflow overhead reduction, measured resource admission, candidate rejection and heterogeneous workload screening. No release or default acceleration change. New code 19a0250 pending exact CI.

## 0.4.0 comparative upstream and dispatch review

Subjective vectors (value, implementation, evidence, integrity): keyframe partition (.98,.80,.80,.85), independent N/W (1,.85,.85,.87), matched quality/size (.99,.60,.70,.70), dispatch ordering (.91,.65,.35,.50), resource admission (.99,.20,.25,.45), parallel audio (.95,.65,.76,.70), owner-PC acceptance (1,.35,.15,.35). 45573017677 passed; no speed win. 4f985a8 pending exact Windows CI; local dispatch proxy no robust gain. Prioritize full workflow overhead and resource-aware admission over more chunks, compare empirical cost proxies to frame count. No weighted sum, new default or version change.

## Indexed access screening update

Subjective (user value, implementation, evidence, integrity): indexed seek (.98,.20,.35,.35), random-access admissibility (1,.15,.30,.30), shared source grid (.97,.25,.30,.40), dispatch ordering (.91,.65,.35,.50). Local closed-GOP indexed seek promising (~16.86% lower than existing early seek proxy), but open-GOP both reject, no continuous comparator and only two repeats. Hard integrity gate outranks apparent speedup. Research harness independent of current Windows CI; do not enable indexed seek or new cache. Verify exact 4f985a8 run first; no new version.

## AVC random access checkpoint

Exact 4f985a8 Windows CI success, longest-first no speed gain in 1080p screen; no promotion. Subjective (value, implementation, evidence, integrity): longest-first (.91,.80,.80,.85), IDR candidate classification (.98,.55,.45,.55), safe indexed seek (.98,.25,.45,.50), full-workflow speed (1,.35,.55,.55), global resource admission (.99,.20,.25,.45). 46d7a7f C# classifier and reference tests pending Windows. Open GOP 2/3 direct keyframe failures, preceding IDR 3/3 matches in local proxy. Prioritize closed-GOP independent reference, indexing cost and continuous fallback; no default change.

## Public corpus expansion

Subjective (value, implementation, evidence, integrity): public media coverage (1,.65,.15,.45), native compatibility (.99,.45,.20,.45), per-frame quality screening (.98,.60,.25,.55), AVC IDR classifier (.98,.75,.80,.80), adaptive resource admission (.99,.20,.25,.45), representative speed gate (1,.35,.35,.50). 46d7a7f verified, no production routing. 44a0a5d introduces separate corpus CI, pending both workflows. Highest priority: seal actual acquired bytes and verify normalization CFR; local negative evidence shows explicit -r and -fps_mode cfr are needed in FFmpeg 7.1.5. No default speed claim. Future content additions should include high-motion live footage, low-light/noise, long native audio, VFR phone and gaming; avoid counting resolution variants as independent content.

## Objective review after real media run

Do not assign measured success to implementation effort. Observed: normal CI pass, public corpus overall fail, 5/6 normalized inputs, 20 full verified outputs, 6/10 comparable pairs, 0/10 speed wins, 0 native original encodes. Highest-priority blockers: BBB fixture audio clock, source provenance seal, strong native encoder baseline, resource and broader native compatibility coverage. Segment policy remains experimental; no default benefit, and matched pair evidence argues against promoting it on these short clips. No arbitrary score increase from commit volume.

## Decision and observed review

See docs/decision. 12 axes/4 profiles/12000 seeded subjective scenarios; mean/5–95 percentile and first/top-three share, pairwise, regret and Pareto. Model values are not calibrated probability or evidence of feature success; mandatory corpus correction first, codec/preset frontier next. Empirical second run has 30 pair rows, four content groups, no matched >=15% win; native auto content-equal mean +.0076, fast +.0966, aligned -.2938. Do not overstate small corpus. User explicitly requested immediate multilingual implementation; isolated C/Rust comparative lane now implemented, C local reference passed, actual Rust/C# compilation and timings pending. No promotion from novelty.

## Assessment update — verified native, product workflow and remaining gates

Empirical lane: 2911ea2 native hot-cache AVC median Rust ~8.6–13.7x existing C#; C similar, no full workflow evidence and high C# variance. Prioritize measuring replaceable stage share and optimized managed comparator before any native packaging. Corpus mandatory repair still open; six source pins reproduced, BBB offset diagnosis refined, current audio/video preparation hypothesis pending actual CI. Fused validation is a bounded candidate with earlier shadow measurements and explicit full workflow oracle; no default promotion. User requested UI/UX and features alongside performance. Product result playback/persistent metrics implemented as bounded change, pending WPF verification. Use existing 12-axis scenario model as historical snapshot, not updated empirical score: no claim new ranking from changed evidence. Next rescore UX, resource admission, codec compatibility and diagnostics as distinct candidates; preserve native failure/packaging gates.

## Evidence update after 26d9101

Both decision snapshots reproduced. Current hypothesis priority: single output decode (stage-local effect observed, full C# proof pending), codec-specific rate-distortion and bounded probe selection, actual resource measurement, input compatibility, UX diagnostics. Overlap full C# mean +3.89% but variable, fused +4.21% in latest run, split -25.75% all comparisons. Do not infer utility or success probability from these percentages. No new 12k snapshot in this change; retain previous model and perform separate noncompensatory correctness gates. All novel candidates remain internal until differential/cancel/resource and whole-workflow evidence passes.

## Evidence checkpoint after 8f4ba94

Decision v3 reproduced on CI. Live adaptive search and long HEVC validation provide stronger evidence for those leading research candidates, not a new 12000-scenario result. Batch resource screen is a separate provisional experiment. Resource feasibility remains unproven: requested thread budget is not actual resource usage; partial proxy showed higher summed RSS with concurrency and unstable timing, no winner. Retain required/owner/blocked gates, no production promotion. Candidate judgments (user impact, speed potential, integrity, testability) for adaptive batch admission (.79,.65,.89,.93), stage backpressure (.86,.77,.90,.73), source timeline elimination (.84,.85,.57,.65); subjective hypothesis prioritization, no measured utility or success probabilities. Source timeline elimination must not substitute a count derived only from frames that survived encoding for an independent input-frame oracle.

## Multi-audio hypothesis

Provisional four-axis (user value, speed potential, integrity confidence, testability) scores for multi-audio scan elimination (.72,.81,.83,.92), combined single decode + per-track join (.85,.83,.78,.90). Subjective and conditional on multi-track coverage; not measured probabilities or a replacement for v3. Prior public single-decode positive repeated result raises confidence but not global acceptance. Remaining hard gates and actual owner acceptance unchanged. No new 12000 scenario run here.

## 2026-11-27 evidence-based update

Batch admission passes correctness, not matched acceleration (0/4). Next decision must use same per-job thread requests; validation gate is untested and may worsen throughput. Combined single-decode/audio join proxy improves validation cost, full C# comparison pending. Do not infer whole-job savings from component ratios or add two ratios. Source-clock packet-only hypothesis falsified for unfiltered packets on valid trimmed MP4; filtered approach still unproven. Four-axis subjective screen (user value, speed potential, integrity confidence, testability): combined validator (.88,.87,.83,.94), equal-settings schedule experiment (.83,.69,.94,.95), validation gate (.78,.61,.90,.91), raw source packet-only substitution (.70,.75,.30,.83), independently observed source snapshot reuse (.70,.68,.78,.80). Hard integrity veto prevents raw packet-only routing despite speed potential. Existing v3 scenario remains last reproduced; not a new 12000-run snapshot.
