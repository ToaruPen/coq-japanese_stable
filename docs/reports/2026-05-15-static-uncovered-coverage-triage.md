# Static Uncovered Coverage Triage

Date: 2026-05-15

This report consolidates the current static-analysis view of QudJP localization
coverage gaps. It distinguishes true uncovered candidates from already-covered
fixed popup/message leaves, closure-overlay coverage, and sink-only routes that
need upstream ownership proof.

## Evidence

Commands run:

```bash
just static-producer-preview ~/dev/coq-decompiled_stable /tmp/qudjp-static-producer-inventory-current.json
just static-producer-check
just text-construction-surface-queue ~/dev/coq-decompiled_stable /tmp/qudjp-text-construction-inventory.json 50
uv run python scripts/static_producer_closure.py --queue message-candidates --format json --limit 0 > /tmp/qudjp-static-producer-message-candidates.json
uv run python scripts/text_construction_surface_policy.py --inventory /tmp/qudjp-text-construction-inventory.json --format json --include valuable --limit 0 > /tmp/qudjp-text-construction-surface-policy-valuable.json
just localization-coverage-map-check
```

Validation:

- `just static-producer-check`: passed, including 71 pytest checks, Ruff, and basedpyright.
- `just localization-coverage-map-check`: passed.
- `just localization-check`: passed.
- `just translation-token-check`: passed.
- `just build`, `just test-l1`, `just test-l2`, and `just test-l2g`: passed.
- Fresh static-producer preview matched `docs/static-producer-inventory.json`
  after removing no fields; the tracked producer inventory is current for the
  local decompiled source.

## Static Producer Status

Target surfaces:

- `EmitMessage`
- `Popup.Show*`
- `AddPlayerMessage`

Raw inventory:

| Metric | Count |
| --- | ---: |
| callsites | 2,208 |
| families | 1,012 |
| text arguments | 2,238 |
| Roslyn resolved callsites | 2,206 |
| Roslyn candidate callsites | 2 |
| Roslyn unresolved callsites | 0 |

Closure-overlay effective status:

| Effective status | Callsites | Text args | Meaning |
| --- | ---: | ---: | --- |
| `covered_by_owner_patch` | 1,066 | 1,074 | Covered by current owner-patch, data-route, or pre-emit template closure evidence. |
| `messages_candidate` | 457 | 457 | Fixed/pattern candidates; policy queue classifies them separately. |
| `needs_family_review` | 231 | 230 | Mixed fixed/generated/runtime families that remain split or policy-classified. |
| `runtime_required` | 197 | 204 | Static source cannot prove the emitted runtime shape. |
| `runtime_deferred_explicit` | 82 | 96 | Explicitly deferred runtime-proof rows. |
| `sink_observed_only` | 23 | 25 | Generic sink/wrapper observation, not an owner route. |
| `debug_ignore` | 151 | 151 | Debug/tool-like rows. |
| `owner_patch_required` | 1 | 1 | A residual owner-patch row outside the owner-action queue. |

Owner-action queue:

| Queue | Families | Callsites |
| --- | ---: | ---: |
| `static_producer_owner_queue` | 0 | 0 |

The owner-action queue being empty does not mean all runtime text is proven
covered. It means current owner-action rows are either closed by overlay,
classified as message candidates, deferred for runtime proof, sink-only, or
debug ignored.

## Message Candidate Policy Queue

The remaining static producer `messages_candidate` rows are mostly not true
gaps:

| Decision | Text args | Source files | Interpretation |
| --- | ---: | ---: | --- |
| `existing_dictionary_coverage` | 542 | 225 | Existing popup/message dictionary exact leaves. |
| `existing_message_pattern_coverage` | 144 | 36 | Existing `messages.ja.json` pattern coverage. |
| `existing_does_verb_route_coverage` | 5 | 4 | Existing DoesVerb route coverage. |
| `existing_owner_route_coverage` | 2 | 2 | Existing owner route coverage. |
| `reject_pseudo_leaf` | 5 | 5 | Empty/control pseudo-leaves; not translation work. |

Do not count these fixed popup/message leaves as uncovered unless a focused
runtime route proves that the existing dictionary or pattern does not serve the
visible output.

## Closed During Issue 702

These rows looked uncovered in the raw producer inventory, but are now closed by
pre-emit template/asset coverage or by focused owner-route coverage.

| Producer family | Lines | Surface | Previous status | Closure evidence |
| --- | ---: | --- | --- | --- |
| `XRL.World.Parts/PowerSwitch.cs::XRL.World.Parts.PowerSwitch.AccessCheck` | 536, 544, 571 | `EmitMessage` | `runtime_required` | PowerSwitch access fields are covered by `TranslatePartFields_NormalizesPowerSwitchAccessTemplates_BeforeEmitStage`. |
| `XRL.World.Parts/PowerSwitch.cs::XRL.World.Parts.PowerSwitch.TryPowerSwitchOn` | 784, 789 | `EmitMessage` | `runtime_required` | PowerSwitch activate fields are covered by `TranslatePartFields_NormalizesPowerSwitchEmitTemplates_BeforeEmitStage`. |
| `XRL.World.Parts/PowerSwitch.cs::XRL.World.Parts.PowerSwitch.TryPowerSwitchOff` | 816, 821 | `EmitMessage` | `runtime_required` | PowerSwitch deactivate fields are covered by `TranslatePartFields_NormalizesPowerSwitchEmitTemplates_BeforeEmitStage`. |
| `XRL.World.Parts/RemotePowerSwitch.cs::XRL.World.Parts.RemotePowerSwitch.HandleEvent` | 68, 73, 94, 99 | `EmitMessage` | `runtime_required` | Remote power switch uses the same translated `PowerSwitch` fields. |
| `XRL.World.Parts/TreatAsSolid.cs::XRL.World.Parts.TreatAsSolid.Match` | 107, 167, 189 | `AddPlayerMessage` | `runtime_required` | `TreatAsSolid.Message` is shipped as localized ObjectBlueprint overlay data and guarded by `TreatAsSolidMessages_AreLocalizedInObjectBlueprintOverlays`. |
| `XRL.World.Parts.Mutation/SunderMind.cs::XRL.World.Parts.Mutation.SunderMind.Nosebleed` | 319, 327, 335 | `EmitMessage` | `runtime_required` | SunderMind owner route now targets `Nosebleed` and translates nose/core/brain failure shapes. |
| `XRL.Liquids/LiquidWarmStatic.cs::XRL.Liquids.LiquidWarmStatic.GlitchSkills` | 192, 251 | `EmitMessage` | `runtime_required` | Warm static owner route translates mind fluctuation and skill-knowledge distortion messages. |
| `XRL.Liquids/LiquidWarmStatic.cs::XRL.Liquids.LiquidWarmStatic.GlitchMutations` | 306, 351 | `EmitMessage` | `runtime_required` | Warm static owner route translates genome fluctuation and mutation transmutation messages. |
| `XRL.World.Capabilities/AutoAct.cs::XRL.World.Capabilities.AutoAct.Interrupt` | 403, 438 | `AddPlayerMessage` | `runtime_required` | Existing AutoAct owner route now has production-pattern proof for stop-reason and spotted-object messages, including `exploring`, `waiting`, and `disassembling`. |
| `XRL.World.Effects/ProceduralCookingEffectWithTrigger.cs::XRL.World.Effects.ProceduralCookingEffectWithTrigger.Trigger` | 126, 134 | `AddPlayerMessage` | `runtime_required` | Cooking runtime owner route now translates resolved trigger notifications after token replacement. |
| `XRL.World.Effects/RealityStabilized.cs::XRL.World.Effects.RealityStabilized.OptionToContest` | 656, 683 | `Popup.Show*` | `runtime_required` | RealityStabilized owner route now translates normality-lattice contest prompts for Sifrah and percentage-estimate paths. |
| `XRL.UI/StatusScreen.cs::XRL.UI.StatusScreen.ShowMutationPopup` | 601, 625 | `Popup.Show*` | `runtime_required` | Dedicated StatusScreen mutation-popup owner route now translates mutation details, rank-up prompts, success/failure tails, and BaseMutation rank-boost reason families. |
| `XRL.UI/SkillsAndPowersScreen.cs::XRL.UI.SkillsAndPowersScreen.Show` | 634, 638 | `Popup.Show*` | `runtime_required` | Skill/power formatted descriptions are backed by localized `Skills.jp.xml` `Description` attributes and guarded by focused L1 coverage. |
| `XRL.Liquids/BaseLiquid.cs::XRL.Liquids.BaseLiquid.ObjectEnteredCell` | 415, 440 | `EmitMessage` | `runtime_required` | Liquid slip messages are `SlipperyMessage` templates translated by `StartReplaceTranslationPatch` before `GameText.VariableReplace` emits them. |

## Remaining True Static Producer Gap

After excluding fixed leaf/pattern coverage, generic sinks, and the Issue 702
closures above, the only remaining high-priority static producer gap is the
Physics damage sink. It should stay split; closing the whole family would
overstate coverage.

| Priority | Producer family | Lines | Surface | Status | Why it is likely real work |
| ---: | --- | ---: | --- | --- | --- |
| 1 | `XRL.World.Parts/Physics.cs::XRL.World.Parts.Physics.ProcessTakeDamage` | 3780, 3795, 3811 | `EmitMessage` | `runtime_required` | High-visibility damage route; line 3780 is `Event.Message` pass-through and lines 3795/3811 are Physics damage-frame composition. SunderMind's `NoDamageMessage` frame is now translated at the SunderMind owner route, but the Physics family remains too broad for full static closure. Follow-up issue: #703. |

## Sink Or Upstream-Owner Risks

These should not be implemented as sink fallbacks. They point to upstream owner
or runtime evidence work.

| Family | Lines | Surface | Status | Route decision |
| --- | ---: | --- | --- | --- |
| `Qud.API/IBaseJournalEntry.cs::Qud.API.IBaseJournalEntry.DisplayMessage` | 239, 243 | `Popup.Show*`, `AddPlayerMessage` | `runtime_required` | Journal display sink-like route; find generated journal entry owner. |
| `XRL.UI/ConversationUI.cs::XRL.UI.ConversationUI.Render` | 521 | `Popup.Show*` | `runtime_required` | Conversation renderer; use conversation node/template owner proof. |
| `XRL/CheckpointingSystem.cs::XRL.CheckpointingSystem.ShowDeathMessage` | 95, 99 | `Popup.Show*` | `runtime_required` | Death message display; trace upstream death reason/message ownership. |
| `XRL.UI/Popup.cs::XRL.UI.Popup.ShowConversation` | 1561 | `Popup.Show*` | `sink_observed_only` | Generic popup sink; not an owner gap. |
| `XRL.Messages/MessageQueue.cs::XRL.Messages.MessageQueue.AddPlayerMessage` | 135 | `AddPlayerMessage` | `sink_observed_only` | Generic message queue sink; not an owner gap. |

## Explicit Runtime Deferrals

These are known risk areas, but not safe static closures yet.

| Family | Lines | Surface | Status | Reason |
| --- | ---: | --- | --- | --- |
| `XRL.World.Parts/Campfire.cs::XRL.World.Parts.Campfire.CookFromIngredients` | 1012-1082 | `Popup.Show*` | `runtime_deferred_explicit` | Meal/HistorySpice output; component reconstruction or runtime evidence needed. |
| `XRL.World.Parts/Inventory.cs::XRL.World.Parts.Inventory.FireEvent` | 1657-2147 | `Popup.Show*` | `runtime_deferred_explicit` | Inventory action failure variables; existing inventory patches are partial. |
| `XRL.Core/XRLCore.cs::XRL.Core.XRLCore.PlayerTurn` | 945, 1057, 1335, 1780 | `Popup.Show*` | `runtime_deferred_explicit` | Existing XRLCore owner patch covers many branches, but not all runtime-deferred text. |

## TextConstruction Non-Producer Candidates

`scripts/text_construction_surface_policy.py` found:

| Classification | Families |
| --- | ---: |
| `player_visible_api` | 2,031 |
| `player_visible_owner_candidate` | 574 |

After de-prioritizing plain producer overlap, these are the highest-value
non-producer candidates.

| Priority | Route family | Surfaces | Count | Existing coverage state | Next evidence |
| ---: | --- | --- | ---: | --- | --- |
| 1 | `XRL.World/ZoneManager.cs:1752` `ZoneManager.SetActiveZone(Zone)` | `JournalAPI:60`, `HistoricStringExpander:1` | 61/117 | Partial; `AddPlayerMessage` overlap exists, journal writes are outside producer inventory. | Runtime zone-change/journal evidence and owner route test. |
| 2 | `XRL.World.ZoneBuilders/VillageBase.cs:2968` `getAVillageWall()` | `DescriptionAssignment:34`, `DisplayNameAssignment:12`, `HistoricStringExpander:6` | 52/81 | Likely uncovered; no direct patch/test match found. | Generated village object inspection and runtime evidence. |
| 3 | `XRL.World.ZoneBuilders/VillageCodaBase.cs:3257` `getAVillageWall()` | same as above | 52/81 | Likely uncovered; no direct patch/test match found. | Coda village generation evidence. |
| 4 | `XRL.World.Parts/CherubimSpawner.cs:231` `BestowElement(...)` | `DirectTextAssignment:11`, `DisplayNameAssignment:11` | 22/54 | Likely uncovered owner-candidate assignment route. | Spawned cherub display-name/text evidence. |
| 5 | `XRL.World.ZoneBuilders/InteractWithAnObjectDynamicQuestTemplate_FabricateQuestGiver.cs:28` | `HistoricStringExpander:18` | 18/62 | Likely partial/uncovered; dynamic quest fabrication. | Generated quest dialogue evidence. |
| 6 | `XRL.World.Parts/BandageMedication.cs:46` `PerformBandaging(...)` | `MessageFrame:18`, `Does:2` | 20/36 | Likely uncovered or only indirectly covered. | Bandage action runtime message evidence. |
| 7 | `XRL.World.Parts.Skill/Tactics_Charge.cs:136` `PerformCharge()` | `MessageFrame:17` | 17/42 | Likely uncovered or partial. | Charge runtime message evidence. |
| 8 | `XRL.World.Parts.Mutation/MultiHorns.cs:160` `PerformCharge(...)` | `MessageFrame:16` | 16/26 | Likely uncovered or partial. | Multi-horns runtime message evidence. |
| 9 | `Qud.UI/TinkeringDetailsLine.cs:103` `setData(...)` | `SetText:15`, `Description:1` | 16/24 | Likely UI assignment gap. | Screen field ownership and screenshot/runtime evidence. |
| 10 | `XRL.World.Parts.Skill/Physic_AmputateLimb.cs:38` `FireEvent(Event)` | `MessageFrame:11`, `Does:3` | 14/39 | Likely uncovered or partial. | Amputation action runtime evidence. |
| 11 | `XRL.World.ZoneBuilders/FindASpecificItemDynamicQuestTemplate_FabricateQuestGiver.cs:28` | `HistoricStringExpander:13` | 13/76 | Likely partial/uncovered. | Generated item quest evidence. |
| 12 | `XRL.World.ZoneBuilders/FindASpecificSiteDynamicQuestTemplate_FabricateQuestGiver.cs:42` | `HistoricStringExpander:13` | 13/70 | Likely partial/uncovered. | Generated site quest evidence. |
| 13 | `XRL.World.Parts/EaterCryptPlaque.cs:91` `GeneratePlaque()` | `HistoricStringExpander:12` | 12/57 | Likely uncovered display text. | Plaque object text evidence. |
| 14 | `XRL.World.Parts.Mutation/WillForce.cs:161` `Mutate(...)` | `ActivatedAbility:12` | 12/12 | Ability name route, not producer message coverage. | Ability list evidence and activated ability route audit. |
| 15 | `Qud.UI/SkillsAndPowersLine.cs:176` `setData(...)` | `SetText:11` | 11/14 | Likely UI row assignment gap. | Skills UI ownership and screenshot/runtime evidence. |

## Areas To Avoid Double Counting

These domains have meaningful existing coverage and should be treated as
partial, not globally uncovered:

- Static producer fixed popup/message leaves: mostly covered by existing
  dictionaries and message patterns.
- XRLCore/PlayerTurn: has owner patch and tests, but some branches remain
  runtime-deferred.
- Combat/MissileWeapon: has `CombatTextSurfaceTranslationPatch`,
  message-pattern tests, DoesVerb tests, and focused L2 coverage; remaining
  TextConstruction rows need route-level comparison.
- Inventory/item action: has `InventoryFireEventTranslationPatch`,
  inventory line patches, observability tests, and L2G target evidence, but not
  full inventory action closure.
- Screen UI: status/options/trade/tinkering have broad existing route tests;
  gaps should be screen-field specific.
- Historic narrative/gospel property walkers: existing Annals/HistorySpice
  coverage is real, but generated village/worldgen object names and
  descriptions remain likely gaps.
- Tutorial fixed popups: many are dictionary-covered; tutorial `LateUpdate`
  generated or route-specific TextConstruction rows still need runtime proof.

## Recommended Backlog Order

1. **Remaining static producer triage**:
   Split `Physics.ProcessTakeDamage` into `NoDamageMessage` pass-through and
   Physics damage-frame follow-up work; do not close the full family without
   runtime proof across player/third-person/no-damage/popup variants.
2. **Generated world/village object coverage**:
   `VillageBase.getAVillageWall`, `VillageCodaBase.getAVillageWall`,
   `CherubimSpawner.BestowElement`, `EaterCryptPlaque.GeneratePlaque`.
3. **Action feedback MessageFrame/Does families**:
   `BandageMedication.PerformBandaging`, `Tactics_Charge.PerformCharge`,
   `MultiHorns.PerformCharge`, `Physic_AmputateLimb.FireEvent`.
4. **UI assignment routes**:
   `TinkeringDetailsLine.setData`, `SkillsAndPowersLine.setData`, then
   focused status-screen popup branches.
5. **Runtime-deferred generated prose**:
   `Campfire.CookFromIngredients`, dynamic quest fabrication, JournalAPI
   generated quest/zone notes.

## Next Evidence Commands

For each selected family, start with one focused source trace plus existing
coverage search:

```bash
jq -r '.[] | select(.producer_family_id=="FAMILY_ID")' /tmp/qudjp-producer-effective-uncovered-candidates.json
rg -n 'FamilyOrPatchName|MethodName|Representative English text' Mods/QudJP/Assemblies/src Mods/QudJP/Assemblies/QudJP.Tests Mods/QudJP/Localization
sed -n 'START,ENDp' ~/dev/coq-decompiled_stable/path/to/Source.cs
```

For TextConstruction candidates, use:

```bash
jq -r '.entries[] | select(.source_file=="SOURCE" and .member_start_line==LINE)' /tmp/qudjp-text-construction-surface-policy-valuable.json
rg -n 'SourceType|MethodName|RouteName' Mods/QudJP/Assemblies/src/Patches Mods/QudJP/Assemblies/QudJP.Tests
```

Do not close a candidate from static shape alone. Closure requires a proven
owner route, focused L1/L2/L2G tests as appropriate, and runtime evidence when
the source text is data-driven or generated.
