# Issue 711 Text-Construction Queue Separation

Date: 2026-05-16

## Scope

This pass separates the largest `text-construction-surface-queue` families into
reviewed buckets without claiming a project-wide untranslated-zero state.

The queue is static Roslyn evidence over likely player-visible text construction
families. It is not runtime untranslated proof. A family can remain in the
queue because it mixes fixed popup leaves, already-covered owner routes, runtime
payloads, and still-unowned generated text in one upstream method.

## Evidence

Commands:

```bash
just text-construction-surface-queue
uv run python scripts/text_construction_surface_policy.py \
  --inventory /tmp/roslyn-text-construction-inventory.json \
  --format json \
  --include valuable \
  --limit 0
uv run python scripts/text_construction_surface_policy.py \
  --inventory /tmp/roslyn-text-construction-inventory.json \
  --format lanes-json \
  --include valuable \
  --limit 0
```

Baseline before this pass:

| Lane | Entries | Text constructions | Reviewed status before |
| --- | ---: | ---: | --- |
| `combat_message_frame_does` | 500 | 6,518 | 1 covered, 499 action-required |
| `history_generated_text` | 78 | 2,351 | all action-required |
| `journal_quest_routes` | 65 | 1,277 | all action-required |
| `producer_message_popup` | 687 | 5,447 | all action-required |
| `description_effect_detail` | 843 | 2,001 | all action-required |
| `display_name_composition` | 242 | 860 | all action-required |
| `screen_ui_direct_text` | 76 | 595 | all action-required |
| `activated_ability_names` | 114 | 506 | all action-required |

Reviewed status after this pass:

| Lane | Reviewed covered | Partial | Runtime | Likely true gap | Still unreviewed |
| --- | ---: | ---: | ---: | ---: | ---: |
| `combat_message_frame_does` | 4 | 7 | 0 | 3 | 486 |
| `history_generated_text` | 0 | 3 | 2 | 2 | 71 |
| `journal_quest_routes` | 0 | 1 | 0 | 0 | 64 |
| `producer_message_popup` | 1 | 5 | 1 | 1 | 679 |
| `description_effect_detail` | 0 | 1 | 0 | 2 | 840 |
| `display_name_composition` | 0 | 2 | 0 | 1 | 239 |
| `screen_ui_direct_text` | 2 | 1 | 0 | 0 | 73 |
| `activated_ability_names` | 1 | 1 | 0 | 1 | 111 |

## Reviewed Covered

These families have focused owner-route or asset evidence and can be counted as
reviewed coverage in the text-construction overlay.

| Family | Reason |
| --- | --- |
| `Combat.MeleeAttackWithWeaponInternal` | Existing overlay; covered by Does/message-pattern/L2 combat queue tests. |
| `MissileWeapon.MissileHit` | Missile hit multiplier and vital-area message shapes are covered by `MissileWeaponHitTranslationPatch`, message patterns, and L2 queue tests. |
| `GameObject.PerformThrow` | Throw hit/self-target popup routes are covered by owner tests and L2G target resolution. |
| `GameObject.Move` | Movement queue/popup owner routes are covered by owner tests and L2G target resolution. |
| `PickTarget.ShowPicker` | Range and visibility popup owner route is covered by focused popup tests and existing fixed popup leaves. |
| `GameSummaryScreen._ShowGameSummary` | Cause/details route is owned by `GameSummaryScreenShowTranslationPatch` and `GameSummaryTextTranslator`. |
| `SkillsAndPowersStatusScreen.UpdateDetailsFromNode` | Details panel fields are owned by `SkillsAndPowersStatusScreenDetailsPatch`. |
| `PhotosyntheticSkin.Mutate` | `Bask` ability name is covered by activated ability assets and localization coverage tests. |

## Partial Coverage

These families have real existing coverage, but whole-family closure would hide
unreviewed branches or runtime payloads.

| Family | Split decision |
| --- | --- |
| `Inventory.FireEvent` | Graveyard-zone queue and container-ownership popup are covered; runtime inventory action failure messages remain unowned. |
| `MissileWeapon.FireEvent` | Fixed and pattern-covered fire messages exist; ammo/load event `Message` and `Message2` remain runtime deferrals. |
| `TradeUI.ShowTradeScreen` | Vendor popups are covered; haggle `StringBuilder` text is runtime trade-state text. |
| `LongBladesCore.FireEvent` | Lunge/swipe/guard routes are covered; other branch text needs callsite-level comparison. |
| `LiquidVolume.HandleEvent` | Fixed liquid owner routes exist; several liquid interaction string builders remain runtime detail text. |
| `Tonic.HandleEvent` | Some tonic queue routes are covered; tonic inventory-action popup text is runtime supplied. |
| `Village.BuildZone` / `VillageCoda.BuildZone` | Village gospels and era history are covered; generated pets, origins, names, and story fragments need separate route proof. |
| `ZoneManager.SetActiveZone` | One generated time-suffix route is covered; zone names and journal note payloads remain runtime data. |
| `CherubimSpawner.HandleEvent` | Replace-description crash path is covered; generated cherub display-name/text remains unowned. |
| `SultanShrine.ShrineInitialize` | Description wrapper route is covered; generated shrine display-name needs route audit. |
| `StatusScreen.Show` | Some popups are covered; psychic glimmer description remains runtime-generated. |
| `XRLCore.PlayerTurn` | Several popup/message/journal routes are covered; runtime lines called out in issue-576 remain deferred. |
| `TinkeringScreen.Show` | Footer and prompt routes are covered; central list and branch-specific text need screen-owner review. |
| `InventoryScreen.Show` | Prompt/footer routes are covered; category/weight builders need runtime evidence. |
| `AbilityManager.Show` | Cooldown queue message is covered; selected ability `NotUsableDescription` remains runtime text. |
| `TinkeringDetailsLine.setData` | Bit cost and ingredient details are covered; display name, description, and mod description remain likely gaps. |
| `PsychicCombatSifrah..ctor` | Constructor popup route is partly covered; slot/detail text needs Sifrah screen evidence. |
| `MultiHorns.Mutate` | `Wrecking Charge` ability label is covered; mutation display-name variants need separate audit. |
| `MissileWeapon.ShowPicker` | Fixed popup leaves and hotkey text have coverage; aiming overlay text should be separated by route. |
| `Physic_AmputateLimb.FireEvent` | One owner route is covered; reach/no-target/no-limb branches need further proof. |

## Runtime Required

These are generated or runtime-state routes where static shape is not enough.

| Family | Reason |
| --- | --- |
| `OptionsUI.Show` | Restart prompt depends on runtime option display text. |
| `SultanRegion.FireEvent` | Region reveal description uses government, terrain, and HistorySpice data. |
| `Tombstone.GenerateTombstone` | Inscription text uses generated names, factions, objects, and random cause frames. |

## Likely True Gaps

These are the best next implementation targets from this separation pass.

| Family | Why |
| --- | --- |
| `VillageBase.getAVillageWall` / `VillageCodaBase.getAVillageWall` | Generated wall display names and descriptions have no focused owner translator. |
| `CherubimSpawner.BestowElement` | Element adjective, rules text, and generated cherub names are not safely exact leaves. |
| `TurretTinker.FireEvent` | Count-bearing `Tinker Turret [N remaining]` activated ability name needs a generated ability-name route. |
| `XRLCore._Start` | Legacy main-menu direct buffer text may still be visible and has no current owner proof. |
| `RandomAltarBaetylRewardManager.HandleRewardNode` | XML reward `Description` data source is not currently owned. |
| `ModGigantic.GetDescription(int,GameObject)` | Static world-mod descriptions are covered, but dynamic item-specific descriptions are not. |
| `BandageMedication.PerformBandaging` | MessageFrame/Does action feedback lacks a focused owner patch or owner-route proof. |
| `Tactics_Charge.PerformCharge` | Charge action feedback lacks focused runtime and owner-route evidence. |
| `MultiHorns.PerformCharge` | Charge/stopped/stomp message frames lack focused owner-route evidence. |

## Next Work

1. Use the likely-gap table as the next issue-711 implementation queue.
2. Do not add exact dictionary leaves for generated village, cherub, tombstone,
   SultanRegion, or dynamic ability-name text.
3. When a partial family is revisited, split it by callsite or route before
   changing the whole-family overlay status.
4. Runtime-required rows need fresh `Player.log` or targeted in-game evidence
   before owner promotion.
