# 2026-04-13 remaining localization baseline

## Scanner baseline (current reconciled bridge inventory from `docs/candidate-inventory.json`)

| Metric | Count | Source |
| --- | ---: | --- |
| scan date | 2026-04-13 | `docs/candidate-inventory.json:2-4` |
| Phase 1a unique files | 5367 | `.sisyphus/evidence/task-2-baseline-refresh.txt` |
| Phase 1a sink hits | 3778 | `.sisyphus/evidence/task-2-baseline-refresh.txt` |
| Phase 1a override hits | 874 | `.sisyphus/evidence/task-2-baseline-refresh.txt` |
| total sites | 4634 | `.sisyphus/evidence/task-2-baseline-refresh.txt` |
| bridge translated | 2930 | current reconciled bridge inventory status tally in `docs/candidate-inventory.json` |
| bridge unresolved | 1199 | current reconciled bridge inventory status tally in `docs/candidate-inventory.json` |
| bridge needs_review | 200 | current reconciled bridge inventory status tally in `docs/candidate-inventory.json` |
| bridge needs_patch | 40 | current reconciled bridge inventory status tally in `docs/candidate-inventory.json` |
| bridge excluded | 265 | current reconciled bridge inventory status tally in `docs/candidate-inventory.json` |
| needs_translation | 0 | current scan; prior 27-row pseudo-leaf queue is now carried as `excluded` residue |
| bridge non-translated remainder | 1704 | `bridge unresolved + bridge needs_review + bridge needs_patch + bridge excluded` |
| fixed-leaf validation | `0` checked / `0` issues | `.sisyphus/evidence/task-2-baseline-refresh.txt` |

## Partition 1 — owner-safe backlog

Backlog admission rule for this report: existing owner seam already evidenced in the issue-364 ledger and route-family notes; keep the queue tied to route family + current non-translated status, not sink visibility.

| Route family | Type | Route-held baseline status mix | Count | Why it stays owner-safe |
| --- | --- | --- | ---: | --- |
| `DidX` | `MessageFrame` | `needs_patch=338` | 338 | Existing `XDidYTranslationPatch` + `MessageFrameTranslator` + `verbs.ja.json`; follow-through stays on the current seam (`docs/reports/2026-04-11-didx-messageframe-batch-01.md:159-237`, `docs/reports/2026-04-12-issue-354-stale-bucket-reclassification-batch-01.md:12-45`) |
| `Does` | `VerbComposition` | `needs_review=283` | 283 | Dedicated Does seam and tests already exist; next work is family decomposition on the current route (`docs/reports/2026-04-11-does-verbcomposition-batch-01.md:9-37,101-126`) |
| `EmitMessage` | `Unresolved` | `unresolved=214` | 214 | Producer-owned queue under `GameObjectEmitMessageTranslationPatch`; keep generic sink traffic separate (`docs/reports/2026-04-11-emit-addplayermessage-batch-01.md:66-123`) |
| `Parts.GetShortDescription` | `Unresolved` | `unresolved=264` | 264 | Existing description owner seam with scoped asset/helper subfamilies (`docs/reports/2026-04-11-description-families-batch-01.md:23-61`) |
| `Effects.GetDescription` | `Unresolved` | `unresolved=180` | 180 | Shared active-effect owner seam; exact single-line descriptions now have dedicated L2 proof on the current route (`docs/reports/2026-04-11-description-families-batch-01.md:62-117`, `Mods/QudJP/Assemblies/QudJP.Tests/L2/ActiveEffectsOwnerPatchTests.cs:70-105`) |
| `Effects.GetDetails` | `Unresolved` | `unresolved=171` | 171 | Shared active-effect owner seam; templated multiline quickness/mutation details now have dedicated L2 proof, and remaining residue stays seam-owned rather than generic unresolved sink traffic (`Mods/QudJP/Assemblies/QudJP.Tests/L2/ActiveEffectsOwnerPatchTests.cs:142-180`, `Mods/QudJP/Assemblies/src/Patches/ActiveEffectTextTranslator.cs:67-170`) |
| **owner-safe backlog total** |  |  | **1450** |  |

Active-effect decomposition note for the current backlog:

- `Effects.GetDescription` is no longer an undifferentiated unresolved bucket: the existing owner seam now has explicit L2 proof for both plain exact and tagged exact single-line descriptions through `EffectDescriptionPatch` + `ActiveEffectTextTranslator` (`Mods/QudJP/Assemblies/src/Patches/ActiveEffectTextTranslator.cs:22-65`, `Mods/QudJP/Assemblies/QudJP.Tests/L2/ActiveEffectsOwnerPatchTests.cs:70-105`).
- `Effects.GetDetails` is likewise split on the current seam: `AdrenalControl2Boosted`-style `+{0} Quickness\n+{1} rank(s) to physical mutations` multiline templates are now proven through the owner route and existing scoped dictionary rows (`Mods/QudJP/Assemblies/src/Patches/ActiveEffectTextTranslator.cs:67-170`, `Mods/QudJP/Assemblies/QudJP.Tests/L2/ActiveEffectsOwnerPatchTests.cs:142-180`, `Mods/QudJP/Localization/Dictionaries/world-effects-status.ja.json:341-354`, `docs/candidate-inventory.json:72785-72796`).
- Explicit remaining `Effects.GetDetails` residue stays narrow and seam-owned: placeholder single-line detail rows such as `XRL.World.Effects/AnemoneEffect.cs` and builder/composed detail blocks such as `XRL.World.Effects/Adjusted.cs` are still follow-through on this route, not evidence of a missing owner seam (`docs/candidate-inventory.json:72768-72813`).

## Partition 2 — deferred buckets

Deferred rule for this report: do not let sink-observed / builder-display-name umbrellas read as owner-safe backlog. `Popup*`, `GetDisplayName*`, `<no-context>`, and generic `AddPlayerMessage` stay separate even when their current remainder count is zero. The current triage artifact only identifies routes, so freshness for Rosetta promotion still has to come from a new `Player.log`.

| Deferred family | Named route families | Named owner seam | Type coverage in inventory | Non-translated remainder | Current inventory status footprint | Why it stays deferred |
| --- | --- | --- | --- | ---: | --- | --- |
| Popup producer/handoff routes | `PopupShowTranslationPatch`, `PopupMessageTranslationPatch`, `PopupPickOptionTranslationPatch`, `PopupAskStringTranslationPatch`, `PopupAskNumberTranslationPatch`, `PopupShowSpaceTranslationPatch`, `QudMenuBottomContextTranslationPatch` | `PopupTranslationPatch.TranslatePopupTextForProducerRoute`, `PopupTranslationPatch.TranslatePopupMenuItemTextForProducerRoute` (`Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs:238-276`) | `Unresolved`, `Leaf`, `Template`, `Builder` | 0 | `translated=1451` | Route-identifying only, hold only. Keep the popup handoff outside owner-safe backlog until a fresh Rosetta `Player.log` proves the upstream producer owns the text. |
| Display-name builder/process routes | `GetDisplayNamePatch`, `GetDisplayNameProcessPatch` | `GetDisplayNameRouteTranslator.TranslatePreservingColors` (`Mods/QudJP/Assemblies/src/Patches/GetDisplayNameRouteTranslator.cs:66-185`) | `Builder` | 0 | `translated=143` | Route-identifying only, hold only. Keep the builder/process seam outside owner-safe backlog until a fresh Rosetta `Player.log` proves the composed name path is owned here. |
| Sink-observed fallback boundary | `UITextSkinTranslationPatch` | `UITextSkinTranslationPatch.TranslatePreservingColors`, `SinkObservation.LogUnclaimed` (`Mods/QudJP/Assemblies/src/Patches/UITextSkinTranslationPatch.cs:65-119`) | observation only | n/a | observation-only boundary | Observation only, not backlog. This boundary records unclaimed text, but it does not justify promotion for `Popup*` or `GetDisplayName*`. |
| generic `AddPlayerMessage` | generic `AddPlayerMessage` | observation-only sink path | `Unresolved`, `Leaf`, `Template` | 0 | `translated=581` | sink-observed umbrella; keep observation-only, keep scanner provenance on the sink/review path, and peel off producer-owned overlays separately (`docs/reports/2026-04-11-emit-addplayermessage-batch-01.md:12-65`) |
| `<no-context>` | `<no-context>` | actual upstream family as proven later | any | 0 | no current rows | keep separate from owner-safe backlog if it reappears; when runtime triage does surface it, preserve the mixed explicit outcomes (`static_leaf` / `logic_required` / `unresolved`) instead of collapsing it into one sink bucket; current scan still shows no `<no-context>` residue |
| **deferred buckets total** |  |  |  | **0** | `translated-only footprint = 2175` |  |

## Partition 3 — fixed-leaf / duplicate / pseudo-leaf residue

Residue rule for this report: keep fixed-leaf bookkeeping separate from route backlog. The current scan no longer exposes a `needs_translation` queue; the only remaining non-translated leaf residue is the pseudo-leaf batch now carried as `excluded`.

| Route family | Type | Route-held baseline status mix | Count | Reading |
| --- | --- | --- | ---: | --- |
| `SetText` | `Leaf` | `excluded=27` | 27 | This is the former 27-row pseudo-leaf queue (`""`, `" "`, `BodyText`, `SelectedModLabel`) now kept as residue instead of promotion work (`docs/reports/2026-04-11-fixed-leaf-owner-triage.md:52-97`, `docs/reports/2026-04-12-static-untranslated-quality-review.md:29-40`, `docs/reports/2026-04-12-issue-362-static-baseline-refresh.md:57-68`) |
| **fixed-leaf residue total** |  |  | **27** | `fixed-leaf validation = 0 checked / 0 issues`; no actionable duplicate/addition queue remains in this baseline |

Reference counts inside the current leaf inventory:

| Leaf status | Count |
| --- | ---: |
| `translated` | 829 |
| `excluded` | 27 |
| `needs_translation` | 0 |

## Partition 4 — explicit non-goals

Non-goal rule for this report: keep template/procedural/pseudo-generic families and unaudited unresolved families out of the owner-safe backlog until a narrower route proof exists. These are baseline-held buckets, not implementation-ready work for the next owner-safe wave.

| Route family | Type | Route-held baseline status mix | Count | Why it is a non-goal here |
| --- | --- | --- | ---: | --- |
| `HistoricStringExpander` | `ProceduralText` | `excluded=238` | 238 | explicitly procedural; not a dictionary/owner-safe backlog target (`docs/reports/2026-04-11-fixed-leaf-owner-triage.md:109-128`) |
| `JournalAPI` | `NarrativeTemplate` | `needs_review=115` | 115 | narrative-template bucket is outside the evidence-backed owner-safe set for this baseline (`docs/reports/2026-04-11-fixed-leaf-owner-triage.md:117-128`) |
| `ReplaceBuilder` | `VariableTemplate` | `needs_review=37` | 37 | variable-template bucket stays outside the current owner-safe wave (`docs/reports/2026-04-11-fixed-leaf-owner-triage.md:117-128`) |
| `Mutations.GetLevelText` | `Unresolved` | `unresolved=131` | 131 | not included in the current issue-364 owner-safe route-family evidence set; do not promote it into the existing backlog without its own route audit (`docs/reports/2026-04-12-issue-364-execution-ledger.md:106-145`) |
| `Mutations.GetDescription` | `Unresolved` | `unresolved=127` | 127 | same hold as `Mutations.GetLevelText`; no current owner-safe admission in the cited route-family set (`docs/reports/2026-04-12-issue-364-execution-ledger.md:106-145`) |
| `GetShort/LongDescription` | `Unresolved` | `unresolved=6` | 6 | description-adjacent but not part of the evidenced `Parts.GetShortDescription` / active-effect owner queues used for this baseline (`docs/reports/2026-04-11-description-families-batch-01.md:97-117`) |
| `SetText` | `Unresolved` | `unresolved=106` | 106 | generic UI sink residue; not one of the evidence-backed owner-safe route families in this baseline |
| `SetText` | `Template` | `needs_review=33` | 33 | template residue; keep out of the owner-safe route wave |
| `EmitMessage` | `Template` | `needs_review=8` | 8 | emit-route template residue is not part of the producer-owned unresolved queue admitted above |
| **non-goals total** |  |  | **801** |  |

## Baseline-held partition closure check

| Partition | Count |
| --- | ---: |
| owner-safe backlog | 1450 |
| deferred buckets | 0 |
| fixed-leaf residue | 27 |
| non-goals | 801 |
| **partitioned baseline-held total** | **2278** |

This closure check tracks the baseline-held route-family partitions that were grouped before the reconciled bridge inventory status rollup. It is broader than the current `bridge non-translated remainder = 1704` summary above because the partition tables still carry route-held baseline buckets instead of the reconciled bridge-only status tally.

The `2278 - 1704 = 574` difference is the set of sites that the reconciled bridge inventory now counts as translated while the partition tables still retain them inside route-held audit buckets (most visibly the `translated-only footprint = 2175` deferred bucket and other route-held baseline partitions). The bridge tally answers “which sites are still non-translated now?”, while the partition tables answer “which route-family buckets are still being tracked in the baseline audit?”.

Current decision: no family in this partition is promoted to owner-safe backlog yet. `Popup*` and `GetDisplayName*` stay on hold until a fresh Rosetta `Player.log` closes the freshness gap, and `UITextSkinTranslationPatch` stays observation only.

This report keeps `docs/candidate-inventory.json` as a bridge/view-only baseline input and leaves route ownership decisions anchored in the existing issue-364 evidence set rather than sink-visible translated totals.
