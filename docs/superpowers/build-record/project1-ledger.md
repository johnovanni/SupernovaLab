# SDD ledger — plan: docs/superpowers/plans/2026-11-11-supernovalab-project1.md
Spec: docs/superpowers/specs/2026-11-11-supernovalab-design.md (research files normative). Branch project1, base 3808bbb.
Owner's standing instruction: make reversible calls, report later. Local commits OK; never push.

## Pre-flight scan
| Pair / task | Produces → consumes | Finding |
|---|---|---|
| T1 → all | Quantity/Source/Confidence, ObserverSetup, UnitFormat | consistent |
| T2 → T3/T4 | ExplodabilityTable rows 9–120 M☉ (Lookup clamps) | clamping could silently answer for 150 M☉ → Ruling R1 |
| T2 self | test "explodes rows have NS mass 1.2–2.0" vs fallback-BH rows that explode | Ruling R2 |
| T3 → T4 | EarlyFate+PresupernovaStar → Fate/Outcome | consistent; ECSN 9.0–9.25 handled before table (T3 test 9.1 = T4 test 9.1) |
| T4 → T5 | OutcomeEngine extended in place | consistent |
| T5 self | 40 M☉ Full-stripped extreme rotation → GRB needs BH or M_CO ≳ 7 (Sch21 says NS at 34–70) | consistent via M_CO clause |
| T6 → T7–T13 | ISignalModel, Detectors (all Ids fixed in T6), EventSampler, TimeAxis | consistent; models need parameterless ctors (in Global Constraints) |
| T6..T12 tests | need hand-built Outcomes (2008D-like, 1987A-like, 2014J-like) | Ruling R3 (TestOutcomes helper) |
| T8 → T9 | cooling phase reused by LightCurveModel | consistent |
| T5/T9 | magnetar SLSN case reused in T9 test | consistent |
| T12 → T15 | SpectrumModel.At (not ISignalModel) | consistent |
| T13 → T14/T15 | PresetCatalog, ObservedFact | consistent |
| T14 → T15/T16 | MainWindow placeholders | T15 Files omit MainWindow → Ruling R4; T16 "Modify SceneController" doesn't exist → Ruling R5 |
| T14 self | screenshot helper "copied" but Universe3D's helpers live in its git-ignored workspace | Ruling R6 |
| T16 → | EjectaKinematics uses LightCurveModel (T9) | consistent |
| T17 self | docs only | consistent |
Rulings R1–R6 recorded in global-constraints.md.

## Tasks
Task 1: dispatched (sonnet, base 3808bbb, agent a3e9d255d336c5220)
Task 1: review → Needs fixes (2 Important: UnitFormat.Sig G3 → "1E+03" at 1,000 yr / 1 Gpc; unit-boundary rollover). Trailers checked by controller: present.
Task 1: minor (deferred): 1 Gpc z test loose (0.2–0.3 → ~0.248±0.01); no RedshiftAtComovingDistance round-trip / z>8 branch test; UnitFormat Mass/µs/min/Mpc untested; Extinction.cs N_H comment section cite; U/Uv should be Speculative when surfaced.
Task 1: fix round 1/5 dispatched (082f5fa); re-review running
Task 1: fix round 1/5 (3 addressed, 0 open; commits ad0b7ec..082f5fa)
Task 1: minor (deferred): UnitFormat boundary rollover gives "1 days"/"1 years" (plural unit with value 1)
Task 1: complete (commits 3808bbb..082f5fa, review clean)
Task 2: dispatched (opus, base 082f5fa)
Task 2: implementer DONE_WITH_CONCERNS (74c30af): real S16 per-model data obtained (no fallback). Rulings R7 (keep published 100 M☉ NS mass), R8 (15 → 16 M☉ for unstripped exploding cases; 15.0 fails in N20).
Task 2: minor (deferred): no FallbackBlackHole row test (27.4 M☉, M_b 4.96234) or Explodes==false⇒DirectBlackHole consistency test; LoadEmbedded re-parses each call (cache if hot); EmbeddedResource glob Data\Embedded\**\* broad; fallback rows Ni ~1e-13 must not be read as yields
Task 2: complete (commits 082f5fa..74c30af, review clean)
Task 3: dispatched (opus, base 74c30af)
Task 3: implementer DONE_WITH_CONCERNS (24d9bc1). Rulings R9 (ejecta floor), R10 (envelope vs H mass), R11 (WD < 9.0). Concern 4 (Z-scheme non-monotonic; 300 M☉ PISN at Z☉) left to review as a named risk.
Task 3: review (opus) → Needs fixes: Critical Z=1 discontinuity / non-monotonic final mass; Important PISN at Z☉ (Ruling R12), 1500 R☉ RSG for compact >33 M☉ stars, [APPROX] labelled ModelDependent, R10 X_H not public.
Task 3: minor (deferred): Partial-stripping cIIb radius branch dead; wind-speed confidence ignores Z; consider moving HeStar helper to its own file.
Task 3: fix round 1/5 dispatched (resume implementer)
Owner decision (2026-11-11): multi-window layout — spec §3/§5.0/§7/§10 and plan Tasks 14–15 updated (commit ee61b8b). Later briefs must be re-extracted from the updated plan.
Task 3: fix round 1/5 (5 findings + minors addressed, 0 open; commits 24d9bc1..3cc3235)
Task 3: minor (deferred): 2 M☉ thin-envelope threshold → radius step ×60 at Z=1 for ~32.3–34 M☉ (use 1.6 M☉ or add S16 33 M☉ point); H-rich PISN ceiling (1/3) unreachable & test name misleading (200 M☉ Z=0.5 is PPISN via winds); floor-above-Z☉ comment premise / gate on bare stars; CO-core & radius step at Z=1 for bare WR stars (45 M☉ CO 13.0→10.3 crosses F12 M_CO=11); radius step where envelope runs out (11.7→4.5 R☉); α switch at 30 M☉ steps final mass ~13% at Z≠1 (inherited from research)
Task 3: note for Task 4/5: honour R12 direct-BH note for PISN-range cores above ceiling; CHE stars ~36–60 M☉ now PPISN (CHE collapsar effectively 20–35 M☉)
Task 3: complete (commits 74c30af..3cc3235, review clean)
Task 4: dispatched (opus, base ee61b8b)
Task 4: implementer DONE_WITH_CONCERNS (f44038f). Ruling R13 (NS ≥ 1.2 M☉; too-light stripped cores → WD; ultra-stripped E 1e50) sent as pre-review fix. Concerns 2 (PPISN 35–45 ejects 0, per research rule) and 3 (>120 M☉ Z☉ He-star fate flip 150 Ib / 250 BH) accepted as research-rule consequences — left to review.
Task 4: pre-review fix 1 done (02c5cfa, R13). Controller found root cause: stripped final mass < S16 CO core below ~13 M☉ (W19 fit extrapolated) → Ruling R14, pre-review fix 2 sent.
Task 4: implementer NEEDS_CONTEXT on literal R14 (showed it breaks monotonic He-star erosion); controller chose option B → R14 revised. Notes fix committed db7ede3.
Task 4: pre-review fix 2 done (f312863, R14 option B). Review dispatched (opus).
Task 4: review (opus) → Needs fixes: Important R1 breach >30 M☉ at Z=1 (fate flips at Z=1, Ruling R16); calibrated Ni > M_ej for low-mass stripped (Ruling R15). Fix round 1 incl. minors (IIb/II-L precedence doc, ultra-stripped small kick, PPISN zero-ejecta type None, stale comment, 15.0 M☉ test).
Task 4: minor (deferred): NeverCollapses note removal by substring (dead branch); pins can put E/M_ej on failed SN/PPISN — guard in Task 13; H&He CO uses max(S16,W19) (≤0.1 M☉); OutcomeEngine.cs 535 lines (Task 5 grows it)
Task 4: fix round 1 done (133d5a4); implementer also fixed Task 3 deferred minor (bare-WR CO step at Z=1, keep S16 CO/final ratio) — accepted by controller (it was the Task 3 re-reviewer's suggested fix). Re-review dispatched.
Task 4: fix round 1/5 (3 addressed, 1 new Important open — bare-WR CO rule breaks CO/He-shell continuity at 45 M☉, 120 M☉ off Z☉ (Ic↔Ib), envelope-exhaustion Z, non-monotonic CO; commits f312863..133d5a4)
Task 4: note for Task 13: pins can bypass the Ni ≤ 0.5·M_ej cap (pin only Ni, or small M_ej) — decide explicitly.
Task 4: fix round 2/5 dispatched (resume implementer)
Task 4: fix round 2/5 (3 addressed, 0 open; commits 133d5a4..23e9d2e)
Task 4: minor (deferred): stale comment OutcomeEngine.cs:363-365 "150 M☉ explodes as Ib" (now Ic); Z☉ H-envelope jump 40.6–45 M☉ (table vs scheme; structure only, fate unaffected); new sweeps' 10% bound loose (real <0.2%)
Task 4: complete (commits ee61b8b..23e9d2e, review clean)
Task 5: dispatched (opus, base 23e9d2e)
Owner constraint (CLAUDE.md, antivirus): Ruling R19 replaces R6 — in-app --snapshot rendering, no screen-capture scripts. Plan Tasks 14–17 updated accordingly.
Task 5: implementer DONE_WITH_CONCERNS (1f9c916). Rulings R17 (Ni cap excludes Ia/Iax), R18 (magnetar B default 0.8e14; test 5e13–1e15) sent as pre-review fix.
Task 5: pre-review fix done (52badd3). Plan screenshot steps updated (commit 9134dd7). Review dispatched (opus).
Task 5: review (opus) → Needs fixes: 7d on stripped stars erases He shell / flips Ib↔Ic (R20); 7d-raised He cores 35–41 not sent to PPISN (R20); Phillips M_B/Δm15 unowned (R21). Minors folded into the round: SLSN-II cut (R22), contradictory notes, magnetar t_d uses capped E_K (use E_SN+E_p per §5.3), Iax E_K label Speculative.
Task 5: minor (deferred): 30 M☉ S16→W19 step now toggles GRB for Full/Z0.1/Extreme (25–30 GRB, 30.1–31.3 NS Ib, 31.5+ GRB) — pre-existing Task 3 deferred α/W19 switch; some tests relational only; near-Ch channel fixed Ni 0.6 so 91T only via sub-Ch (by design; WD slider disabled for near-Ch per spec §2.1)
Task 5: fix round 1/5 dispatched (resume implementer)
Task 5: fix round 1/5 (7 addressed, 0 open; commits 52badd3..c47d174)
Task 5: minor (deferred): PpisnHeCore duplicated in OutcomeEngine and ProgenitorModel; PPISN via 7d keeps "iron core collapses" sentence; R22 threshold not pinned near 7e43; rotation leaves progenitor radius unchanged; OutcomeEngine.cs ~650 lines (partial with RotationBranches.cs)
Task 5: complete (commits 23e9d2e..c47d174, review clean)
Task 6: dispatched (opus, base c47d174)
Task 6: implementer DONE_WITH_CONCERNS (c40693a). Accepted: binding-energy normalization by the fit's own integral (2.37e53); ChannelId.cs extra file.
Task 6: note for Task 8: Swift XRT threshold 1e-13 is a ~10 ks figure — a minutes-long breakout flash must use an exposure-scaled threshold (signals §11: SN 2008D-like L_X detectable to ~200 Mpc by XRT), so "TooFaint at 1 Gpc" holds.
Task 6: note for Task 15: EventSampler has no cap — panels must check the expected count first and thin to ≤ ~5,000 drawn dots with a "1 dot = N events" label.
Task 6: review (opus) → Needs fixes: Important failed-SN E_anue up to 3.45e53 (R23); Detector has no threshold confidence (R24, plan-mandated). Minors folded: Super-K ≥17 MeV trigger + DUNE ν_e MSW (R25), FAST/SKA citation wording, ULTRASAT [APPROX], EventSampler bound saturation guard, explicit SignalChannel MayBeNegative flag.
Task 6: note for Task 13: SN 1987A Kam-II count ≈20 (hand estimate) sits at the top of spec §8's 6–20 — check against the real preset; consider a Kam-II 7.5 MeV threshold efficiency.
Task 6: note for Task 10/11: MeerKAT/FAST serve both continuum (§11 radio ~30 µJy) and pulsar-timing (§11 ~0.1 mJy) views with one Threshold — those tasks need per-view thresholds (rule then).
Task 6: fix round 1/5 dispatched (resume implementer)
Task 6: fix round 1 done (970cbf8): failed-SN Super-K 15 M☉ 36,543→20,980, 19.5 M☉ 48,078→21,127; E_anue ≤ 1.48e53; Super-K trigger horizon ~450 kpc, M31 0.72 → TooFaint.
Task 6: minor (deferred): failed-SN DUNE counts low (391–2,241) — after MSW DUNE sees mostly ν_x and the failed branch has ν_x only from PNS cooling; research gives no ν_x accretion figure, so no invented number (DUNE already [APPROX]). Revisit in final review / Project 2.
Task 6: re-review dispatched (scoped, c40693a..970cbf8)
Task 6: fix round 1/5 (all addressed, 0 open; commits c40693a..970cbf8). Minor (deferred): EventSampler doc understates blind spot (nonzero background too) and depth-4 bound-raise bias; TriggerTable static-init order comment.
Task 6: complete (commits c47d174..970cbf8, review clean)
Task 7: dispatched (opus, base 970cbf8)
Task 7: implementer agent ae9c504919716d753
Task 7: implementer DONE_WITH_CONCERNS (2d29487). Ruling R26 (GW calibration; spec §8 example 8→5 kpc, commit above). Concern 3 (BH keeps explosion amplitude to t_BH, ≤1.9e46 erg, inside 2D range) and 6 (spectrogram = expected power) left to review.
Task 7: review dispatched (opus)
Task 7: review (opus) → Needs fixes: Important fallback/collapsar BH gets failed-SN waveform (E_GW 1.1e46); GW labels ModelDependent though built on invented/[APPROX] values → Ruling R27 (whole GW panel Speculative). Minors folded: f0_eff comment, aligo network name, E_GW default note, tests (fallback BH, ring-down present, E_GW ≤ 2e46 at t_BH 4.7 s).
Task 7: note for Task 15: GW spectrogram needs a log colour scale (bounce bin is ~1e5× the track); BH ring-down (~3 kHz) is above the 2,048 Hz default range — acceptable.
Task 7: fix round 1/5 dispatched (resume implementer)
Task 7: fix round 1 done (b869429); re-review dispatched (sonnet). Failed SN at t_BH 4.7 s → E_GW 1.94e46 (guarded by test).
Task 7: fix round 1/5 (all addressed, 0 open; commits 2d29487..b869429)
Task 7: complete (commits 970cbf8..b869429, review clean)
Task 8: dispatched (opus, base b869429)
Task 8: implementer agent a25afb1dd62de347f
Task 8: implementer DONE_WITH_CONCERNS (036c512). Concerns to review: 2008D flash 3.3 s / 6.5e44 erg vs observed ~300 s / ~2e46 (dense-wind breakout not in §3); v_bo 0.58c outside W&K validity, temp ~1e9 K; XRT 1/√t threshold calibrated on real 2008D flash → model 2008D-like reaches ~75 Mpc; RSG flash 1.6–2.6e45 above research's rough 1e43–1e45.
Task 8: review (opus) → Spec ✅, quality Needs fixes: relativistic breakout unflagged (R28); TooFaint→Detected flip at flash end (R29 continuous fluence criterion); H-free flash contradicts 2008D silently (R30 caveat). Minors M1–M7 folded (temp label + cooling accessor, Ic He-formula [APPROX], Kistler x clamp/label, constants into PhysicalConstants, mag@ultrasat or ChannelId doc, EP horizon note, BAT reason text). Controller annotates signals.md:433/449 (3e46 = soft-X-ray-band part; bolometric W&K ~1e48).
Task 8: fix round 1/5 dispatched (resume implementer)
Task 8: fix round 1 done (6a4870b). XRT horizon: model 2008D-like ≈47 Mpc, observed-2008D fluence ≈388 Mpc (research ~200); EP ≈7.5 / ≈78 Mpc. Accepted: model's bare-star flash is short by design (R30 caveat explains).
Task 8: note for Task 15: X-ray Detect is a fluence integral (~0.8 ms/call) — cache per outcome/time, don't call per grid point.
Task 8: re-review dispatched (sonnet)
Task 8: fix round 1/5 (all addressed, 0 open; commits 0baaa7d..6a4870b). New minors: N1 CoolingPhase.At at exact StartS returns flash state (→ fix in Task 9, it consumes the accessor); N2 monotone test lacks dust case (deferred); N3 NotYet also = "first exposure filling" (→ Task 15 label); ProgenitorModel.cs:553 private σ_SB (→ Task 9 folds into PhysicalConstants).
Task 8: complete (commits b869429..6a4870b, review clean)
Task 9: dispatched (opus, base 6a4870b)
Task 9: implementer agent a7b99452f0262e241
Task 9: implementer DONE_WITH_CONCERNS (56669b0 carry-overs, a64697c light curves). Rulings R31 (Ia fixed κ 0.19, slider core-collapse only; spec updated) and R32 (one magnetar peak definition shared by engine and light curve) → sent as pre-review fix.
Task 9: note for Task 13: SN 1987A A_V not in presets.md — verify a literature value (E(B−V) ≈ 0.15–0.19 → A_V ≈ 0.5–0.6) and add it with a citation; model peaks m_V 2.70 at A_V 0 (≈3.3 at A_V 0.6, edge of ±0.5); model dome peaks ~70 d vs observed ~85 d.
Task 9: pre-review fix (R32) dispatched (resume implementer)
Task 9: pre-review fix done (db0933b, R32). 121/2,212 magnetar labels changed (105 II-P→SLSN-II fast/unstripped; 16 IIb→SLSN-II Partial 35.5–40 at low Z); now every H-rich magnetar is SLSN-II (lowest peak 9.2e43) — R22 "luminous II" branch unreachable; reviewer asked to judge (κ 0.2 vs 0.34 for H-rich ejecta). Non-magnetar 30 M☉ hypernova RSG shock-cooling peak 1.06e44 briefly, labelled II-P (documented).
Task 9: note for Task 13: R22 decision precedes preset pins — a pinned H-rich magnetar could cross 7e43 without relabel; re-evaluate the label after pins (no current preset affected).
Task 9: review dispatched (opus)
Task 9: review (opus) → Spec ✅, quality Approved; 12 minors. Controller folds the accuracy-relevant ones into one cleanup round: Ruling R33 (magnetar κ 0.2(1+X̄); keep+test R22 sub-threshold branch; engine II-P peak uses G19 like the drawn plateau; post-plateau tail = L_dep), consistent Arnett-2017 ε set, dedupe cooling suppression constants, fade doc, failed-SN text conditional on H envelope, BC/BSG [APPROX] docs, tests (M_V −17.512 at 50 d hand value; Ib handoff shape), split Curve into Signals/LightCurve.cs, BolometricCorrectionTests file.
Task 9: note for Task 13: SN 1987A preset should set κ = 0.10 (dome peak 82.7 d, m_V 2.83 at A_V 0 — closer to observed ~85–90 d).
Task 9: note for Task 14: κ slider disabled for Ia/Iax (R31).
Task 9: fix round 1/5 dispatched (resume implementer)
Task 9: cleanup round 1 done (75461df). No label changes over 9,072 outcomes. Stated II-P peaks 0.10–0.99 mag fainter (G19), II-L 1.04–1.74 mag fainter (Arnett rule for M_env < 1). Ruling R34: UI states peaks from the drawn curve only.
Task 9: note for Task 14: add/show LightCurveModel's own peak (R34); check II-L drawn peak brightness vs observed II-L (~−17 to −18) in final review.
Task 9: re-review dispatched (sonnet)
Task 9: cleanup round 1 re-review (all 12 addressed; 0 open; commits db0933b..75461df). New minor N1: comment E_K 4.72759e51 vs assertion 4.72722e51 in OutcomeEngineExtremeTests 16 M☉ SLSN-II test (→ Task 10 carry-over).
Task 9: complete (commits 6a4870b..75461df, review clean)
Task 10: Ruling R35 (⁴⁴Ti mass rule). Dispatched (opus, base 75461df)
Task 10: implementer agent a82c3fb16c062f6c3
Task 10: implementer DONE_WITH_CONCERNS (3104c90). SN 2014J 847 keV 4.03e-4 (1.72× measured); Cas A ⁴⁴Ti 1.745e-5. Rulings R36 (GRB pins; 980425 low-luminosity via pins), R37 (line escape via Klein–Nishina T0_line), R38 (NuSTAR threshold anchored to 1987A/Cas A detections) → pre-review fix. Accepted: Outcome gains Ti44Mass; PISN ⁴⁴Ti NotModeled; afterglow z=1 day correction (reviewer to check).
Task 10: pre-review fix dispatched (resume implementer)
Task 10: pre-review fix done (2a8f6d7): 980425 pinned GBM Detected (peak 1.8×, band 1.23×); 2014J 847 keV 3.02e-4 (1.29×), f_esc(75 d) 0.633; SPI ≈11.5 Mpc, COSI ≈20.8; NuSTAR detects 1987A & Cas A. Review dispatched (opus).
Task 10: review (opus) → Spec ✅, quality Needs fixes: Important — pinned 980425 afterglow outside formulas + wrong self-absorbed slope when ν_A > ν_m (Ruling R39: NaN/NotModeled there). Minors folded: D_L via ObserverSetup, prompt label "rest-frame 1–10⁴ keV", Cas A hand value comment, X-ray absorption moved to Extinction.XrayTransmission, SPI 1 MeV caveat. Minor 1 done by controller (signals §9.3 annotation).
Task 10: note for Task 11/13: SN 1998bw's radio must come from CsmInteractionModel (mildly relativistic SN shock); Task 13 checks it against presets §7 (Kulkarni 1998).
Task 10: fix round 1/5 dispatched (resume implementer)
Task 10: fix round 1 done (33f4faa). Accepted: classical afterglow drawn until ν_A ≥ ν_m (~50 d for 40 M☉, A*=0.25), then NotModeled — honest cut; the self-absorbed late regime is not in the research. Re-review dispatched (sonnet).
Task 10: fix round 1/5 (all addressed, 0 open; commits 2a8f6d7..33f4faa). New minors (→ Task 11 carry-over): SelfAbsorbedReason jargon — say plainly the afterglow curve ends (~7 weeks); Detect tail "fades over months" and ValidRange/AfterglowEndS 180 d vs ~50 d cut.
Task 10: complete (commits 75461df..33f4faa, review clean)
Task 11: Ruling R40 (radio thresholds per view). Dispatched (opus, base 33f4faa)
Task 11: implementer agent a968f51b7dfc5a0e3
Task 11: implementer DONE_WITH_CONCERNS (1cd7efa). Crab Ė 4.69e38, τ_c 1,223 yr; Sedov 5.33 pc; ν_p(30 d, A*=10) 9.59 GHz; NS 300 yr 1.84 MK; 1998bw-like radio ~7 mJy @5 GHz 30 d 38 Mpc (real several× brighter — Newtonian formulas at 0.58c; Speculative). Ruling R41 (Crab/1987A pins for Task 13). Accepted: g14 2.4 as written (10 km canonical); KB10 vs L&K spin-down for magnetars vs pulsars (documented); extended-envelope IIb shock scale.
Task 11: note for Task 13: R41 pins (NsSpinPeriodS, NsFieldG, PulsarRadioLumMjyKpc2, EquatorialRing → ringLx channel).
Task 11: review dispatched (opus)
Task 11: review (opus) → Spec ✅ (hand-offs: Task 15 maps CSM X-ray channels by model/channel not Messenger; cooling band from SurfaceTemperatureAt; PulseTimes source time, beam check by caller), quality Needs fixes: I1 SSA join not normalized (Ruling R42; research annotated 2 commits after 1cd7efa). Minors M1–M11 folded.
Task 11: note for Task 15: CSM X-ray channels → X-rays panel by model/channel id; NS cooling band before 100 yr from SurfaceTemperatureAt; PulseTimes are source time, caller applies BeamTowardEarth.
Task 11: fix round 1/5 dispatched (resume implementer)
Task 11: fix round 1 done (608e8a3). 5 GHz 30 d: II-P 10 Mpc 1.12 mJy, Ib 9.50, 1998bw-like 38 Mpc 14.5 (2.5e28), typical Ic 0.66; 1994I drawn 1.417e27. Research note constants corrected to exact (x0 1.29926, N 1.81897).
Task 11: minor (deferred): model radio peaks +1.1σ (II) / +1.8σ (Ib/c) above Bietenholz 2021 means — expected bias (CF06 speeds come from detected SNe; Bietenholz mean includes non-detections, 31% detected); [APPROX] 58,000 km/s = geometric mean of CF06's 32k–105k.
Task 11: re-review dispatched (sonnet)
Task 11: fix round 1/5 (all addressed, 0 open; commits 22fe800..608e8a3). New minor (deferred): ring L jumps 0→5% plateau at 3,000 d (documented). Task 13 must carry "observed/absorbed, 0.5–2 keV, don't re-absorb" into ringLx.
Task 11: complete (commits 33f4faa..608e8a3 incl. research notes 22fe800, 53a9558; review clean)
Task 12: dispatched (sonnet, base 53a9558)
Task 12: implementer agent a6606eef42952f7de
Task 12: implementer DONE_WITH_CONCERNS (61075c2). Accepted: PhysicalConstants Planck/Boltzmann cgs added; LightCurveModel.ColourTemperatureK shared (inverts blended B−V); IcBL with GRB 19,200 km/s; nebular 150 d CC / 200 d Ia [APPROX]. Ruling R43 (H-rich velocity extrapolated to V peak; test band 6,000–12,000) → pre-review fix.
Task 12: pre-review fix done (cd1ea51). Review dispatched (opus).
Task 12: review (opus) → Spec ✅, Needs fixes: I1 NaN colour before cooling start → 1,000 K spectrum AND light-curve V −2.4 mag for default star at 1–2.5 d (Task 9 bug); I2 one velocity for all lines (per-line §10.2 ratios); I3 Iax at Ia velocity. Ruling R45 (incl. R43 amendment: H-rich law held before max(t_peak, 15 d)). Minors 1–7, 9 folded; 8 (Icn C II lines) skipped — not in research, stays [APPROX].
Task 12: note for final review: plateau-family/IIn colour fixed at 6,000 K at all times (Task 9 choice) — real IIn early spectra are bluer.
Task 12: fix round 1/5 dispatched (resume implementer)
Task 12: fix round 1 done (14b54f3 light-curve colour, 6a44967 spectrum). Default star internal M_V 1–2 d −2.41 → −15.33; public absV NaN before cooling start (~2.5 d). Accepted: R45d hand value 12,701 (my 12,640 was a slip); IcBL baseline at 6000 Å; Ia nebular widths ~3,200 km/s noted (research silent).
Task 12: note for final review: visible-light channels are NaN between breakout and cooling start (~2.5 d for default star) — planar/bridge-phase optical light not drawn.
Task 12: re-review dispatched (sonnet)
Task 12: fix round 1/5 (all addressed, 0 open; commits cd1ea51..6a44967)
Task 12: complete (commits 53a9558..6a44967, review clean)
Task 13: dispatched (opus, base 6a44967)
Task 13: implementer agent aa33cca2537a6f345
Task 13: implementer DONE_WITH_CONCERNS (4b7b231). Validations pass except Kam-II 23.9 (engine NS 1.634 M☉ → E_b 3.9e53) → Ruling R46 (Blinnikov 2000 NS mass pin; expected 18.4) as pre-review fix. Accepted & to report to owner: 1987A Z = 0.1 (only BSG route); 1998bw radio 9× faint (4.8 vs 45 mJy, Kulkarni 1998); 1987A 847 keV 56× below SMM (no ⁵⁶Co mixing); sweep mag bound before dust; Cas A/93J A_V [APPROX]; Kepler 5.1 kpc. OutcomeEngine.cs 824 lines (final review).
Task 13: note for Task 15: Preset.ObserverSource; ringLx channel; ObservedFact.ChannelId may name an Outcome member (Ti44Mass) or a detector without a channel (cumulative@kamiokande-ii, cumulative@imb) — panels must handle.
Task 13: pre-review fix dispatched (resume implementer)
Task 13: pre-review fix done (63a8943). Kam-II 18.45 (E_b 3.02e53). Accepted: Blinnikov 2000's 1.6 M☉ mass cut read as baryonic (Lagrangian mass coordinate of a hydro model) → 1.444 gravitational via Lattimer & Yahil; report to owner. Review dispatched (opus).
Task 13: review (opus) → Spec ❌ (small: 2008D two-author set, sweep >+60 via dust — accepted R47c / documented), Needs fixes: I1 ring not in Chandra verdict; I2 2008D Swift blocked undisclosed; I3 2008D check in capped regime + wrong R* citation + unabsorbed/absorbed fact; I4 undisclosed misses (1998bw radio, 1987A 847 keV 22× over SMM window); I5 JSON silent on bad key; I6 Crab circular (label); I7 RF5 test can't fail (note; real check Task 14). Ruling R47. Minors M1–M10 folded (M1: split OutcomeEngine pins logic into OutcomePins.cs).
Task 13: note for Task 14: view-model test — editing an input drops preset pins and shows a status message (R47e).
Task 13: fix round 1/5 dispatched (resume implementer)
Task 13: fix round 1 done (4332f6f pure move → OutcomeEngine.Pins.cs, 0d8c568 fixes). Ruling R48 (sweep dust allowance accepted). Re-review dispatched (sonnet).
Task 13: fix round 1/5 (all addressed, 0 open; commits 63a8943..0d8c568). Controller fixed N1/N2 + Kam-II "some tables list 12" wording directly (commit below 0d8c568). Deferred minors: N3 test gaps (unknown top-level key test, preset named on bad enum, ChannelId messenger pairing); N4 duplicate "presets" key last-wins.
Task 13: complete (commits 6a44967..HEAD, review clean)
Task 14: dispatched (opus, base 6e7e078)
Task 14: implementer agent aa46534ca29888e7b
Task 14: implementer DONE_WITH_CONCERNS (992547f). Controller viewed shots2 (main 1366×768, details): layout clean; lab panel scrolls at 1366×768 (Advanced below fold); marker label 'Remnant becomes r…' truncated; M☉ glyph renders like Θ in the UI font — check. Accepted: observer edits also drop pins (spec 'any input'); GRB marker at burst peak; presetinfo snapshot window; OutcomeChanged on observer change. Note for final review: default 16 M☉ peak M_V −17.87 at 8.5 d (shock-cooling peak) — check vs typical II-P. Review dispatched (opus).
Task 14: review (opus) → Spec ❌ (badge tooltips empty — MultiBinding StringFormat on object Content; layout cramped at 1366×768), Needs fixes: I1 tooltips, I2 lab panel ~75 px short (denser pickers, Published badge, trim margins), I3 three wrong user-facing numbers (NS 12 km radius not across; beam 5° half-opening; "within 12.4 s"). Ruling R49 (observer edits keep explosion pins; spec updated). Minors M1–M12 folded (M1: U+2609 kept, Display text formatting, normal-weight unit).
Task 14: note for Task 15: panels must resample on RunNumber PropertyChanged (Re-roll doesn't raise OutcomeChanged); Task 15 snapshot run re-checks the 1366×768 lab panel and captures the white-dwarf form, expanded Advanced, details Notes, a collapsar's crowded markers.
Task 14: fix round 1/5 dispatched (resume implementer)
Task 14: fix round 1 done (9dd3099). Lab panel fits 1366×768; thin scrollbar at 1366×728. Re-review dispatched (sonnet).
Task 14: fix round 1/5 (all addressed, 0 open; commits 2eaf3c8..9dd3099). New minors: N1 dense shared light-curve citation paragraph (deferred); N2 re-selecting the same preset after an observer edit is a no-op (can't restore published distance/dust) + stale status message → Task 15 carry-over.
Task 14: complete (commits 6e7e078..9dd3099, review clean)
Task 15: dispatched (opus, base 9dd3099)
Task 15: implementer agent a6b080e6fc031dfd6
Task 15: implementer DONE_WITH_CONCERNS (e5289f1). 87 fps worst case (Debug, off-screen). Controller viewed instruments-sn1987a and main-sn1987a: panels read well; SN 1987A lab radio peaks ~300 mJy at 5 GHz ~15–20 d — real 1987A radio peaked ~150 mJy at 0.84 GHz ~day 4 and faded fast (BSG fast thin wind) → reviewer to check the wind the CSM model uses for a pinned BSG. Concerns: CSM starts before RSG breakout; 1998bw lab peak V 14.1 @23 d vs 13.62 @16 d undisclosed; badge judges all panel models; PanelPlot/Sparkline extra files; InstrumentPanelViewModel 782 lines. Review dispatched (opus).
Task 15: review (opus) → Spec ❌ (gamma badge not current-time; Task 14 snapshot hand-off partly undone), Needs fixes: I1 badge stuck Detected; I2 badge picks best of all models / afterglow unjudged; I3 panel views leak subscriptions on reopen; I4 facts & context hook outside error catch; X1 SN 1987A radio (WR-like wind A*0.25 + RSG speed; needs A*~0.006, ~42,000 km/s) undisclosed; X2 CSM starts before RSG breakout. Rulings R50, R51. Minors M1–M12 folded (split InstrumentPanelViewModel into partials; disclose 1998bw peak and SN 1054 peak).
Task 15: fix round 1/5 dispatched (resume implementer)
Task 15: fix round 1 interrupted by API network error (ENOTFOUND) mid-edit; uncommitted work kept; implementer resumed.
Task 15: fix round 1 done (985cdfb core, 3ce2fa4 app). X1: no paper verified with both wind density and early shock speed → disclosed in sn1987a Summary (lab ~350 mJy @1.4 GHz peaking ~day 18 vs observed ~130 mJy @0.84 GHz within days; Ball et al. 2001 citing Turtle 1987). Also fixed WD form opacity label (Task 14 bug). Not fixed (→ Task 16 carry-over): keyboard re-select of same preset no-op (M6); spectrum "10,000 Å" label clipped.
Task 15: SN 2011fe gamma found in snapshot: lab says SPI detects 847 keV (6.8e-5 vs narrow-line 3e-5) but INTEGRAL did not (Isern 2013). Cause: Ia lines are broad (~4,100 km/s rms) and §5.4's SPI figure is a narrow-line sensitivity → Ruling R52 (line-width-scaled threshold), assigned to the Task 10 implementer after this re-review.
Task 15: re-review dispatched (sonnet)
Task 15: fix round 1/5 (all addressed, 0 open; commits e5289f1..3ce2fa4). New minors N1 (gamma badge past ~180 d says "Over: too faint" though GBM detected — judge at PromptEndS) and N2 (Reason[0] on empty) → folded into the R52 follow-up.
Task 15: complete (commits 9dd3099..3ce2fa4, review clean)
R52 follow-up: dispatched to the Task 10 implementer (a82c3fb16c062f6c3) with Task 15 N1/N2, M6 keyboard re-select, spectrum "10,000 Å" label clip.
R52 follow-up done (79e07bc): SPI Ia horizon 5.97 Mpc (research 5–10), COSI 15.1 (Tomsick ~20); 2014J still Detected (3.04e-4 vs 1.11e-4); 2011fe not (best 0.76×; ATel 3822 limit 1.39e-4, 2σ preliminary, days 45–88 — lab 7.5e-5 below). Review dispatched (sonnet). Task 16 dispatched in parallel (opus, base 79e07bc) — disjoint files (Rendering/Scene vs gamma).
Task 16: implementer agent a62dea9f69f4e2a0b (base 79e07bc). R52 reviewer a44cfe331098bf6f4.
R52 review (sonnet) → compliant; Needs fixes: I1 2011fe limit not labelled preliminary in shipped text; I2 upper limit drawn as an observed diamond at a single time (add ObservedFact.IsUpperLimit + span). Minors: M1 dashed limit still narrow-line vs badge broad-line; M2 Iax width; M3 stale comments. Fix round sent to the Task 10 implementer (told to stage only its own files while Task 16 runs).
R52 follow-up: review fixes done (724fc01): ObservedFact.IsUpperLimit + span; dashed limit = broad-line threshold; Iax 5,000 km/s FWHM. Skipped M5/M6/M8 with reasons. Accepted without a separate re-review (small, focused); final whole-branch review to cover 79e07bc..724fc01.
Task 16: implementer DONE_WITH_CONCERNS (98b7426). 77 fps with all panels; 7 snapshots via GL.ReadPixels in one launch; controller viewed sn1998bw jets and sn1054 at 100 yr — clear and labelled. Concerns: GL message says 4.1 (spec says 4.1; controller's '3.3' was a slip); spec §6 CSM translucent shell not drawn (spec requirement → fix); scale-bar unit gaps; SN 1987A grows pulsar beams at 10 yr though none observed. Review dispatched (opus).
Task 16: review (opus) → Spec ❌ (CSM shell §6 missing), Needs fixes: I1 CSM shell; I2 pulsar note says "real period" (model's) + sn1987a must disclose no pulsar seen (R47b); I3 1998bw jets labelled 5° though pinned low-luminosity burst states no angle; I4 "Ejecta radius" is uniform-sphere edge, slower than line velocities — overlay note. ⚠️ GL disposal order, GL 4.1 failure path, off-screen fps. Fix round 1 dispatched (resume implementer).
Task 16: fix round 1 done (d6023e8). Accepted: sn2023ixf not switched to DenseCsm (would make it IIn; real 23ixf is a II with confined CSM, already disclosed); shell 1e15 cm, A*≥100 [APPROX]; 1987A ring not drawn (Project 3 remnant close-up); '365 days' for 1 yr (deferred minor). Re-review dispatched (sonnet).
Task 16: fix round 1/5 (all addressed, 0 open; commits 98b7426..d6023e8). New minors N1 (uniform-ball note during 3–10 yr blend), N2 (unguarded GL release on Closing), N3 (WindAStar ∞ at v_w 0) → Task 17 carry-over.
Task 16: complete (commits 724fc01..d6023e8, review clean)
Task 17: dispatched (sonnet, base d6023e8)
Task 17: implementer agent a290fb3c2165b7432
Task 17: implementer DONE_WITH_CONCERNS (a2fcaa3). Sweep of 14 presets to 1,000 yr: 0 problems (Debug+Release). fps 3D: 94.5 Debug / 79.0 Release (Iris Xe, off-screen). Review dispatched (sonnet).
Task 17: review (sonnet) → Needs fixes: Important README 2008D '100×' vs presets.json '200×' (+ presets-verification.md stale). Minors: 2011fe margin not in summary; 1998bw 'mildly relativistic' is sourced (presets.md:175, Kulkarni 1998) — cite; perf doc launch wording/proxy; UnitFormat boundary tests; --bench range text. Fix round 1 dispatched (resume implementer).
Task 17: fix round 1 done (105eec9); doc-only fixes, accepted without a separate re-review (final review covers). 
Task 17: complete (commits d6023e8..105eec9)
FINAL REVIEW: dispatched (opus), range main..105eec9
FINAL REVIEW (opus): ready to merge with fixes — 0 Critical, 6 Important (I1 notes contradict outcome; I2 pinned ejecta vs remnant mass; I3 extreme magnetar SLSNe brighter than any observed; I4 visible 'Not yet' wrong after breakout; I5 Phillips mislabelled/biased; I6 spectrum velocities not tied to E/M), 12 Minor. Ruling R53. ONE fix wave dispatched (fresh opus implementer); M9 skipped.
Owner request (2026-11-12): keep docs/research/scientific-inconsistencies.md updated with every model/research/observation mismatch; update statuses after the final fix wave.
FINAL FIX WAVE done (11a867b, 716ab39, 4b0af59): 1,705 tests; 392/684 SLSN labels → base class under R53 I3 (no fate/remnant/peak changes; presets unchanged; 2015bn stays SLSN-I); 1998bw BH 2.9 M☉; 2011fe Δm15 0.99 vs 1.103±0.035 (Pereira 2013) disclosed — every near-Ch Ia gives 0.99 (fixed curve shape). Accepted: Ic-BL spectrum refs Taddia 2019 (7e51/4) and Cano 2017 with GRB (2.5e52/6) instead of R53's literal Ib/c ref; Outcome.DeclineRateDm15B removed (R21 superseded). Scoped re-review dispatched.
FINAL RE-REVIEW (opus): 3 Important + 4 Minor residuals (I5 tooltip, N1 magnetar II-P size note, N2 Ia subtype fade sentences, M10 Ia text on Ic-BL, N4 Ibn/Icn colour, N5 log cap) — all fixed by the controller directly (13a037e, 5 new tests, 1,710 pass). Parked: N3 (--snapshot sweep doesn't count guarded marker/curve-row/scene errors; diagnostic only).
