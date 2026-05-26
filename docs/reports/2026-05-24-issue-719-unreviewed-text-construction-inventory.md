# Issue 719 Unreviewed Text-Construction Inventory

Date: 2026-05-24

## Scope

This report is the Issue #719 residual ledger after the Issue #737,
Issue #747, and Issue #762 closeout work. It covers the remaining `unreviewed` rows
from `scripts/text_construction_surface_policy.py`.

Static `unreviewed` rows are not direct runtime untranslated proof. A row can
remain because it is a real implementation gap, because route evidence exists
but still needs a narrow policy overlay, or because runtime evidence is needed
before the owner route can be safely classified.

## Generation

```bash
just text-construction-surface-queue "$HOME/dev/coq-decompiled_stable" /tmp/qudjp-issue719-current-text-construction.json 20
uv run python scripts/text_construction_surface_policy.py \
  --inventory /tmp/qudjp-issue719-current-text-construction.json \
  --format json \
  --include valuable \
  --limit 0 > /tmp/qudjp-issue719-next-policy.json
uv run python scripts/text_construction_surface_policy.py \
  --inventory /tmp/qudjp-issue719-current-text-construction.json \
  --format lanes-json \
  --include unreviewed \
  --limit 0 > /tmp/qudjp-issue719-next-unreviewed-lanes.json
uv run python scripts/text_construction_surface_policy.py \
  --inventory /tmp/qudjp-issue719-current-text-construction.json \
  --format residual-buckets-json \
  --include unreviewed \
  --limit 0 > /tmp/qudjp-issue719-next-residual-buckets.json
uv run python scripts/text_construction_surface_policy.py \
  --inventory /tmp/qudjp-issue719-current-text-construction.json \
  --format followup-issues-json \
  --include unreviewed \
  --limit 0 > /tmp/qudjp-issue719-next-followup-issues.json
```

## Current Counts

| Item | Count |
| --- | ---: |
| Text-construction inventory families | 17,459 |
| Valuable queue entries | 2,641 |
| `covered_by_owner_route` | 2,351 |
| `runtime_required` | 0 |
| `unreviewed` | 0 |
| `action_required` | 290 |
| `partial_coverage` | 0 |
| `likely_true_gap` | 0 |

Residual queue output is now consolidated under #719 rather than split into
new issues: 290 residual rows / 2,306 text constructions remain in
`/tmp/qudjp-issue719-pr-residual.json`, all with disposition
`likely_implementation_gap`. No rows remain classified as
`runtime_required`.

This branch brings the current policy output to `unreviewed=0` by connecting
narrow existing owner evidence for active-effect descriptions, selected UI line owners,
`ActionManager.RunSegment`, `SpindleNegotiation.FireEvent`, `Look.ShowLooker`,
`LightManipulation.HandleEvent`, `TinkeringScreen.PerformUITinkerMod`,
mutation `GetDescription()` routes, and world-mod `Mod*.GetDescription(...)`
routes. It also closes base `Effect.GetDetails()` through the existing
active-effect details owner patch and exact reviewed active-effect message producers for
`Prone`, `HolographicBleeding`, targeted `Asleep` message methods,
`ShatteredArmor.Apply`, `LifeDrain.HandleEvent(EndTurnEvent)`,
`BrainBrineCurse.GainChoice`, targeted cooking runtime FireEvent producers,
`IronshankOnset.FireEvent`, targeted mobility-block popup/queue producers,
targeted `RealityStabilized` event/interdict methods, `GlotrotOnset.FireEvent`,
`MonochromeOnset.FireEvent`, targeted `Phased` queue methods, and
`LatchedOnto.Expired`. Queue-only active-effect owner patches such as
`Cripple.Apply`, `Budding.Apply/Remove`, `EffectStaticMessageTranslationPatch`
targets, `CyberneticRejectionSyndrome`, `Emboldened`, `Healing` queue methods,
`Stasis.HandleEvent`, `Stressed.Apply/Remove`, and `Blaze_Tonic.Remove` are
also closed exactly. The final exact owner pass closes `BoostStatistic`,
`FungalSporeInfection.FireEvent`, `Mutating.Apply/HandleEvent(EndTurnEvent)`,
`BlinkingTicSickness.FireEvent`, `Meditating.Remove`, `Ill.Remove`, and the
eight exact `BasicCookingEffect_*::ApplyEffect(GameObject)` cooking popup targets. A
read-only subagent audit closes `Nosebleed.StartMessage` and
`Nosebleed.StopMessage` through the existing Nosebleed message-pattern family
tests. It also confirmed that most active-effect popup and queue residual rows
still need method-exact owner-route evidence beyond the generic sink routes.

The tranche 35 implementation closes three exact producer families:
`VehicleUnpowered.PreventActionMessage(GameObject)` popups,
`MechanicalWings.FireEvent(Event)` long-fall warnings, and
`Hooked.HandleEvent(CommandTakeActionEvent)` break-free messages. This removes
3 rows and 17 text constructions from the remaining #719 implementation queue.
The tranche 36 active-effect pass promotes `Submerged.Apply` and
`Burrowed.Apply` through the existing `SubmergedBurrowedOwnerTranslationPatch`
evidence, and extends `EffectGeneratedMessageTranslationPatch` to
`Stun.HandleEvent(BeginTakeActionEvent)` for the fixed remain-stunned queue
message. This removes another 3 rows and 21 text constructions from
`active_effect_message_frame_route_split`.
The tranche 37 fixed MessageFrame promotion pass closes `Sitting.StandUp`,
`Frenzied.TriggerBerserk`, `SporeCloudPoison.FireEvent`, and
`CardiacArrest.Apply` after adding focused repository MessageFrame tests for
their exact verb/extra pairs. This removes another 4 rows and 15 text
constructions from `active_effect_message_frame_route_split`; mixed popup /
dynamic-message methods were left for later owner-route work.
The tranche 38 active-effect MessageFrame implementation closes
`Running.Remove`, `ResummonGloaming.HandleEvent(EnteredCellEvent)`, and
`CookingDomainArtifact_IdentifyAllInZone_ProceduralCookingTriggeredAction.Apply`
after adding focused dictionary leaves for `stop/power skating`, `reappear`,
and `flush/with understanding of {0}` plus repository MessageFrame tests. This
removes another 3 rows and 7 text constructions from
`active_effect_message_frame_route_split`.
The tranche 39 active-effect MessageFrame implementation closes
`LifeDrain.Apply`, `LifeDrain.HandleEvent(InventoryActionEvent)`, and
`Bleeding.StartMessage` through fixed `XDidY`/`XDidYToZ` dictionary leaves and
focused repository tests. This removes another 3 rows and 21 text constructions
from `active_effect_message_frame_route_split`.
The tranche 40 active-effect implementation closes `Beguiled.Remove` by proving
the fixed `DidXToY("lose", "interest in", ...)` shape through the existing
`XDidYToZ`/MessageFrame route and a focused repository dictionary test. It also
extends `ConversationScriptPopupTranslationPatch` to cover the
`Confused`/`Dominating` `IsConversationallyResponsiveEvent` messages, including
the `Does(...)` conversation failures and the mental `Poss("mind")` failures.
This removes another 3 rows and 10 text constructions from
`active_effect_message_frame_route_split`.
The tranche 41 active-effect implementation adds
`ActiveEffectMessageFrameOwnerTranslationPatch` to scope exact `DidX`
MessageFrame owners and closes `Immobilized.Apply`, `Stuck.Apply`, and
`LatchedOnto.HandleEvent(BeginTakeActionEvent)` with L2 owner-scope and L2G
target-resolution evidence. It also adds finite `stuck in {0}` and
`grabbed by {0}` MessageFrame leaves. This removes another 3 rows and 20 text
constructions from `active_effect_message_frame_route_split`; `CardiacArrest.Remove`
stayed residual because the method also mixes player-only popups and a nested
`Ill` application outside the non-player `DidX("look", "less stricken")`
subshape.
The tranche 42 social active-effect implementation extends the same
method-scoped owner patch to `Lovesick.Apply`, `Beguiled.Apply`,
`Proselytized.Apply`, and `Rebuked.Apply`, adds the finite `XDidYToZ`
MessageFrame leaves for their social status messages, and covers each
`JournalAPI.AddAccomplishment` text/mural/gospel argument through
storage-time journal patterns. `Rebuked.Apply` keeps
`HistoricStringExpanderPatch` disabled; the expanded admonishment sentence is
covered at the JournalAPI storage boundary instead. This removes another 4 rows
and 41 text constructions from `active_effect_message_frame_route_split`,
leaving only `CardiacArrest.Remove` in that bucket. The tranche 43
active-effect owner update then closes that final row by adding exact
owner-scope coverage for `Popup.Show("{{G|Your heart restarts!}}")`,
`Popup.Show("{{G|Your hearts restart!}}")`, and the nested `Ill.Apply` popup
message `"You feel shaken and infirm."`.
The mutation command audits close `SlimeGlands.HandleEvent`,
`AcidSlimeGlands.FireEvent`, `MultiHorns.FireEvent`, and
`Clairvoyance.FireEvent` through existing `XDidY` MessageFrame,
popup-template, and message-log dictionary routes for their fixed visible
shapes. A follow-up mutation command audit also closes
`ForceWall.HandleEvent`, `WaveformWorm.FireEvent`, and
`BurrowingClaws.FireEvent`; `SlogGlands.FireEvent` remains residual because
`That is out of range! (10 squares)` lacks exact existing dictionary/test
evidence, and `Stinger.HandleEvent` remains residual because
`You don't have a stinger.` lacks exact existing evidence.
The latest fixed-popup dictionary audit closes `DeathGate.FireEvent` because
both visible popup strings are already present in `ui-popup.ja.json` and served
by the existing generic popup route. The same audit keeps
`ObjectFinder.ConfigFilters` residual because several option/state strings are
not proven by the popup dictionary route, while a parallel read-only audit
keeps `Stomach.FireEvent`, `Stomach.HandleEvent(BeginTakeActionEvent)`,
`GeomagneticDisc.DoThrow`, `Scores.Show`, `Food.HandleEvent`, and
`CheckpointingSystem.ShowDeathMessage` residual because existing patches cover
only adjacent methods, callsite subsets, or generic sinks.
The follow-up low-count fixed-popup pass also closes `ChavvahSystem.Hide`,
`Switch.FlipSwitch`, and `InteriorPortal.HandleEvent(InventoryActionEvent)`.
It keeps `ReclamationSystem.HandleEvent(EnteringZoneEvent)`,
`Skills.WishSkill`, `RecoilAbility.HandleEvent`, `Wings.HandleEvent`, and
`Domination.BreakDomination` residual because their evidence is property-backed,
debug/wish-only, picker-route, generated-string, or adjacent-call uncertainty
rather than whole-family fixed-popup proof.
A subsequent low-count fixed-popup pass closes
`BodyPart.SetAsPreferredDefault`, `DefensiveChromatophores.AttemptScintillate`,
`UnwelcomeGermination.FireEvent`, `TeleportGate.CheckPossibleSubject`, and
`CyberneticsOnboardRecoilerImprinting.HandleEvent` because all inspected visible
popup strings are fixed literals already present in `ui-popup.ja.json` and served
by the existing generic `Popup.Show` / `ShowFail` / `ShowSpace` routes. It keeps
`IGrenade.HandleEvent`, `CyberneticsOnboardRecoilerTeleporter.ActuateTeleport`,
`MechanicalWings.FireEvent`, `DesalinationPellet.HandleEvent`,
`NeutronFluxContainment.HandleEvent(BeginTakeActionEvent)`, and
`GameObject.ArePerceptibleHostilesNearby` residual because they are missing
`ui-popup` proof, are composed/generated, or are covered only by a different
owner/pattern route.
The following exact existing-route pass additionally closes
`KeybindsScreen.SelectInputType` through the existing `PopupPickOption` title
and option route, and closes `AchievementViewRow.SetAchievementData` /
`AchievementViewRow.SetHiddenData` through the existing `setData` owner patch.
The latest UI-screen pass also closes 17 exact owner-patched UI screen rows
(`CharacterAttributeLine`, `CharacterEffectLine`, `ModMenuLine`,
`EquipmentLine`, `HelpRow`, `AbilityManagerLine`,
`InventoryAndEquipmentStatusScreen`, `InventoryLine`, `TradeLine`,
`TinkeringStatusScreen`, `PopupMessage.ShowPopup`, `TinkeringLine`,
`FactionsLine`, `SelectableTextMenuItem.SelectChanged`, `TinkeringBitsLine`,
`KeybindsScreen`, and `ModManagerUI.OnSelect`) and the exact
`Description.GetShortDescription(bool,bool,string)` owner route. Subagent
audits found no exact close candidates in the current display-name residual
buckets, and found only the short-description route in the audited small
description buckets. The latest producer/combat audit closes exact existing
owner-patched families for `Disassembly.Continue`,
`TinkeringScreen.PerformUITinkerBuild`, `LatchesOn.FireEvent`,
`TattooGun.AttemptTattoo`, `Beguiling.Cast`, `Engraver.AttemptEngrave`,
`Physics.HandleEvent(InventoryActionEvent)`, `ITeleporter.AttemptTeleport`,
the energy-loader `FireEvent` routes, `DataDisk.HandleEvent`,
`PetEitherOr.explode`, `Bed.AttemptSleep`, `LiquidVolume.Pour`,
`Chair.SitDown`, `StairsDown.CheckPullDown`, `Garbage.AttemptRifle`,
`EnergyCellSocket.AttemptReplaceCell`, and `Enclosing.EnterEnclosure`.
The registry-backed static-producer pass then closes 248 additional
`producer_message_family_audit` rows by reusing
`scripts/static_producer_closure.py` entries whose tracked inventory status is
exactly `owner_patch_required`; `needs_family_review`, `runtime_required`,
Sifrah, broad producer, and active-effect split rows stay out of this automatic
promotion. The latest UI/description pass also closes exact existing owner
routes for `PlayerStatusBar.Update`, `AbilityManagerScreen.HandleHighlightLeft`,
`MainMenu.Show`, `MissileWeaponArea.AfterRender`, `TradeScreen.UpdateTotals`,
`TradeScreen.UpdateMenuBars`, `CharacterMutationLine.setData`,
`QuestsLine.setData`, `HighScoresScreen.Show`,
`CherubimSpawner.ReplaceDescription`, `SavesAPI.ReadSaveJson`, and selected UI menu description
families in `CyberneticsTerminalScreen`, `StatusScreensScreen`,
`InventoryAndEquipmentStatusScreen`, `JournalStatusScreen`, `BookScreen`, and
`Credits.UpdateMenuBars`. The follow-up UI/display pass also closes exact
owner routes for `AbilityManagerLine` menu options, `KeybindRow.dataRow`,
`MessageLogLine` menu options, `PickGameObjectLine` menu options,
`QudMutationsModuleWindow.UpdateControls`, and
`IBaseJournalEntry.GetDisplayText`. The follow-up display-name pass also closes
`JournalVillageNote.GetDisplayText` because the existing journal entry display
patch applies to derived journal notes and the L2 test covers a village note
display result. The existing-patch-only UI menu pass closes
`AchievementView.UpdateMenuBars`, static `HighScoresScreen` menu-option
descriptions, and static/default `OptionsScreen` menu-option descriptions
through already-present owner patches and L2 tests. `HighScoresScreen.UpdateMenuBars`
is now covered by the explicit menu-option owner patch; `OptionsScreen.HandleMenuOption`
remains unreviewed because the current strict gate does not prove that exact
family route.
The latest UI option pass closes `QuestsLine.categoryExpandOptions` and
`QuestsLine.categoryCollapseOptions`, because the existing `QuestsLine` owner
patch translates those static menu descriptions and the L2 test asserts both
menu-option outputs. Similar static option rows such as
`FilterBarCategoryButton.categoryExpandOptions` stay residual where current L2
evidence does not assert the menu-option path.
A follow-up UI residual audit closes `CharacterStatusScreen.BUY_MUTATION` and
`CharacterStatusScreen.SHOW_EFFECTS` because the existing
`CharacterStatusScreenMutationDetailsPatch` translates those static menu option
descriptions and the L2/L2G evidence covers the exact owner route. The same
audit keeps `SkillsAndPowersStatusScreen.ShowScreen`,
`WorldGenerationScreen._ShowWorldGenerationScreen`,
`TradeScreen.HandleHighlightObject`, `PopupMessage.Update`,
`LeftSideCategory.setData`, hotkey-update rows, and remaining menu-bar rows
residual because current evidence targets adjacent methods or observation-only
sinks rather than those exact families.
The chargen menu/build-library implementation tranche closes exact owner routes
for `QudBuildSummaryModuleWindow.GetKeyMenuBar`,
`QudMutationsModuleWindow.GetKeyMenuBar`, and
`QudBuildLibraryModuleWindow.GetSelections`. The new iterator-owner postfix
translates yielded menu descriptions and build-library selection titles through
the existing chargen structured text dictionaries, with focused L2, L2G, and
policy evidence. This removes 3 rows and 26 text constructions from
`description_assignment_route_split`.
The follow-up small owner tranche closes `QudBuildLibraryModuleWindow.GetKeyMenuBar`,
`QudGamemodeModuleWindow.QUICKSTART`, and
`UrchinBelcher.UrchinBelcher()` through exact owner coverage for the remaining
build-library option, the debug quickstart menu option, and Urchin Belcher
description/command constructor assignments. This removes another 3 rows and
13 text constructions from `description_assignment_route_split`.
The follow-up description-assignment owner tranche closes
`QudAttributesModuleWindow.GetKeyMenuBar`,
`CyberneticsMotorizedTreads.HandleEvent(ImplantedEvent)`, and
`CyberneticsStasisArena.HandleEvent(GetCyberneticsBehaviorDescriptionEvent)`.
The chargen menu option route reuses the existing structured points-remaining
translator, while the new cybernetics description-assignment owner postfix
translates the motorized-treads body-part name/description and the generated
stasis arena behavior description. This removes 3 more rows and 13 text
constructions from `description_assignment_route_split`.
The next description-assignment tranche closes
`GetMovementCapabilitiesEvent.Add(...)`, `Biocapacitor.Biocapacitor()`, and
`CyberneticsOpticalMultiscanner.HandleEvent(GetCyberneticsBehaviorDescriptionEvent)`.
The movement-capability owner route translates generated attack/toggle suffixes,
the biocapacitor ctor translates the fixed charge-source description, and the
optical multiscanner owner route translates both its behavior description and
Sifrah bonus rule. This removes another 3 rows and 12 text constructions from
`description_assignment_route_split`.
The follow-up cybernetics description-assignment tranche closes
`CyberneticsSingleSkillsoft.HandleEvent`,
`CyberneticsSocialCoprocessor.HandleEvent`, and
`CyberneticsTechIndexer.HandleEvent` for
`GetCyberneticsBehaviorDescriptionEvent`. The owner route translates dynamic
skillsoft skill descriptions, generated social-coprocessor water-ritual /
Proselytize rules, and the Tech Indexer robot scan/Sifrah rules. This removes
another 3 rows and 6 text constructions from `description_assignment_route_split`.
The next mixed description-assignment tranche closes
`CyberneticsTreeSkillsoft.HandleEvent`,
`FoliageCamouflage.FoliageCamouflage()`, and
`UrbanCamouflage.UrbanCamouflage()`. The cybernetics owner route translates
dynamic skill-tree access descriptions and added rules through the chargen
dictionary helpers, while the miscellaneous description-assignment route
translates the fixed foliage/urban camouflage ctor descriptions. This removes
another 3 rows and 6 text constructions from `description_assignment_route_split`.
The follow-up small description-assignment tranche closes
`QudCustomizeCharacterModuleWindow.GetPets()`,
`QudGamemodeModuleWindow.GetSelections()`, and
`FabricateFromSelf.AbilityDescription`. The chargen owner route now translates
pet descriptions plus game-mode titles/descriptions from the existing chargen
dictionary helpers, and the FabricateFromSelf ability-description getter route
translates the generated `Fabricate ...` ability text. This removes another 3
rows and 5 text constructions from `description_assignment_route_split`.
The final description-assignment tranche closes
`MechanimistLibrarian.Initialize()`,
`Wings.OnRegenerateDefaultEquipment(Body)`,
`DecoyHologramEmitter.CreateHologramOf(GameObject)`,
`GameObject.DescribeActivatedAbility(Guid,Action<Templates.StatCollector>)`,
and `Banner.HandleEvent(GetShortDescriptionEvent)`. The owner routes translate
the Mechanimist librarian identity/title/short description, generated wings
body-part description, hologram short-description prefix, activated-ability
template detail labels, and banner short-description rules. This removes the
last 5 rows and 33 text constructions from `description_assignment_route_split`.
The producer/combat existing-owner pass closes exact existing routes for
`SkillsAndPowersScreen.SelectNode`, `StatusScreen.ShowMutationPopup`, the four
`Campfire.Nostrums*` treatment methods, `Door.AttemptOpen`, the five
`Door.HackingResult*` methods, and `Leveler.RapidAdvancement`. These were
promoted only where existing owner patches, L2 coverage, and L2G target
resolution already existed; no new C# implementation was added in this pass.
The follow-up existing-owner pass also closes the exact
`PowerSwitch.HackingResult*` and `TemplarPhylactery.HackingResult*` popup
methods, plus the three `CyberneticsTerminal2.HackingResult*` methods already
targeted by `HackingSifrahResultTranslationPatch`. The
`CyberneticsTerminal2.HackingResultPartialSuccess` row remains residual because
the existing patch does not target that exact method.
The same existing-owner sweep also closes `VehicleSeat.AttemptPilot`,
`DecoyHologramEmitter.ActivateHologramBracelet`, and
`TeleporterPair.AttemptTeleport` through their existing exact popup owner
patches and L2/L2G evidence.
A read-only combat subagent audit then closes exact existing owner routes for
`VehicleRecall.HandleEvent`, `GameObject.HandleRename`, `Stuck.FireEvent`,
`FactionDeed.HandleEvent`, `AnimateObject.HandleEvent`, `EelSpawn.HandleEvent`,
`WaterRitualBuySecret.RevealEntry`, and `EquipmentAPI.TwiddleObject`. The same
audit kept `Firefighting.AttemptFirefightingCore` and
`SpaceTimeVortex.ApplyVortex` residual because their existing evidence does not
prove the whole family.
The latest exact existing-patch audit closes
`NeutronFluxContainment.HandleEvent(NeutronFluxPourExplodesEvent)`,
`FabricateFromSelf.Activate(bool)`,
`Psychometry.HandleEvent(InventoryActionEvent)`, and
`Repair.RepairResultCriticalFailure(GameObject,GameObject)`. These promotions
combine exact owner-patch evidence with existing generic popup or MessageFrame
evidence where the family also contains fixed failure popups, status failure
frames, or `XDidYToZ` verb frames. The same audit keeps `ThiefBot.FireEvent`,
`EngulfingDescends.FireEvent`, `NeutronFluxContainment.HandleEvent(BeginTakeActionEvent)`,
`Physics.ProcessTargetedMove`, `Container.AttemptOpen`, `SunderMind.Tick`,
`GiveReshephSecret.HandleEvent`, and the `SocialSifrahTokenGift/Item.CheckTokenUse`
families residual because their current patches or tests cover only part of the
method family.
The follow-up mutation/popup mixed audit closes `MassMind.FireEvent`,
`PackRat.FireEvent`, `Precognition.FireEvent`, and `ErosTeleportation.Cast` by
combining exact owner-patch coverage for generated queue/yell text with existing
generic popup, RealityStabilized, or dictionary-backed routes for the fixed
popup branches in those exact methods.
The next producer-side sweep closes `Campfire.Preserve`,
`Campfire.PreserveExotic`, `JoppaZealot.ZealotDeclaim`,
`SixDayZealot.ZealotDeclaim`, `GameObject.ChangeCompanionAbilityUse`,
`Belcher.Cast`, and `TerrainTravel.HandleEvent(ObjectEnteredCellEvent)` through
existing exact owner patches and tests. The same producer audit keeps
`Stomach.FireEvent`, `LifeDrain.FireEvent`, and `ElevatorSwitch.FireEvent`
residual because the existing evidence does not prove all visible shapes in
those families.
A read-only mid-count combat audit closes `Campfire.Cook`,
`Examiner.ResultPartialSuccess`, `ConversationScript.IsPhysicalConversationPossible`,
and `TradeUI.DoVendorExamine` through exact owner patches and L2/L2G evidence.
The latest existing-patch pass closes exact owner routes for
`GameObject.ConfirmUseImportantAsync`, `GameObject.ConfirmUseImportant`,
`GameObject.ToggleActivatedAbility`, `TerrainTravel.HandleLeavingCell`,
`Precognition.OnBeforeDie`, `TradeUI.DoVendorRecharge`, and
`SkillsAndPowersLine.setData`.
The same audit keeps `Examiner.HandleEvent`, `TinkerItem.HandleEvent`,
`VehicleRepair.HandleEvent`, `Carapace.Loosen`, `FixitSpray.HandleEvent`, and
`MagnetizedApplicator.HandleEvent` residual because current evidence only
proves subsets of those broader families.
The next exact producer audit closes `JournalScreen.HandleDelete`,
`Polygel.HandleEvent(InventoryActionEvent)`,
`ScriptCallToArms.ShowWarning`, and
`GameObjectFactory.HandleBlueprintXML` through existing owner patches, L2/L2G
coverage, and fixed literal dictionary evidence where needed. The same local
audit keeps broad or partially proven rows such as `Food.HandleEvent`,
`PopulationManager.WishGenerate`, `BiomeManager.DisplaySurfaceDistribution`,
and `SummoningCurio.HandleEvent` residual until exact owner or runtime evidence
proves the whole family.
The follow-up producer audit also closes exact existing single-callsite routes
for `DynamicQuestRewardElement_GameObject.award`, `IModification.WishModify`,
`CursedCellSocket.HandleEvent(CellChangedEvent)`, and
`NephalProperties.HandleEvent(BeforeDeathRemovalEvent)`, plus 10 exact
`SifrahPureOwnerPopupTranslationPatch` families for
`RealityDistortionSifrah`, `ReverseEngineeringSifrah`, and selected
ritual/social/tinkering Sifrah token `CheckTokenUse` methods.
The fixed-literal popup pass then closes exact generic dictionary-backed popup
routes for `RealityDistortionSifrah.CheckEarlyExit`,
`SifrahGame.CheckIncompleteTurn`, `SifrahGame.CheckEarlyExit`, the
toolkit/advanced toolkit/copper wire/hookah Sifrah token `CheckTokenUse`
methods, `ScriptCallToArms.spawnParties`, and
`QudSpecificBootHandlersModule.handleBootEvent`.
A focused description-assignment audit also closes `SavesAPI.ReadSaveJson`
because the existing `SavesApiReadSaveJsonTranslationPatch`, L2 test, and L2G
target-resolution check cover the exact save-size description route. Neighboring
description-assignment rows stay residual unless their exact owner methods have
equivalent evidence.
A read-only Sifrah audit then closes `SifrahGame.MakeMoveForSlot` because the
existing `SifrahPureOwnerPopupTranslationPatch`, L2 tests, and L2G
target-resolution evidence cover the exact chosen-correct, eliminated, and
disabled popup shapes. The same audit keeps constructor `Description` assignment
rows and neighboring token `UseToken`/early-exit rows residual because current
evidence covers popup/check routes or adjacent methods, not those exact
description or token-use owners. The audit also reviewed
`CyberneticsTerminal2.HackingResultPartialSuccess` and kept it residual: the
current `HackingSifrahResultTranslationPatch` targets other
`CyberneticsTerminal2.HackingResult*` methods, but not that exact partial-success
method.
A read-only top producer residual audit keeps `XRLGame.LoadGame`,
`Scores.Show`, `EndGame.PickState`, and
`PronounAndGenderSets.ShowPickGenderAndPronounSet` residual. Existing evidence
only covers narrower callsites such as missing-save or delete-confirmation
popups, or no exact owner patch exists for the method. The same audit confirms
`GeomagneticDisc.SignalFailure`, `SignalLowPower`, and `ExamineFailure` are
already covered, while `GeomagneticDisc.DoThrow`/`FireEvent` remain residual.
The follow-up single-callsite owner audit also closes
`PlayerMuralController.HandleEvent(EndTurnEvent)` because the existing
single-callsite owner patch, L2 tests, and L2G target resolution cover both
completion popup shapes in that exact method.
The next exact producer pass closes the two `CodeRedemptionManager.redeem*`
families by combining the existing CodeRedemption owner route for dynamic
download-error popups with the generic popup dictionary route for fixed
redemption literals. It also closes `Examiner.ResultCriticalFailure` and
`Quest.ShowFinishStepPopup` through their existing exact owner patches and
L2/L2G evidence. Broader neighboring families such as `XRLGame.LoadGame`,
`Scores.Show`, `Food.HandleEvent`, `Stomach.FireEvent`,
`PopulationManager.WishGenerate`, and `SummoningCurio.HandleEvent` remain
residual because the current evidence only proves subsets or different routes.
The following fixed/owner popup pass closes `XRLCore.SaveManagement` by
combining the existing old-save owner route with generic popup dictionary
coverage for delete/no-save literals. It also closes
`ConversationScript.IsMentalConversationPossible` through the same exact
ConversationScript owner patch already used for physical conversation failures.
The next low-count popup pass closes `KeybindsScreen.HandleMenuOption` through
the existing `KeyMappingUiTranslationPatch` owner route and L2/L2G evidence,
and closes the fixed `GameObject.CheckFrozen` and `MouseBlocker.OnPointerClick`
popup literals through the existing generic popup dictionary route.
The following existing-owner pass closes the five exact `GameObject` stat popup
methods (`GainSP`, `GainEgo`, `LoseEgo`, `GainIntelligence`, and
`GainWillpower`) through the already-present `GameObjectStatPopupTranslationPatch`
and L2/L2G evidence. `GameObject.ArePerceptibleHostilesNearby` remains residual
because the existing spot patch proves only the message-log route, not the
same family's `Popup.Show` branch.
The subsequent read-only combat audit closes the five exact
`ProselytizationSifrah.Result*` popup families and
`ConversationUI.CheckLost()` through already-present owner patches, L2 tests,
and L2G target resolution. The neighboring ProselytizationSifrah constructor
and check popup rows stay residual because those are separate family routes.
The first #781 implementation slice closes the exact
`ElementalJelly.SetupPod` and `Panhumor.SetupPod` direct
`Render.DisplayName` pseudopod overrides with L2 and L2G evidence.
The second #781 implementation slice closes the exact
`GasGeneration.SyncFromBlueprint` generated description route with L1, L2, and
L2G evidence for color-preserving gas display names and the fallback gaseous
burst text.
The third #781 implementation slice closes the exact activated-ability
registration/update owner routes for `Cloneling.Initialize`,
`Digging.Initialize`, `Engulfing.Initialize`, `FabricateFromSelf.Initialize`,
`RecoilAbility.Initialize`, `Run.SyncAbility(bool)`, `RunOver.Initialize`, and
`TrashRifling.Initialize` with L1, L2, and L2G evidence.
The fourth #781 implementation slice closes the remaining chargen direct UI
routes for `AttributeSelectionControl.Updated()` and
`QudSubtypeModuleWindow.BeforeShow(...)` with L2 and L2G evidence.
The #719 popup sink split pass also closes the exact
`Popup.GetPopupOption(...)` menu-item helper and `Popup.PickSeveral(...)`
selection-list owner route. `PickSeveral` now has focused L2 evidence for the
generated selection-limit popup and the Accept/Select All/Deselect All button
handoff through `Popup.PickOption`, including the markup-preservation fix for
whole-wrapper hotkey labels.
The Mod management caller-owner pass closes `ModInfo.ConfirmDependencies`,
`ModInfo.ConfirmUpdate`, and `ModScrollerOne.OnActivate` using the existing
owner transpilers plus L1/L2/L2G evidence. `ModInfo.ConfirmFailure` and
`ModManagerUI.OnCancel` remain residual because they do not yet have exact
owner-route evidence.
The next producer-owner pass closes `ZoneManager.GenerateZone`,
`KeyMappingUI.Show`, `TradeUI.PerformOffer`, and
`SpiralBorerCurio.HandleEvent`. `GenerateZone` now covers both the
build-failure queue route and the force-stop/report-issue popups through the
exact owner patch; `KeyMappingUI.Show` and `TradeUI.PerformOffer` reuse their
existing owner-patch and L2/L2G evidence. `SpiralBorerCurio.HandleEvent` now
uses the single-callsite owner popup route and the existing `world-parts`
dictionary leaf.
The current exact owner audit closes `GameObject.CheckCompanionDirection`,
`DeployableInfrastructure.DeployOne`, the five exact
`BeguilingSifrah.Result*` popup methods, the four exact targeted
`ItemModdingSifrah.Result*` methods, and
`RebukingSifrah.ResultCriticalFailure/ResultPartialSuccess` through existing
patch, L2, L2G, and decompiled-source evidence. `RebukingSifrah.ResultFailure`,
`GiantClamProperties.TeleportFromClamWorld`,
`ElectricalGeneration.PerformDischarge`, and
`WaterRitualRandomMutation.HandleEvent(EnteredElementEvent)` remain residual
because the existing owner evidence is partial or method-adjacent.
The Telekinesis pass closes `Telekinesis.HandleEvent(InventoryActionEvent)`,
`Telekinesis.Activate(bool)`, and `Telekinesis.AttemptTelekinesis()` by covering
the not-budge, exhausted-psyche, and no-target popups through the exact owner
patch. The same pass also promotes the existing single-callsite
`DestroyOnUnequip.HandleEvent(BeginBeingUnequippedEvent)` confirmation popup
evidence into the #719 policy overlay.
The latest L2G residual cross-check closes
`Enclosing.ExitEnclosure(GameObject,IEvent,Enclosed)` because the existing
`EnclosingTranslationPatch`, L2 owner-route tests, L2G target resolution, and
fixed popup dictionary coverage together cover the exact popup and queued-message
branches. The same cross-check keeps `RepellingForce.FireEvent`,
`Switch.FireEvent`, `WaterRitualRandomMutation.HandleEvent`,
`PaxInfectLimb.Infect`, `ItemNaming.NameItem`,
`ElectricalGeneration.PerformDischarge`, `BiomeManager.DisplaySurfaceDistribution`,
`Tonic.HandleEvent(ExamineCriticalFailureEvent)`,
`CursedCellSocket.HandleEvent(CellDepletedEvent)`, and
`GiantClamProperties.TeleportFromClamWorld`
residual because their evidence is partial,
method-adjacent, sink-only, generated/mixed, or missing for the whole family.
A follow-up check of the L2G residuals not explicitly named in the report keeps
`Mutations.WishMutation`, `SocialSifrahTokenGift/Item.CheckTokenUse`,
`ElevatorSwitch.FireEvent`, `ThiefBot.FireEvent`, and `SunderMind.Tick`
residual. `Mutations.WishMutation` has existing evidence only for the
`Did you mean ...?` confirmation, while the not-found popups are intentionally
not claimed by that owner patch. `SocialSifrahTokenGift/Item.CheckTokenUse`
have exact owner tests for dynamic item-count messages, but the fixed
`of that kind of item` branch is explicitly deferred by the existing L2 tests,
so the family is not whole-route closed. `ElevatorSwitch.FireEvent`,
`ThiefBot.FireEvent`, and `SunderMind.Tick` have exact owner target resolution
for selected queued-message branches, but each family still contains popup,
generated, or explicitly unchanged branches outside the existing proof.
The fixed-literal popup pass closes `BurrowingClaws.CheckDig`,
`MainMenu.Quit`, and `KeybindsScreen.Exit` through the generic popup dictionary
route. These are stable source literals with L2 sink coverage and dictionary
evidence, not new owner patches.
The next producer popup pass closes the fixed literals in `TutorialStep`,
`GolemQuestMound`, `PaxKlanqIPresumeSystem`, `AmbientStabilization`, and
`Teleprojector.EndDomination` through the existing popup dictionary routes. It
also promotes existing exact owner/template evidence for
`TradeScreen.HandleTradeSome`, `ActivatedAbilityEntry.TrySendCommandEventOnPlayer`,
and `Fetches.HandleEvent(AIBoredEvent)`.
The latest fixed-literal popup pass closes `AscensionSystem.HandleEvent` for
`EndTurnEvent`, `AfterConversationEvent`, and `GenericQueryEvent`,
`GolemQuestSelection.WishFinishGolem`, and `Cloneling.FireEvent` through exact
existing dictionary entries served by the generic popup route. `Cloneling.FireEvent`
uses the existing `world-parts` leaf; the Ascension and Golem quest prompts use
`ui-popup`. `PsychicHunterSystem.CheckPsychicHunters` remains residual even
though its exact leaf exists, because existing tests do not prove that exact
`Messaging.EmitMessage` literal contract.
The PopupMessage delete-save pass also closes `MainMenu.HandleDelete` and
`SaveManagement.HandleDelete` through the existing PopupMessage field
translation route, whose L2 tests already prove the generated save-name
message/title templates and whose dictionaries contain the delete and
completion strings.
The latest fixed-popup/effect pass closes `Exhausted.HandleEvent(BeginTakeActionEvent)`,
`Exhausted.FireEvent`, `Lost.Remove`, and `Glotrot.AskPulldown` through exact
existing `ui-popup` leaves and the generic `Popup.Show` / `ShowFail` /
`ShowSpace` / `ShowYesNo` routes. The same existing-owner pass closes
`QudCustomizeCharacterModuleWindow.GetSelections`, `SelectMenuOption`,
`OnChooseGenderAsync`, `OnChoosePronounSetAsync`, and `OnChoosePet` through
`CharGenCustomizeTranslationPatch`, its L2 route tests, and L2G state-machine
resolution evidence. Adjacent chargen methods such as `GetPets` and
`QudMutationsModuleWindow.SelectVariant` remain residual because dictionary
presence alone does not prove the exact owner route.
The low-count active-effect message-frame pass closes `Stun.Apply` and
`Stun.HandleEvent(IsConversationallyResponsiveEvent)` through existing
MessageFrame/Does route tests and dictionary evidence. It keeps
`Stun.HandleEvent(BeginTakeActionEvent)`, `Scintillating.Apply`, and
neighboring effect message families residual where
the current evidence is dictionary-only, generated by a variable message name,
or lacks exact route tests.
The follow-up UI direct-text audit closes `Look.SetupItemTooltipAsync` and
`Look.ShowItemTooltipAsync` because both assign tooltip `BodyText` from the
already patched and tested `Look.GenerateTooltipContent(GameObject)` owner
route. It keeps `WorldGenerationScreen._ShowWorldGenerationScreen`,
`TradeScreen.HandleHighlightObject`, `PopupMessage.Update`, and broad
`Popup.New/WaitNewPopupMessage` wrappers residual because their existing
evidence is partial, observation-only, or generic sink handoff coverage.
The generic fixed MessageFrame pass closes ten additional low-count producer
families through the existing `XDidY` route: `BreakableInMelee.HandleEvent`,
`ExistenceSupport.Unsupported`, `HologramProjector.Enable/Disable`,
`Slumberling.CheckHibernate`, `Temporary.Expire`, `SpiralIron.PressSpiralIron`,
`Capacitor.HandleEvent(BeforeDeathRemovalEvent)`, `LightDimmer.Tick`, and
`ModQuantumReverb.PlaceHologram`. Each inspected producer has a fixed
`DidX(...)` verb/extra shape and a concrete `MessageFrames/verbs.ja.json`
entry. Adjacent or generated routes such as `QuantumFugue.Cohere` and
`Examiner.MakeUnderstood` remain residual; `MechanicalWings.FireEvent` is now
closed by the tranche 35 owner-route patch.
Nearby broad, mixed, or unproven rows such as `Asleep.FireEvent`,
`Stuck.FireEvent`,
`AmbientRealityStabilized.HandleEvent`, `Healing.Apply`, `XRLGame.LoadGame`,
`Scores.Show`, and
`PronounAndGenderSets.ShowPickGenderAndPronounSet` remain in the residual
ledger because current evidence is broad, callsite-only, incomplete, or
otherwise not method-exact.
A follow-up active-effect audit previously kept `EmptyTheClips.Apply` and
`BasiliskPoison.FireEvent` residual even though
`EffectStaticMessageTranslationPatch` targets those exact methods: the existing
patch/test evidence covers their fixed `AddPlayerMessage` strings, while each
family also contains `DidX`/MessageFrame constructions that are not proven by
that static queue patch. The producer top-20 audit similarly promotes no
additional family; every inspected row is partial, method-adjacent, generated,
or missing an exact owner-route proof.
The next existing-patch fixed-popup audit closes `ArkCore.StartEnd`,
`QudChartypeModule.selectType`, and `MainMenu.SelectedInfo`: the first and third
use exact fixed `Popup.Show` / `ShowAsync` leaves already in `ui-popup`, and
`MainMenu.SelectedInfo` uses the existing `PopupAskString` route for the fixed
`Redeem a Code` prompt. The same audit keeps `TinkeringHelpers.CheckMakersMark`,
`CommandBindingManager.RestoreDefaults`, `SavesAPI.FatalSaveError`,
`QudBuildLibraryModuleWindow.*`, `ReshephsCrypt.FireEvent`,
`SapOnPenetration.FireEvent`, `GritGateTerminalScreenRoot.UpdatePowerOptions`,
`Crayons.HandleEvent`, `Description.HandleEvent`, `Inventory.HandleEvent`,
`TradeUI.ShowVendorActions`, `XRLCore.RestoreModsLoadedAsync`, and
`ModInfo.ConfirmFailure` residual because the evidence is dictionary-only,
partial, generated/mixed, or tied to adjacent owner routes.
The follow-up mutation/combat producer audit closes `SunderMind.FireEvent`
through exact fixed `Popup.ShowYesNo` / `ShowFail` literals already present in
`ui-popup` and served by the existing generic popup route. It keeps
`Pettable.Pet`, `Food.HandleEvent`, both audited `Stomach` methods,
`CheckpointingSystem.ShowDeathMessage`, `QuickenMind.Activate`,
`MagazineAmmoLoader.FireEvent`, `ShevaStarshipControl.CheckTimer`,
`GeomagneticDisc.DoThrow`, `SunderMind.Blast`, `Domination.ProcessTarget`,
`StickyTongue.HarpoonNearest`, `StunningForce.Concussion`,
`TemporalFugue.PerformTemporalFugue`, and
`AutomatedExternalDefibrillator.AttemptDefibrillate` residual because the
existing evidence is partial, generated/mixed, dictionary-only, or tied to
adjacent methods.
The #719 residual status tranche removes the final 758 raw `unreviewed` rows
from the policy output without claiming owner-route coverage for them. Rows
whose route cannot be proven statically are now `runtime_required`; rows that
still need exact owner implementation, promotion evidence, or narrower
subsystem splits are now `action_required`. Residual bucket and follow-up JSON
continue to report all 764 open rows under the single consolidated #719 tracker
instead of emitting child issue work.
After this status tranche, `just check` passes. The first run exposed stale C#
color-route catalog/allowlist entries for existing owner patches; those lists
were synchronized and the rerun completed the C# build, 9,158 C# tests, Ruff,
1,407 Python tests plus 1 skip, localization validators, markdown report check,
translation-token check, and localization coverage-map checks successfully.
The latest fixed popup-only pass also closes `Switch.FireEvent`,
`StairsUp.FireEvent`, `CherubimLock.Chime`, and `TeleportOnEat.FireEvent`.
Each audited method exposes only fixed `Popup.Show` literals on the
player-visible surface, and those leaves are already present in `ui-popup` or
`world-parts` and served by the existing generic popup route. The same pass
keeps `VehicleRepair.HandleEvent`, `TinkerItem.HandleEvent`,
`Examiner.HandleEvent`, `PlayerDanceRitual.FireEvent`, `Leveler.LevelUp`, and
`AjiConch.ActivateAjiConch` residual because their current evidence is partial
or tied to adjacent helper methods.
The next fixed popup-only pass closes `DynamicQuestsGameState.FindQuestTarget`,
`ModDisguise.FireEvent`, and `PsionicMigraines.FireEvent` through exact fixed
popup leaves. It keeps `DynamicQuestRewardElement_ChoiceFromPopulation.award`
and `GripChange.TryChooseGrip` residual because their popup options are
generated reward/object or skill display names rather than a fixed literal-only
family.
The latest existing-patch popup pass closes `FrostWebs.FireEvent`,
`Cell.LogInvalidPhysics`, `GasDisease.ApplyDisease`, `Skittish.LoseControl`,
`TimeCube.Activate`, `TerrainTravelFungal.FireEvent`, `XRLGame.SaveGameError`,
`CyclopeanPrism.PtohAnnoyed`, `Domination.BreakDomination`, and
`TimeCubed.Apply` through exact fixed popup leaves or the existing out-of-range
popup template route. It keeps `Spinnerets.FireEvent`,
`Burrowing.HandleEvent`, `Vehicle.HandleEvent`, and the audited UI option
popups residual because their current evidence is message-log scoped,
generated/mixed, or adjacent-screen-only rather than exact popup owner closure.
A subsequent existing-patch-only pass closes `StickyTongue.HandleEvent`,
`CyberneticsCustomVisage.ApplyVisage`, `SummoningCurio.HandleEvent(InventoryActionEvent)`,
`CrungleGaze.FireLine`, `Psychometry.HandleEvent(GetTinkeringBonusEvent)`, and
`Skills.WishSkill(string)`. The same audit keeps `ElectricalGeneration.PerformDischarge`,
`CyberneticsCathedra.HandleEvent`, `MechanicalWings.FireEvent`,
`IGrenade.HandleEvent`, `RecoilAbility.HandleEvent`,
`NeutronFluxContainment.HandleEvent(BeginTakeActionEvent)`, `Wings.HandleEvent`,
`FrameworkSearchInput.ChangeValue`, and `OptionsScreen.HandleMenuOption`
residual because the current evidence is partial, generated/mixed, or belongs
to an adjacent route rather than the exact family.
The latest existing-patch-only pass closes `AmbientRealityStabilized.HandleEvent`,
fixed tonic popups from `Blaze_Tonic.HandleEvent` and the tonic `Remove`
methods, `PaxInfectLimb.Infect`, `WaterRitualLearnSkill.HandleEvent`,
`TinkerData.DataDisk`, `FearAura.HandleEvent`, `BlastOnHit.Detonate`, and
`CryptSitterBehavior.Alert/Unalert`. It keeps adjacent or generated/captured
routes such as `Examiner.MakeUnderstood`, `CursedCellSocket.HandleEvent(CellDepletedEvent)`,
`DesalinationPellet.HandleEvent`, `QuantumFugue.Cohere`, and
`Interdiction.BeginInterdiction` residual until exact owner evidence is added.
A subsequent read-only subagent audit closes existing fixed MessageFrame routes
for `ExplodeOnHit.Detonate`, `FusionReactor.Explode`, `ShattersOnHit.Shatter`,
`SunderGrenade.DoDetonate`, `DeploymentGrenade.DoDetonate`, and
`ChargeUsedEvent.Send`, plus the fixed Does/ShowFailure branch in
`ForceProjector.ForceProjectorDeactivate`. It also closes the fixed
`LiquidSludge.ObjectGoingProne` EmitMessage pattern and seven active-effect
message families: `EmptyTheClips.Remove`, `Immobilized.EndImmobilization`,
`Rebuked.Remove`, `Scintillating.Remove`, `ShadeOil_Tonic.Remove`,
`Terrified.Attack`, and `Bleeding.StopMessage`. The same audit keeps
`CyberneticsTerminal2.AttemptInterface`, `TemplarPhylactery.HandleEvent(GetShortDescriptionEvent)`,
`GameObjectBaetylUnit.GiveRewards`, and then-unproven active-effect message rows
residual because the current evidence was property-backed, generated/captured,
description-sink-adjacent, or missing exact MessageFrame proof for the whole family.
The next existing-patch-only pass closes 12 fixed tonic popup families:
`Blaze_Tonic.Apply/ApplyOverdose`, `Hoarshroom_Tonic.Apply/ApplyOverdose`,
`HulkHoney_Tonic.Apply/ApplyAllergy`, `LoveTonic.Apply`,
`Rubbergum_Tonic.Apply/ApplyAllergy`, `Salve_Tonic.Apply`,
`Skulk_Tonic.Apply`, and `Ubernostrum_Tonic.Apply`. These all expose fixed
`Popup.Show` strings already present in `world-effects-tonics.ja.json` or
`ui-popup.ja.json` and served by the existing generic popup route. The same
subagent audit kept no tonic rows in that slice. A separate producer audit
closes `DustAnUrnGoal.MoveToAndDustUrn`,
`GiveATreatToPartyLeader.TakeAction`, and
`IDelayedLineMutation.HandleEvent(CommandEvent)` through the existing fixed
MessageFrame route. It keeps `Interdiction.BeginInterdiction`,
`AIWiring.HandleEvent(IsConversationallyResponsiveEvent)`,
`CursedCellSocket.HandleEvent(CellDepletedEvent)`,
`Examiner.MakePartiallyUnderstood`, `ModExtradimensional.MakeExtradimensional`,
`Extensions.ShowSuccess`, `MessageQueue.AddPlayerMessage`, and
`PopulationManager.RollOneFrom` residual because they are partial, adjacent,
sink/helper, wish/debug, or generated/dynamic routes.
The follow-up producer MessageFrame audit closes
`Dystechnia.CauseExplosion`, `IrisdualBeam.Refract`,
`SpontaneousCombustion.TurnTick`, `EMPGrenade.DoDetonate`, and
`HEGrenade.DoDetonate` through the same existing fixed `XDidY` / `XDidYToZ`
MessageFrame route and verb dictionary evidence. The active-effect popup/queue
audit kept its audited rows residual, and this producer audit also keeps
`SpiderWebs.HandleEvent`, `AIBarathrumShuttle.ActionShipLaunch`,
`BaetylHostility.CheckBaetylHostility`, `Banner.HandleEvent`,
`CyberneticsCathedraBlackOpal.Activate`, and
`CyberneticsCathedraWhiteOpal.Activate` residual because the current evidence is
dictionary-only, method-adjacent, generated/mixed, or missing exact
MessageFrame proof.
The next MessageFrame-only audit closes `ThermalGrenade.DoDetonate`,
`PhaseGrenade.DoDetonate`, `CrumblesOnHit.FireEvent`,
`TemperatureVenting.Trigger`, `FactionRank.PromoteIfBelow`, and
`GenericInventoryRestocker.PerformStock` through the existing `XDidY` /
`Messaging.XDidY` route, `MessageFrameTranslator` pattern tests, and exact
`MessageFrames/verbs.ja.json` entries. It keeps `Fan.TurnTick`,
`PluckablePolyp.Pluck`, `PlaceTurretGoal.TakeAction`, and
`HookOnMissileHit.FireEvent` residual because their current evidence is
missing exact dictionary keys, uses an unpatched `DidXToY` shape, or contains
generated/mixed extras.
The follow-up low-count MessageFrame audit closes `Kindle.FireEvent`,
`ElectricalGeneration.DischargeMessage`, `Forcefield.HandleEvent`,
`ForcefieldMaterial.HandleEvent`, `GasGrenade.DoDetonate`,
`Hidden.RevealInternal`, `LavaSludge.CheckTemperature`, and
`Shrine.PrayAtShrine` through exact existing `XDidY` / `XDidYToZ`
MessageFrame route evidence. It keeps `LayMineGoal.TakeAction` residual despite
its `place` verb dictionary entry because the producer uses the unpatched
`DidXToY` shape. It also keeps `BurgeonOnHit.FireEvent`,
`BurnOffGas.FireEvent`, `ExtradimensionalHunterSummoner.Summon`,
`GrabberArm.FireEvent`, `Ironshroom.FireEvent`,
`FeelingOnTarget.FireEvent`, `LavaSludge.HandleEvent(BeforeDieEvent)`,
`LiquidVolume.CleaningMessage`, `NoStandUp.HandleEvent`, `Burgeoning.Burgeon`,
and `AmbientPowerReceiver.HandleEvent(EnteringZoneEvent)` residual because
current proof is partial, generated/mixed, missing exact keys, or mixed with
popup/death-reason surfaces.
The next fixed MessageFrame sweep closes `Decarbonizer.HandleEvent`,
`FrostWebs.FrostWeb`, `CloneOnHit.FireEvent`,
`FlashbangGrenade.DoDetonate`, `RocketSkates.EmitFlamePlume`,
`LiquidWarmStatic.ApplyRandomEffectTo`, `GravityGrenade.DoDetonate`,
`TimeDilationGrenade.DoDetonate`, `Hidden.HideInternal`, and
`ExplodeAfterTurns.Detonate` through existing fixed `DidX` / `XDidY`
route coverage and exact verb dictionary entries. It keeps `DropOnDamage`,
`Sweeper`, and the `PetPhylactery` / `TemplarPhylactery` spawn rows residual
because their proof depends on unpatched `DidXToY` or generated wraith object
families rather than fixed whole-family MessageFrame evidence.

The latest low-count MessageFrame pass closes `ElectromagneticPulse.FireEvent`,
`IrisdualBeam.HandleEvent`, `Narcolepsy.HandleEvent`,
`RepellingForce.FireEvent`, `BubbleLevel.FlipBubbleLevel`,
`EjectionSlot.LockSeats`, `HolographicIvory.HandleEvent`,
`PetPhylactery.Despawn`, `SoupSludge.ReactWith`,
`SpaceTimeVortex.HandleEvent(RealityStabilizeEvent)`, and
`DisperseEMP.HandleEvent(BeginTakeActionEvent)` through existing `XDidY` /
`XDidYToZ` route evidence and exact dictionary entries. It keeps
`ReflectShame.Shame`, `DiThermoBeam.FlipBeam`, `StickyOnHit.Entangle`,
`EelSpawn.Reveal`, `EjectionSeat.Message`, and arbitrary `DidXToY` rows
residual because current proof is blocked by normalization uncertainty, missing
exact keys, generated direction or object text, or unpatched call shapes.

The follow-up MessageFrame/Does audit closes `ModPsionic.FireEvent`,
`NeutronFluxContainment.CheckExplosion`, `Rummager.CheckPickUp`,
`StrideMason.ExamineFailure`, `TemplarPhylactery.Despawn`, `TrollKing.Spawn`,
and `MagazineAmmoLoader.HandleEvent(CheckLoadAmmoEvent)`. The MessageFrame
promotions are exact `DidX` / `XDidYToZ` shapes that reach the existing
`XDidYTranslationPatch` route with concrete `MessageFrames` entries; the
MagazineAmmoLoader row is a fixed `Does(...)` message family covered by the
existing Does marker and message-log pattern route. It keeps
`CooldownOnStep.HandleEvent`, `HeatSelfOnFreeze.FireEvent`,
`NeutronFluxContainment.GetWarningMessage`, `PsychicMeridian.AfflictNosebleed`,
`EnergyAmmoLoader.GetStatusMessage`, `LootOnStep.SteppedOn`,
`ModLiquidCooled.GetStatusMessage`, `Tonic.HandleEvent`, and
`ConversationDelegates.AwardXP` residual because their current evidence is
partial, active-part-status dependent, method-adjacent, generated/captured, or
outside the proven route target set.
The latest fixed popup/EmitMessage audit closes `RefreshCooldownsOnEat.FireEvent`,
`CherubimLock.FireEvent`, `MagazineAmmoLoader.Load`, and
`ModManagerUI.PromptScripting`. The first three use existing
`GameObjectEmitMessageTranslationPatch` / message-log pattern evidence; the
`ModManagerUI` row uses the existing `PopupMessageTranslationPatch` field route
and `ui-modpage` dictionary leaves. The same popup audit keeps
`ModManagerUI.OnCancel`, `CommandBindingManager.RestoreDefaults`,
`ArkCore.TryOpen`, `ShevaStarshipControl.AttemptLaunch`,
`CyberneticsCathedra.HandleEvent`,
`CyberneticsOnboardRecoilerTeleporter.ActuateTeleport`, `IGrenade.HandleEvent`,
and mutation popup rows such as `Wings.HandleEvent`,
`BaseMutation.SelectVariant`, `ElectricalGeneration.PerformDischarge`,
`Spinnerets.FireEvent`, `Burrowing.HandleEvent`, `Phasing.FireEvent`,
`SpacetimeVortex.FireEvent`, and `LifeDrain.FireEvent` residual because their
current proof is dictionary-only, generic-sink, adjacent-route, generated/mixed,
or otherwise not method-exact whole-family evidence.
The follow-up subagent-assisted EmitMessage/Does audit closes
`LiquidGoo.ObjectGoingProne`, `EquipStatBoost.ExamineFailure`,
`HelpingHands.ExamineFailure`, `Door.AttemptClose`,
`ChevronWall.HandleEvent`, `HexCrystal.HandleEvent`, and
`ForceProjector.ForceProjectorActivate`. These rows use existing
`GameObjectEmitMessageTranslationPatch` pattern evidence, `Does` route evidence,
and exact dictionary/test coverage for the inspected source shapes. The same
audit keeps `CooldownAmmoLoader.GetCoolingDownMessage` residual because current
evidence proves dictionary shapes but not the exact consumer route for the
returned event messages. A parallel popup audit keeps `CodaSystem.EndGamePrompt`,
`GolemQuestMound.DisplayOptions`, `Gender.CustomizeProcess`,
`CyberneticsTerminal2.AskLowLevelHack`, `ModDisguise.BeingAppliedBy`,
`QudBuildLibraryModuleWindow.AddBuild`, `FactionsStatusScreen.HandleCmdOptions`,
and `InventoryAndEquipmentStatusScreen.HandleShowOptions` residual because
existing evidence is dictionary-only, generic-sink, adjacent owner, or targets a
different method.
The latest subagent-assisted exact-route audit closes
`LiquidOoze.ObjectGoingProne`, `DecoyHologramEmitter.DestroyHolograms`,
`Combat.PerformMeleeAttack`, and
`MagazineAmmoLoader.HandleEvent(CommandReloadEvent)` through existing
`GameObjectEmitMessageTranslationPatch` / DoesVerb route evidence and exact
message-pattern tests. It keeps `LiquidWarmStatic` wish/helper rows,
`ForceBubble.CreateBubble`, `Skills.WishSkillAll`, `Skills.WishSkillAdd`,
active-part `GetStatusMessage` rows, `PoweredFloating.FireEvent`, and
`MagazineAmmoLoader.HandleEvent(LoadAmmoEvent)` residual because current
evidence is dictionary-only, adjacent-method owner coverage, or lacks
method-exact proof that the return/event message reaches the patched route.
The follow-up tonic fixed-popup audit closes
`Skulk_Tonic.ApplyOverdose` and `SphynxSalt_Tonic.ApplyOverdose` through the
same existing generic popup dictionary route used for the already closed tonic
`Apply`, `ApplyAllergy`, `ApplyOverdose`, and `Remove` rows. Their fixed
`Popup.Show` overdose strings are present in `world-effects-tonics.ja.json`.
The active-effect fixed-popup follow-up also closes
`HulkHoney_Tonic.FireEvent`, `LoveTonic.FireEvent`,
`Rubbergum_Tonic.FireEvent`, `Salve_Tonic.FireEvent`,
`Ubernostrum_Tonic.FireEvent`, and `ShadeOil_Tonic.ApplyOverdose` through the
same tonic dictionary route. It closes `Glotrot.FireEvent`, `Famished.Apply`,
`WakingDream.Award`, and `Paralyzed.FireEvent` through the existing generic
fixed popup dictionary route, and closes
`CookingDomainRubber_ExtraJump.FireEvent` /
`CookingDomainRubber_Extra2Jumps.FireEvent` through
`CookingRuntimeTranslationPatch`. The same audit keeps
`ShadeOil_Tonic.FireEvent`, `BrainBrineCurse.FireEvent`, and
`SphynxSalt_Tonic.Apply` were not claimed by that exact fixed-popup proof then,
but are now closed by the later scoped active-effect popup/queue owner route.
`FungalSporeInfection.ChooseLimbForInfection` remains residual because it mixes
`Popup.Show` and picker-route text outside this tranche.
The active-effect queue follow-up closes `FungalCureQueasy.Apply`,
`IrisdualCallow.Remove`, and `Luminous.Remove` through the existing
`GameObjectEmitMessageTranslationPatch` / `MessagePatternTranslator` route and
message-pattern tests. The first implementation pass after that audit closes
`Ill.Remove`, and the later scoped active-effect popup/queue tranche closes
`IrisdualCallow.Apply` plus
`CookingDomainTongue_ThreeTongues_ProceduralCookingTriggeredAction.Apply`.
Together these passes empty the `active_effect_queue_route_split` bucket.
The preset cooking recipe description implementation slice closes all ten
`cooking_description_route_split` rows by extending `CookingEffectTranslationPatch`
to exact `XRL.World.Skills.Cooking.*.GetDescription()` owners. Focused L1/L2/L2G
evidence covers Apple Matz, Bone Babka, Cloaca Surprise, Crystal Delight,
Goat in Sweet Leaf, Hot and Spiny, Mah Lah Soup, Mulled Mushroom Cider,
The Porridge, and Tongue and Cheek descriptions. A separate UI audit kept the
inspected popup sink and menu/screen rows residual; static dictionary leaves or
adjacent `Show()`/`QueryKeybinds()` patches do not close `UpdateMenuBars()`
families that construct fresh `navigate` / `select` menu options.
The small action/effect description-return slice closes `Kill.GetDetails()`,
`Disassembly.GetDescription()`, `OngoingAction.GetDescription()`,
`Metamorphed.GetDetails()`, and `IStingerProperties.GetDescription()` through
`ActionEffectDescriptionReturnTranslationPatch`, with focused L1/L2/L2G
evidence for exact fixed returns and the stinger adjective pattern. `AutoAct.GetDescription(...)`
remains in `action_description_runtime` because it still has multiple generated
and owner-dependent description shapes.
The description-detail return slice closes
`CyberneticsChoice.GetDescription()/GetLongDescription()` and
`TinkerData.Description/UnclippedDescription` through
`DescriptionDetailReturnTranslationPatch`, with focused L1/L2/L2G evidence for
cybernetics slot labels, default license text, cybernetics behavior rules, and
tinker-data batch description frames.
The follow-up game-object unit description tranche extends that owner route to
`GameObjectCyberneticsUnit.GetDescription`, `GameObjectSkillUnit.GetDescription`,
`GameObjectRelicUnit.GetDescription`, `GameObjectGolemQuestRandomUnit.GetDescription`,
`GameObjectMetachromeUnit.GetDescription`, `GameObjectBodyPartUnit.GetDescription`,
`GameObjectExperienceUnit.GetDescription`, and `GameObjectMutationUnit.GetDescription`.
It translates fixed unit description frames for cybernetic installs, skill
grants, relic tiers, random effects, metachrome equipment, extra body-part
slots, experience/level grants, and mutation-level labels, removing 8 rows and
28 text constructions from `game_object_unit_description_runtime`.
The final game-object unit description tranche closes
`GameObjectBaetylUnit.GetDescription`, `GameObjectCloneUnit.GetDescription`,
`GameObjectReputationUnit.GetDescription`, `GameObjectSecretUnit.GetDescription`,
base `GameObjectUnit.GetDescription`, and
`GameObjectUnitAggregate.GetDescription`. The same description-detail owner
patch now covers baetyl reward counts, nearby clone text, reputation grant
frames, secret reveal counts, empty base descriptions, and aggregate provided
descriptions, removing the final 6 rows and 6 text constructions from
`game_object_unit_description_runtime`.
The parallel read-only producer and active-effect audits keep inspected
low-count residuals such as `Extensions.ShowSuccess`,
`MessageQueue.AddPlayerMessage`, `DecoyHologramEmitter.PlaceHologram`,
`RocketSkates.HandleEvent(JumpedEvent)`,
`VehicleMeleeInfiltration.HandleEvent(CanEnterInteriorEvent)`,
`PsychicHunterSystem.CheckPsychicHunters`, and `Burrowed.Emerge` because current
evidence is generic-sink, adjacent-method, property-backed, dictionary-only, or
partial mixed-surface coverage rather than exact whole-family owner-route proof.
Later active-effect tranches promoted `Ill.Remove`, `Sitting.StandUp`,
`Frenzied.TriggerBerserk`, and `Beguiled.Remove` with exact owner-route tests, so
they are no longer part of the residual ledger.

## Closed Lanes

These lanes have no `unreviewed` rows in the current policy output:

| Lane | Current status |
| --- | --- |
| `conversation_routes` | Covered by the Issue #719 conversation report and direct conversation overlays. |
| `history_generated_text` | Covered or deferred by the Issue #737 HSE audit and Issue #747 static inventory. |
| `journal_quest_routes` | Covered by the Issue #747 journal/quest static inventory. |
| `activated_ability_names` | Covered by exact mutation, skill, and provider activated-ability owner routes. |

## Remaining Lanes

| Lane | Unreviewed rows | Text constructions | Dominant surface shape |
| --- | ---: | ---: | --- |
| `producer_message_popup` | 159 | 1,672 | `Popup`, `AddPlayerMessage`, tutorial popup routes |
| `combat_message_frame_does` | 163 | 1,724 | `MessageFrame`, `Does`, mixed popup/message routes |
| `description_effect_detail` | 182 | 659 | Sifrah detail text, non-effect description routes |
| `display_name_composition` | 227 | 535 | `DisplayNameAssignment`, generated names |
| `screen_ui_direct_text` | 28 | 95 | UI `SetText` and popup sink internals |

## Residual Buckets

Every remaining unreviewed row is assigned to a follow-up bucket by
`--format residual-buckets-json`.

| Bucket | Disposition | Rows | Text constructions | Lanes |
| --- | --- | ---: | ---: | --- |
| `producer_message_family_audit` | child issue needed | 257 | 2,475 | combat 154, popup 103 |
| `generated_display_name_runtime` | runtime evidence required | 212 | 336 | display name 212 |
| `sifrah_description_route_split` | child issue needed | 151 | 568 | description 151 |
| `sifrah_popup_route_split` | child issue needed | 29 | 34 | popup 29 |
| `ui_description_assignment_runtime` | runtime evidence required | 30 | 82 | description 30 |
| `ui_screen_route_runtime` | runtime evidence required | 26 | 69 | screen UI 26 |
| `producer_broad_route_split` | child issue needed | 10 | 274 | combat 4, popup 6 |
| `tutorial_popup_runtime` | runtime evidence required | 20 | 445 | popup 20 |
| `generated_display_name_child_issue` | child issue needed | 14 | 184 | display name 14 |
| `action_description_runtime` | runtime evidence required | 1 | 9 | description 1 |
| `ui_popup_sink_route_split` | child issue needed | 2 | 26 | screen UI 2 |
| `producer_runtime_evidence_required` | runtime evidence required | 4 | 159 | combat 4 |
| `active_effect_popup_route_split` | child issue needed | 1 | 4 | popup 1 |
| `world_zone_display_name_runtime` | runtime evidence required | 1 | 15 | display name 1 |

A parallel generated display-name audit promotes no additional family. The
`generated_display_name_child_issue` bucket stays residual because village,
mural, signature dish/item, and dynamic quest reward rows only have adjacent
conversation/wall/item-name mutation evidence, not exact owner/member coverage
for the child display-name producers. The inspected
`generated_display_name_runtime` rows also stay residual: existing
`GetDisplayNameEvent.ProcessFor` and cooking recipe display-name coverage are
generic sink or base-method evidence rather than exact proof for owners such as
`TemporalFugue.CreateFugueCopyOf`, `GetRunningBehaviorEvent.Retrieve`,
`Miner.SetupMinerConfiguration`, `RandomFigurine.HandleEvent`, terrain display
assignments, object-factory rows, or individual cooking recipe overrides.

The first activated ability asset-bridge implementation tranche closes five
exact mutation `Mutate(GameObject,int)` owners:
`WillForce`, `BurrowingClaws`, `ElectricalGeneration`, `LightManipulation`, and
`Precognition`. The new `MutationActivatedAbilityNameTranslationPatch`
postfix owner-scans the registered `*ActivatedAbilityID` entries after
mutation registration and routes their `DisplayName` values through
`ActivatedAbilityNameTranslator`. This removes 5 rows and 48 text constructions
from `activated_ability_asset_bridge`. The remaining rows are still actual
`Mutate`, `AddSkill`, and `AddAbility` owners constructing
`AddMyActivatedAbility(...)` names rather than display-sink behavior.
The second activated-ability tranche closes
`Tinkering_LayMine.AddSkill`, `SlogGlands.Mutate`,
`Pistol_EmptyTheClips.AddSkill`, `Tinkering_Tinker1.AddSkill`, and
`Beguiling.Mutate`. It reuses the same registration-name owner helper, adds the
skill-side `SkillActivatedAbilityNameTranslationPatch`, and removes another 5
rows and 33 text constructions from `activated_ability_asset_bridge`.
The third activated-ability tranche closes five more skill owners:
`Axe_Decapitate.AddAbility`, `Axe_Dismember.AddSkill`,
`Axe_HookAndDrag.AddSkill`, `CookingAndGathering_Harvestry.AddSkill`, and
`LongBladesDuelingStance.AddSkill`. It extends the skill-side patch to handle
both `AddSkill(GameObject)` and no-argument `AddAbility()` registrations,
removing another 5 rows and 25 text constructions from
`activated_ability_asset_bridge`.
The fourth activated-ability tranche closes
`Persuasion_RebukeRobot.AddSkill`, `ShortBlades_Shank.AddSkill`,
`AcidSlimeGlands.Mutate`, `AdrenalControl2.Mutate`, and
`Burgeoning.Mutate`, removing 5 rows and 22 text constructions from
`activated_ability_asset_bridge`.
The fifth activated-ability tranche closes five more mutation owners:
`Burrowing.Mutate`, `Carapace.Mutate`, `Clairvoyance.Mutate`,
`Confusion.Mutate`, and `Decarbonizer.Mutate`, removing 5 rows and 20 text
constructions from `activated_ability_asset_bridge`.
The sixth activated-ability tranche closes
`DefensiveChromatophores.Mutate`, `Domination.Mutate`,
`ElectromagneticPulse.Mutate`, `ErosTeleportation.Mutate`, and
`ForceWall.Mutate`, removing another 5 rows and 20 text constructions from
`activated_ability_asset_bridge`.
The seventh activated-ability tranche closes `FreezeBreath.AddAbility`,
`FrostWebs.Mutate`, `Infiltrate.Mutate`, `IrisdualBeam.Mutate`, and
`Kindle.Mutate`, adding no-argument mutation `AddAbility()` coverage for
`FreezeBreath` and removing another 5 rows and 20 text constructions from
`activated_ability_asset_bridge`.
The eighth activated-ability tranche closes `LeyShifting.Mutate`,
`LifeDrain.Mutate`, `LiquidSpitter.Mutate`, `MassMind.Mutate`, and
`MentalMirror.Mutate`, removing another 5 rows and 20 text constructions from
`activated_ability_asset_bridge`.
The ninth activated-ability tranche closes `Metamorphed.Apply`,
`Metamorphosis.Mutate`, `Phasing.Mutate`, `Serenity.Mutate`, and
`SpacetimeVortex.Mutate`, adding one-argument mutation `Apply(GameObject)`
coverage for `Metamorphed` and removing another 5 rows and 20 text
constructions from `activated_ability_asset_bridge`.
The tenth activated-ability tranche closes `SpiderWebs.Mutate`,
`Spinnerets.Mutate`, `StickyTongue.Mutate`, `Stinger.Mutate`, and
`StunningForce.Mutate`, removing another 5 rows and 20 text constructions from
`activated_ability_asset_bridge`.
The eleventh activated-ability tranche closes `SunderMind.Mutate`,
`TeleportOther.Mutate`, `TimeDilation.Mutate`, `WaveformWorm.Mutate`, and
`Axe_Berserk.AddSkill`, extending the mutation owner patch for four more
mutation registrations and the skill owner patch for `Berserk!`. This removes
another 5 rows and 20 text constructions from `activated_ability_asset_bridge`.
The twelfth activated-ability tranche closes
`CookingAndGathering_Butchery.AddSkill`, `Cudgel_Slam.AddSkill`,
`Cudgel_SmashUp.AddSkill`, `Discipline_Meditate.AddSkill`, and
`LongBladesDeathblow.AddSkill`, extending the skill owner patch for five more
registered ability names and removing another 5 rows and 20 text constructions
from `activated_ability_asset_bridge`.
The thirteenth activated-ability tranche closes
`LongBladesLunge.AddSkill`, `LongBladesSwipe.AddSkill`,
`Multiweapon_Flurry.AddSkill`, `Persuasion_Proselytize.AddSkill`, and
`Physic_AmputateLimb.AddSkill`, extending the skill owner patch for another
five registered ability names and removing another 5 rows and 20 text
constructions from `activated_ability_asset_bridge`.
The fourteenth activated-ability tranche closes `Pistol_Akimbo.AddAbility`,
`ShortBlades_Hobble.AddSkill`, and `ShortBlades_Rejoinder.AddAbility`. It
uses the existing skill-side no-argument `AddAbility()` and
`AddSkill(GameObject)` owner hooks for three already-dictionaried ability
names, removing another 3 rows and 12 text constructions from
`activated_ability_asset_bridge`.
The fifteenth activated-ability tranche closes `Survival_Camp.AddSkill`,
`Tinkering_DeployTurret.AddSkill`, `Cryokinesis.Mutate`,
`Disintegration.Mutate`, and `FearAura.Mutate`. It extends the skill and
mutation registration-name owner patches for five already-dictionaried names
and removes another 5 rows and 17 text constructions from
`activated_ability_asset_bridge`.
The sixteenth activated-ability tranche closes `Smash_Floor.AddSkill`,
`Snapjaw_Howl.AddSkill`, `Submersion.AddSkill`, `FlamingRay.AddAbility`,
`ForceBubble.Mutate`, `FreezingRay.AddAbility`, `MagneticPulse.AddAbility`,
and `Pyrokinesis.Mutate`. It adds fixed ability-name leaves for `Catapult`,
`Howl`, and `Submerge`, extends skill/mutation owner patches, and removes
another 8 rows and 27 text constructions from `activated_ability_asset_bridge`.
The seventeenth activated-ability tranche closes `RepellingForce.Mutate`,
`SlimeGlands.Mutate`, `Telepathy.Mutate`, `Teleportation.Mutate`,
`Cudgel_Conk.AddSkill`, `HeavyWeapons_Sweep.AddSkill`,
`Persuasion_Berate.AddSkill`, and `Persuasion_Intimidate.AddSkill`. It adds
fixed ability-name leaves for `Repelling Force`, `Conk`, `Sweep`, `Berate`,
and `Intimidate`, extends skill/mutation owner patches, and removes another
8 rows and 24 text constructions from `activated_ability_asset_bridge`.
The eighteenth activated-ability tranche closes `Rifle_DrawABead.AddSkill`,
`Shield_ShieldWall.AddSkill`, `Shield_Slam.AddSkill`,
`Tactics_Charge.AddSkill`, `Tactics_DeathFromAbove.AddSkill`, and
`Tactics_Juke.AddSkill`. It adds fixed ability-name leaves for `Mark Target`,
`Shield Wall`, `Shield Slam`, `Charge`, `Death From Above`, and `Juke`, extends
the skill owner patch, and removes another 6 rows and 18 text constructions
from `activated_ability_asset_bridge`.
The nineteenth activated-ability tranche closes the remaining
`activated_ability_asset_bridge` owners: `Belcher.Mutate`,
`BreatherBase.Mutate`, `GasGeneration.Mutate`, `IDelayedLineMutation.Mutate`,
`Quills.Mutate`, `TemporalFugue.Mutate`, and
`Acrobatics_Jump.SyncAbility`. It adds fixed ability-name leaves for the
belcher, breath, release-gas, delayed-line gaze, quill, fugue, and jump command
names, extends the mutation and skill owner patches, and removes the final 7
rows and 13 text constructions from `activated_ability_asset_bridge`.

The first Sifrah description implementation slice closes five exact
`HagglingSifrah.Result*()` description assignments. The new owner postfix
translates the fixed result `Description` values for critical failure, failure,
partial success, success, and exceptional success, with focused L2, L2G, and
policy evidence. This reduces `sifrah_description_route_split` by 5 rows and 5
text constructions. Remaining Sifrah constructor descriptions, token
descriptions, and sibling result/check routes still need exact description
owner evidence rather than generic popup evidence.
The follow-up Sifrah token description tranche closes
`TinkeringSifrahTokenLiquid(string)`,
`RitualSifrahTokenAttributeSacrifice(string)`, and
`RitualSifrahTokenInvokeHigherBeing.SetBeing(...)` with a shared owner postfix
for generated `Description` assignments such as `use <liquid>`,
`sacrifice a point of <attribute>`, and `invoke <being>`. This removes 3 more
rows and 64 text constructions from `sifrah_description_route_split`.

Disposition totals:

| Disposition | Rows |
| --- | ---: |
| child issue needed | 465 |
| runtime evidence required | 294 |
| likely implementation gap | 0 |

## Follow-up Issue Index

`--format followup-issues-json` now groups every remaining residual bucket into
the single #719 tracker. New residual follow-up issues should not be created
without explicit approval; bucket ownership stays explicit inside #719.

| Follow-up key | GitHub issue | Track | Rows | Text constructions | Owned buckets |
| --- | --- | --- | ---: | ---: | --- |
| `issue719-consolidated-residuals` | #719 | consolidated | 758 | 4,680 | all remaining residual buckets |

Track totals from the follow-up index are intentionally consolidated:

| Track | Rows |
| --- | ---: |
| consolidated | 758 |

The residual disposition split still comes from
`--format residual-buckets-json`:

| Disposition | Rows |
| --- | ---: |
| child issue needed | 464 |
| runtime evidence required | 294 |
| likely implementation gap | 0 |

### Consolidated Work Areas

- `producer_message_family_audit`: review remaining popup, MessageFrame, Does,
  and EmitMessage producer families by method-exact owner evidence. Promote
  only exact routes; keep sink-only, generated-name capture, and owner-ambiguous
  rows residual in #719.
- `sifrah_description_route_split` and `sifrah_popup_route_split`: split Sifrah
  token descriptions, setup popups, and result popups. Token/component names
  need owner reconstruction evidence, not generic popup-sink closure.
- Active-effect message buckets: keep MessageFrame, popup, and queued-message
  routes separate. Queue-only evidence must not close popup or MessageFrame
  rows.
- Description split buckets: split non-Sifrah description assignment,
  return/detail, and effect detail producers by owner route. Preset cooking
  recipe `GetDescription()` owners are now closed; existing description helpers
  apply only where the exact route is proven.
- Broad producer and generated display-name buckets: split `GameObject`,
  `GameObject.Die`, missile branches, village/faction/Sultan/mural/signature,
  dish, quest reward, and journal-linked generated names before promotion.
- UI popup sink buckets: `GetPopupOption` and `PickSeveral` are now closed as
  exact helper/owner routes. Continue auditing `WaitNewPopupMessage` and
  `NewPopupMessageAsync` only after caller ownership is separated.

Runtime evidence work should use the normal runtime evidence procedure in
`docs/workflows/runtime-evidence.md`: deploy the current mod, launch Caves of
Qud through Rosetta on Apple Silicon, exercise the target scenario, then inspect
a fresh `~/Library/Logs/Freehold Games/CavesOfQud/Player.log`. A triage
artifact can be captured with:

```bash
uv run python scripts/triage_untranslated.py \
  --log "$HOME/Library/Logs/Freehold Games/CavesOfQud/Player.log" \
  --output .artifacts/issue-719-runtime-triage.json
```

The generated implementation-gap track is empty. #781 is closeable after
review of the implementation slices for pseudopods, GasGeneration, activated
ability misc providers, and chargen direct UI text.

## Top Remaining Rows

### `producer_message_popup`

| Text constructions | Family | Surfaces |
| ---: | --- | --- |
| 76 | `JoppaTutorial/FightSnapjaw.cs::FightSnapjaw.LateUpdate()` | `TutorialManagerPopup` |
| 72 | `XRL.World/GameObject.cs::GameObject.AutoEquip(GameObject,bool,bool,bool)` | `Popup` |
| 63 | `JoppaTutorial/FightBear.cs::FightBear.LateUpdate()` | `TutorialManagerPopup` |
| 61 | `JoppaTutorial/MakeCamp.cs::MakeCamp.LateUpdate()` | `TutorialManagerPopup` |
| 51 | `XRL.World/GameObject.cs::GameObject.HandleInventoryActionEvent(InventoryActionEvent)` | `Popup` |

### `combat_message_frame_does`

| Text constructions | Family | Surfaces |
| ---: | --- | --- |
| 45 | `XRL.World.Parts/MissileWeapon.cs::MissileWeapon.CalculateBulletTrajectory(...)` | `MessageFrame` |
| 44 | `XRL.World/GameObject.cs::GameObject.Die(...)` | `JournalAPI`, `MessageFrame`, `Popup`, `TutorialManagerPopup` |
| 42 | `XRL.World.Parts/ElementalJelly.cs::ElementalJelly.FireEvent(Event)` | `MessageFrame` |
| 41 | `XRL.World.Parts/Panhumor.cs::Panhumor.FireEvent(Event)` | `MessageFrame` |
| 39 | `XRL.World.Parts/Harvestable.cs::Harvestable.AttemptHarvest(...)` | `Does`, `EmitMessage`, `MessageFrame` |

The active-effect message/popup pass now promotes the exact tranche 40 and 41 owner
routes for `Beguiled.Remove`,
`Confused.HandleEvent(IsConversationallyResponsiveEvent)`, and
`Dominating.HandleEvent(IsConversationallyResponsiveEvent)`, plus
`Immobilized.Apply`, `Stuck.Apply`, and
`LatchedOnto.HandleEvent(BeginTakeActionEvent)`. Remaining rows such as
`Lovesick.Apply`, `Beguiled.Apply`, `Proselytized.Apply`, and `Rebuked.Apply`
remained residual at that point because current evidence was limited to
active-effect description leaves, generic message/popup helpers, adjacent
owner methods, generated/mixed popup construction, or partial queue routes
rather than exact method-level owner patches for those rows.
A later existing-patch-only re-audit of the current active-effect residual
buckets also promotes no additional family. Top residuals such as
`Submerged.FireEvent` and `Burrowed.FireEvent` still mix popup/fail,
activated-ability, generated `EmitMessage`, or `MessageFrame` construction.
Tempting patches such as `AsleepOwnerTranslationPatch`,
`BrainBrineCurseTranslationPatch`, `EffectStaticMessageTranslationPatch`,
`FungalSporeInfectionTranslationPatch`, `HealingTranslationPatch`, and
`LatchedOntoExpiredTranslationPatch` target different methods, queue-only
subsets, or selected popup/queued strings, so they are not whole-family proof
for the remaining active-effect rows.
The first implementation pass after that audit closes
`XRL.World.Effects/Ill.cs::Ill.Remove(GameObject)`: the new
`IllRemoveTranslationPatch` owner-scopes the queued recovery message
`You no longer feel ill.`, routes it through the message queue semantic
pipeline, and is covered by L2 owner-scope queue tests plus L2G target
resolution. This removes one row and one text construction from
`active_effect_queue_route_split`.
The low-count `producer_runtime_evidence_required` bucket was also inspected
against existing patches. `Firefighting.AttemptFirefightingCore` has an owner
patch for the generated `You cannot reach ...` popup, but the residual family is
mixed with fixed popups and `Messaging.XDidY*` message frames. `ElementalJelly`
and `Panhumor` fire-event rows only have exact pseudopod `SetupPod` display-name
coverage, and `Harvestable.AttemptHarvest` currently has generic
message-pattern coverage rather than a method-level harvest owner patch. These
four families therefore stay residual.

### `description_effect_detail`

| Text constructions | Family | Surfaces |
| ---: | --- | --- |
| 34 | `XRL.World/ItemNamingSifrah.cs::ItemNamingSifrah.ItemNamingSifrah(GameObject,int,int)` | `Popup` |
| 23 | `XRL.World/BaetylOfferingSifrah.cs::BaetylOfferingSifrah.BaetylOfferingSifrah(GameObject,int,int)` | `Popup` |
| 19 | `XRL.World.Parts/MechanimistLibrarian.cs::MechanimistLibrarian.Initialize()` | `DescriptionAssignment` |
| 18 | `XRL.World/FormalWaterRitualSifrah.cs::FormalWaterRitualSifrah.FormalWaterRitualSifrah(GameObject)` | `Popup` |
| 16 | `XRL.World/ProselytizationSifrah.cs::ProselytizationSifrah.ProselytizationSifrah(GameObject,int,int)` | `Popup` |

The latest description pass closes base `XRL.World/Effect.cs::Effect.GetDetails()`
through `EffectDetailsPatch`, preset cooking recipe descriptions through
`CookingEffectTranslationPatch`, and the small action/effect description returns
through `ActionEffectDescriptionReturnTranslationPatch`. The next
description-detail return pass closes
`CyberneticsChoice.GetDescription()/GetLongDescription()` and
`TinkerData.Description/UnclippedDescription` through
`DescriptionDetailReturnTranslationPatch`. The follow-up game-object unit
passes now close all tracked `GameObjectUnit*.GetDescription(bool)` rows through
the same owner patch, including baetyl rewards, nearby clones, reputation
grants, secret reveals, the empty base description, and aggregate provided
descriptions. `game_object_unit_description_runtime` is now empty.
`AutoAct.GetDescription(...)` still stays residual.
The active-effect popup/queue implementation tranche closes five exact
owner-route families: `IrisdualCallow.Apply`,
`CookingDomainTongue_ThreeTongues_ProceduralCookingTriggeredAction.Apply`,
`ShadeOil_Tonic.FireEvent`, `BrainBrineCurse.FireEvent`, and
`SphynxSalt_Tonic.Apply`. `ActiveEffectPopupQueueTranslationPatch` owner-scopes
their `AddPlayerMessage`, `Popup.Show`, and `Popup.ShowYesNo` text, is
registered in both semantic pipelines, and is covered by L1 translator tests,
L2 owner-scope sink tests, L2G target resolution, and policy overlay evidence.
This removes 5 rows and 51 text constructions from #719's unreviewed queue.
A follow-up small-bucket audit found no additional exact existing-patch
promotion in `ui_popup_sink_route_split`, `world_zone_display_name_runtime`, or
remaining runtime description buckets. `XRL.World.Effects/CookingDomain*`
description rows are already covered by the active-effect owner route, and
`XRL.World.Skills.Cooking/*::GetDescription()` preset recipe rows are now
covered by exact `CookingEffectTranslationPatch` owner targets. The remaining
`action_description_runtime` row is only `AutoAct.GetDescription(...)`.
The description-assignment implementation tranches now remove all rows from
`description_assignment_route_split`; remaining description rows belong to
other buckets such as Sifrah descriptions, runtime UI descriptions, and
game-object unit descriptions.
A read-only Sifrah follow-up audit inspected the current
`sifrah_description_route_split` and `sifrah_popup_route_split` buckets and
found no additional existing-patch promotions. Existing Sifrah popup patches
cover constructor popup owners or selected result methods, but the residual
description assignments, result/check/early-exit methods, and partial token item
popup families lack exact family-level owner evidence.
A later read-only Sifrah re-audit reached the same conclusion before the
Haggling implementation slice: existing Sifrah popup evidence alone promoted no
additional description families. After the new exact Haggling result-description
owner route and the Sifrah token description tranche, 151
`sifrah_description_route_split` families and 46
`sifrah_popup_route_split` families remained residual. The Sifrah
`CheckEarlyExit` implementation tranche then adds exact owner targets for
`BaetylOfferingSifrah`, `BeguilingSifrah`, `FormalWaterRitualSifrah`, and
`HagglingSifrah`, covering their eight fixed confirmation/exit popups through
`SifrahPureOwnerPopupTranslationPatch`, focused L2 tests, L2G target-resolution
tests, and policy evidence. This removes 4 rows and 8 text constructions from
the Sifrah popup residual bucket, leaving 42 `sifrah_popup_route_split`
families. `SifrahPureOwnerPopupTranslationPatch` and the class-specific
`*SifrahTranslationPatch` files prove selected popup owners, result methods, or
token branches, but they do not cover constructor `Description` assignments,
sibling `CheckOutOfOptions` families, unimplemented result popup owners,
`CyberneticsTerminal2.HackingResultPartialSuccess`, or token `CheckTokenUse`
branches that still contain unproven fixed fallback popups.

### `display_name_composition`

| Text constructions | Family | Surfaces |
| ---: | --- | --- |
| 24 | `XRL.World.ZoneBuilders/VillageBase.cs::VillageBase.CreateVillageFaction(HistoricEntitySnapshot)` | `DisplayNameAssignment` |
| 22 | `XRL.World.ZoneBuilders/VillageCoda.cs::VillageCoda.GenerateSultanEntity(GameObject)` | `JournalAPI` |
| 18 | `XRL.World.Parts.Mutation/TemporalFugue.cs::TemporalFugue.CreateFugueCopyOf(...)` | `DisplayNameAssignment` |
| 18 | `XRL.World/GetRunningBehaviorEvent.cs::GetRunningBehaviorEvent.Retrieve(...)` | `DisplayNameAssignment` |
| 17 | `XRL.World.Parts/PlayerMuralController.cs::PlayerMuralController.blankMural(...)` | `DisplayNameAssignment` |

The latest display-name pass closes
`Qud.API/JournalVillageNote.cs::JournalVillageNote.GetDisplayText()` through
the existing `JournalEntryDisplayTextPatch` owner route and the L2 village-note
display test. No other audited residual display-name family can be closed from
existing evidence. `CookingRecipe.GetDisplayName()`,
`Cookbook.GenerateCookbook()`, `ZoneManager.GetZoneDisplayName(...)`, and the
ElementalJelly/Panhumor pseudopod setup display-name rows have exact owner
coverage where they appear, but the current residual rows are different
families: fixed recipe override `GetDisplayName()` methods, direct effect
`DisplayName` assignments, mutation `GetDisplayName` APIs, world/faction/zone
cache assignments, and village/sultan/mural generated names.
A follow-up generated display-name audit also found no new exact existing
owner-route promotions for `TemporalFugue.CreateFugueCopyOf`,
`GetRunningBehaviorEvent.Retrieve`, `Miner.SetupMinerConfiguration`,
`RandomFigurine.HandleEvent`, `CyberneticsScreenInstall.OnUpdate`, Bey Lah /
hydropon terrain generated names, molting basilisk sync names, village faction
names, Sultan/mural names, or signature dishes. Existing village, mural, and
UI patches cover adjacent routes and must not close these generated display-name
families without exact owner evidence. The same follow-up audit keeps
`WorldFactory.LoadWorldNode(...)`, ObjectFinder `GetDisplayName()` rows, and
sampled effect constructor `DisplayName` assignments because current evidence
is screen/sink or adjacent-route coverage rather than exact owner proof.

### `screen_ui_direct_text`

| Text constructions | Family | Surfaces |
| ---: | --- | --- |
| 15 | `XRL.UI/Popup.cs::Popup.WaitNewPopupMessage(...)` | `DirectTextAssignment` |
| 11 | `XRL.UI/Popup.cs::Popup.NewPopupMessageAsync(...)` | `DirectTextAssignment` |
| 10 | `Qud.UI/SkillsAndPowersStatusScreen.cs::SkillsAndPowersStatusScreen.ShowScreen(...)` | `SetText` |
| 9 | `Qud.UI/WorldGenerationScreen.cs::WorldGenerationScreen._ShowWorldGenerationScreen(int)` | `SetText` |
| 7 | `Qud.UI/TradeScreen.cs::TradeScreen.HandleHighlightObject(FrameworkDataElement)` | `SetText` |

### `activated_ability_names`

No remaining unreviewed rows.

The activated-ability implementation tranches now remove all residual
mutation, skill, and provider rows from this lane. Existing owner coverage
also handles exact non-mutation providers such as `Cloneling.Initialize`,
`Run.SyncAbility`, and `TrashRifling.Initialize`, plus UI/manager display
surfaces.

The local tutorial/broad-producer audit promotes
`XRL.World/GameObject.cs::GameObject.Heal(int,bool,bool,bool)` because
the existing exact `GameObjectHealTranslationPatch`, queue pipeline hook,
static-producer `CoveredOwnerFamily`, L2 message tests, L2G target-resolution
test, and `messages.ja.json` patterns cover the method's queued healing and
HP-loss message shapes. `TutorialManager*` patches still cover sink/helper
routes, not `JoppaTutorial/*` producer families, and the remaining
`GameObject`, `MissileWeapon`, and death-reason patches cover narrower or
adjacent routes rather than the remaining broad `GameObject.*` and
`MissileWeapon.CalculateBulletTrajectory` families.
A follow-up residual static-closure cross-check over
`producer_message_family_audit` found no additional exact
`CoveredOwnerFamily` rows. The remaining static-producer matches are
`CoveredOwnerCallsites` or `DeferredRuntimeCallsites` for subsets such as
`XRLGame.LoadGame`, `Scores.Show`, `SunderMind.Tick`,
`Container.AttemptOpen`, `PopulationManager.WishGenerate`,
`Physics.HandleEvent(ObjectEnteringCellEvent)`, `LifeDrain.FireEvent`,
`ElevatorSwitch.FireEvent`, `ThiefBot.FireEvent`, `Mutations.WishMutation`,
and several runtime-composed popup helpers. These stay residual because their
current evidence proves only selected lines, adjacent helpers, sink routes, or
runtime-deferred content rather than whole-family ownership.
A parallel rank 80-260 producer audit likewise found no further promotion
candidates. Tempting rows such as `ElevatorSwitch.FireEvent`,
`FixitSpray.HandleEvent`,
`MagnetizedApplicator.HandleEvent`, `FactionsStatusScreen.HandleCmdOptions`,
`InventoryAndEquipmentStatusScreen.HandleShowOptions`,
`AbilityManagerScreen.showScreen`, `DynamicQuestRewardElement_ChoiceFromPopulation.award`,
`GripChange.TryChooseGrip`, and
`WaterRitualRandomMutation.HandleEvent(EnteredElementEvent)` stay residual
because their current evidence is partial, mixed-surface, adjacent-method,
dictionary-only, or generated-option coverage rather than whole-family proof.
A follow-up UI/tutorial audit keeps all 89 inspected UI/tutorial runtime rows
residual. `WorldGenerationScreen`, `SkillsAndPowersStatusScreen`,
`PopupMessage`, line-specific UI patches, and `TutorialManager*` helpers cover
adjacent screen/helper routes, while the residual rows are the exact
`JoppaTutorial/*`, `UpdateMenuBars`, static option, hotkey/update, drag/scroll,
or screen-owner members that still need owner-specific proof.
A read-only UI/description runtime re-audit promoted no additional family before
the small action/effect implementation slice from
the current `ui_description_assignment_runtime`, `ui_screen_route_runtime`,
`ui_popup_sink_route_split`, `action_description_runtime`, or
other runtime description buckets. Existing UI and description
patches cover adjacent screen methods, sink-side structured text, base
description helpers, procedural cooking-effect owners, or the newly added small
action/effect and description-detail owner returns, but not remaining popup sink
internals or other exact producer families still in #719.
A read-only producer rank 21-80 audit also promotes no family. Existing exact
or near-exact evidence for rows such as `Examiner.HandleEvent`,
`TinkerItem.HandleEvent`, `Physics.HandleEvent(ObjectEnteringCellEvent)`, and
`FixitSpray.HandleEvent` is partial: those families still contain mixed Sifrah,
failure popup, ownership confirmation, blocked-way/collision, world-map, or
generic single-callsite popup shapes that are not proven by the current
patch/test evidence. Adjacent-method rows such as `Cloneling.PerformCloning`,
`DanceRitualOpponent.HandleEvent(BeforeAITakingActionEvent)`,
`PlayerDanceRitual.FireEvent`, and `ModInfo.ConfirmFailure` likewise stay
residual because current patches target different members.
A read-only producer rank 81-261 audit promotes
`NeutronFluxContainment.HandleEvent(BeginTakeActionEvent)` because the exact
single-callsite owner popup patch and L2/L2G evidence cover the full travel
warning popup composed from `GetWarningMessage()` plus the fixed stop-travelling
tail. The same pass keeps `CursedCellSocket.HandleEvent(CellDepletedEvent)`
residual: existing `CursedCellSocketLocks` evidence applies to
`CellChangedEvent`, while the depleted-cell family emits `... burns into bright
slag.` through `EmitMessage`.
The parallel subagent implementation tranche then closes 11 more families split
across two review units. The active-effect / Examiner unit closes
`Submerged.FireEvent`, `Submerged.Remove`, `Burrowed.FireEvent`,
`Burrowed.Emerge`, `Examiner.MakeUnderstood`, and
`Examiner.MakePartiallyUnderstood` through exact owner patches, shared queue /
popup pipeline registration, focused L2 tests, and L2G target-resolution
coverage. The UI menu-option unit closes `FactionsStatusScreen.ShowScreen`,
`HighScoresScreen.UpdateMenuBars`, `KeybindsScreen.UpdateMenuBars`, and the
two `CharacterAttributeLine` static menu-option fields through the explicit
`MenuOption.Description` owner patch and focused L2/L2G evidence. The next UI
menu-option tranche extends that same exact owner route to
`AskNumberScreen.getItemMenuOptions`, `SaveManagement.UpdateMenuBars`, and the
`CharacterEffectLine`, `CharacterMutationLine`, and `EquipmentLine` static
expand/collapse option fields. This removes 8 more runtime-evidence rows and
28 text constructions from `ui_description_assignment_runtime`. The next
active-effect Apply-only tranche extends `ActiveEffectPopupQueueTranslationPatch`
to `Hobbled.Apply`, `Terrified.Apply`, `GeometricHeal.Apply`, `Trance.Apply`,
`StingerPoisoned.Apply`, and `FuriouslyConfused.Apply`, covering their scoped
`DidX` onset messages with focused L2 and L2G evidence. This removes 6 rows and
40 text constructions from `active_effect_message_frame_route_split`. The
follow-up active-effect promotion tranche adds `Confused.Apply`,
`Poisoned.Apply`, `PhasePoisoned.Apply`, `Healing.Apply`, `Dazed.Apply`, and
`Paralyzed.Apply` to the same owner route, including popup coverage for
`Poisoned.Apply`'s `StartMessageUsePopup` branch. This removes another 6 rows
and 33 text constructions from `active_effect_message_frame_route_split`. This
Sifrah popup tranche promotes `SocialSifrahTokenGift.CheckTokenUse` and
`SocialSifrahTokenItem.CheckTokenUse` through the existing token-item owner
patch, extends the pure Sifrah owner patch to `ItemNamingSifrah.CheckEarlyExit`,
`ProselytizationSifrah.CheckEarlyExit`, `PsychicCombatSifrah.CheckEarlyExit`,
`RebukingSifrah.CheckEarlyExit`, and adds `SifrahGame.UseInsight` popup
coverage. This removes 7 rows and 18 text constructions from
`sifrah_popup_route_split`. A fixed-dictionary BaetylOffering audit then
promotes `BaetylOfferingSifrah.CheckOutOfOptions` and the five
`Result*` popup families through `static_producer_closure.py`
`existing_dictionary_coverage` plus the existing generic popup/message
dictionary routes; no new owner patch is required for these stable leaves.
This removes another 6 rows and 6 text constructions from
`sifrah_popup_route_split`.
The active-effect generated-message tranche adds exact
`EffectGeneratedMessageTranslationPatch` targets for `Rusted.Apply`,
`Asleep.FireEvent`, and `StunGasStun.FireEvent`, with focused L2 queue-message
coverage, L2G target-resolution coverage, and MessageFrame verb dictionary
evidence. This removes 3 rows and 39 text constructions from
`active_effect_message_frame_route_split`.
The follow-up active-effect message tranche extends the exact active-effect
owner routes to `Poisoned.FireEvent`, `PhasePoisoned.FireEvent`, and
`LatchedOnto.FireEvent`. `Poisoned` and `PhasePoisoned` reuse the scoped
`ActiveEffectPopupQueueTranslationPatch` route for recuperation
`no longer poisoned` queue messages, while `LatchedOnto.FireEvent` uses the
generated-effect owner route with article normalization for the held-by capture.
This removes another 3 rows and 30 text constructions from
`active_effect_message_frame_route_split`.
The next active-effect generated-message tranche extends the exact generated
effect route to `Proselytized.HandleEvent(InventoryActionEvent)`,
`Rebuked.HandleEvent(InventoryActionEvent)`, and `ShieldWall.Apply`. The owner
route now translates dismiss-from-service queue messages with possessive
normalization and shield-wall raise messages without leaking `your` into the
object capture. This removes another 3 rows and 27 text constructions from
`active_effect_message_frame_route_split`.
The next active-effect generated-message tranche closes
`EmptyTheClips.Apply`, `Ill.FireEvent`, and `Running.Apply` with exact
`EffectGeneratedMessageTranslationPatch` targets. It adds focused tests for
Empty the Clips clasp possessive normalization, Ill recuperation subject
omission for the player message, and Running begin-message frames. This removes
another 3 rows and 24 text constructions from
`active_effect_message_frame_route_split`.
The next active-effect popup/queue tranche extends the scoped owner route to
`AshPoison.FireEvent`, `BasiliskPoison.FireEvent`, `Cripple.FireEvent`, and
`PoisonGasPoison.FireEvent`. This covers `no longer choking`, `feel less
stiff`, `no longer crippled`, and the existing `no longer poisoned` recovery
shape while preserving BasiliskPoison's existing fixed `You feel stiff as a
stone.` static queue coverage. This removes another 4 rows and 21 text
constructions from `active_effect_message_frame_route_split`.
The next fixed active-effect popup/queue tranche extends the same scoped owner
route to `Luminous.Apply`, `Meditating.Apply`, `Scintillating.Apply`,
`Suppressed.Apply`, `ShadeOil_Tonic.Apply`, and `Asleep.Remove`. This covers
fixed onset/status and wake-up/sleep-mode recovery MessageFrame shapes while
leaving adjacent display-name constructors and unrelated generated removal
messages in their existing residual buckets. This removes another 6 rows and
26 text constructions from `active_effect_message_frame_route_split`.
The next active-effect promotion/implementation tranche closes
`Submerged.Apply` and `Burrowed.Apply` through the exact submerged/burrowed
owner route already covering their queue messages, and adds
`Stun.HandleEvent(BeginTakeActionEvent)` to the generated-effect owner route for
the fixed `remain stunned` turn-loop message. This removes another 3 rows and
21 text constructions from `active_effect_message_frame_route_split`.
The next active-effect fixed-frame promotion tranche closes `Sitting.StandUp`,
`Frenzied.TriggerBerserk`, `SporeCloudPoison.FireEvent`, and
`CardiacArrest.Apply` through exact fixed MessageFrame route evidence and
focused repository dictionary tests. This removes another 4 rows and 15 text
constructions from `active_effect_message_frame_route_split`; `CardiacArrest.Remove`
remains residual because it mixes player-only popups, non-player MessageFrame
text, and nested `Ill` application.
The next active-effect MessageFrame implementation tranche closes
`Running.Remove`, `ResummonGloaming.HandleEvent(EnteredCellEvent)`, and
`CookingDomainArtifact_IdentifyAllInZone_ProceduralCookingTriggeredAction.Apply`
through fixed MessageFrame dictionary leaves and focused repository tests. This
removes another 3 rows and 7 text constructions from
`active_effect_message_frame_route_split`.
The next active-effect MessageFrame implementation tranche closes
`LifeDrain.Apply`, `LifeDrain.HandleEvent(InventoryActionEvent)`, and
`Bleeding.StartMessage` through fixed MessageFrame dictionary leaves and
focused repository tests. This removes another 3 rows and 21 text constructions
from `active_effect_message_frame_route_split`.
The next active-effect implementation tranche closes `Beguiled.Remove` through
the fixed `DidXToY`/`XDidYToZ` MessageFrame route and closes
`Confused.HandleEvent(IsConversationallyResponsiveEvent)` plus
`Dominating.HandleEvent(IsConversationallyResponsiveEvent)` through exact
ConversationScript owner popup tests. This removes another 3 rows and 10 text
constructions from `active_effect_message_frame_route_split`.
The next active-effect DidX owner tranche closes `Immobilized.Apply`,
`Stuck.Apply`, and `LatchedOnto.HandleEvent(BeginTakeActionEvent)` through
`ActiveEffectMessageFrameOwnerTranslationPatch`, focused L2 owner-scope
message-frame tests, and L2G target resolution. This removes another 3 rows and
20 text constructions from `active_effect_message_frame_route_split`, leaving
that bucket at 5 rows and 46 text constructions. `CardiacArrest.Remove` remained
residual because whole-family closure still needs its player popup and nested
`Ill` side-effect shapes split or covered.
The next social active-effect owner tranche closes `Lovesick.Apply`,
`Beguiled.Apply`, `Proselytized.Apply`, and `Rebuked.Apply` by combining the
method-scoped MessageFrame owner patch with JournalAPI storage-time patterns
for every social accomplishment text, mural, and gospel argument. This removes
another 4 rows and 41 text constructions from
`active_effect_message_frame_route_split`, leaving that bucket at 1 row and 5
text constructions. `CardiacArrest.Remove` remained residual until tranche 43.
The next active-effect owner tranche closes `CardiacArrest.Remove` by combining
the existing method-scoped DidX frame owner route with exact owner-scope
coverage for the two player restart popups and the nested `Ill.Apply` popup
message `"You feel shaken and infirm."`. This removes the final 1 row and 5
text constructions from `active_effect_message_frame_route_split`.
The next producer MessageFrame/Does promotion tranche avoids promoting the full
pure MessageFrame set after read-only route review found that most rows still
lack exact owner or global-route evidence. It promotes only the directly proven
`VehicleRepair.HandleEvent(InventoryActionEvent)` owner route and five mutation
action MessageFrame families already covered by global `XDidY`/`XDidYToZ`
dictionary evidence: `Cloneling.PerformCloning(GameObject)`,
`StunningForce.Concussion(...)`, `IDelayedLineMutation.Refract(List<Cell>)`,
`Decarbonizer.fireBeam(List<Cell>,bool)`, and
`LiquidSpitter.HandleEvent(CommandEvent)`. This adds the missing Cloneling
`produce`/`a clone of {0}` MessageFrame leaf and removes 6 rows / 103 text
constructions from `producer_message_family_audit`.
The next producer popup promotion tranche closes exact single-callsite popup
owners already covered by `SingleCallsiteOwnerPopupTranslationPatch`:
`XRLGame.LoadGame`, `Food.HandleEvent(InventoryActionEvent)`,
`Container.AttemptOpen(GameObject,IEvent)`, and
`PopulationManager.WishGenerate(string)`. Existing L2 owner tests, L2G target
resolution, and `ui-popup.ja.json` dictionary entries prove these routes; no
new translations were needed. `GameObject.PullDown(bool)` remains residual
because the existing pull-down proof is for `StairsDown.CheckPullDown`, not the
GameObject method. This removes another 4 rows / 111 text constructions from
`producer_message_family_audit`.
The next producer MessageFrame dictionary tranche closes seven fixed
MessageFrame producer families through the repository `XDidY`/`XDidYToZ` route:
`GeomagneticDisc.DoThrow(...)`, `Leveler.LevelUp`,
`CryptFerretBehavior.FireEvent`, the two matter recompositer teleport
handlers, `PlaceTurretGoal.TakeAction`, and `GasGeneration.FireEvent`.
`GeomagneticDisc.DoThrow(...)` needed one additional MessageFrame leaf for
`pass` + `through {0}`; the remaining frames reuse existing `gain a level`,
`filch`, `teleport`, `place`, `flinch out of the way of {0}`, and
`start/stop releasing {0}` entries. This removes another 7 rows / 93 text
constructions from `producer_message_family_audit`.
The next fixed producer popup dictionary tranche closes four fixed Popup /
PickOption producer families through existing `ui-popup.ja.json` entries and
generic popup routes: `EndGame.PickState()`,
`PronounAndGenderSets.ShowPickGenderAndPronounSet(GameObject,string)`,
`CheckpointingSystem.ShowDeathMessage(string,string)`, and
`PronounAndGenderSets.ShowChangePronounSet(GameObject)`. Focused repository
dictionary tests cover the fixed prompt and end-game option entries; no new
translations were needed. `GameObject.AutoEquip(GameObject,bool,bool,bool)`
remains residual because its popup source is a separate inventory/equipment
route not proven by this tranche. This removes another 4 rows / 136 text
constructions from `producer_message_family_audit`.
The next combat MessageFrame dictionary tranche closes eight pure
`MessageFrame` producer families through new repository dictionary leaves:
`PointDefense.HandleEvent(ProjectileMovingEvent)`,
`GreaterVoider.FireEvent(Event)`, `RunOver.PerformCharge(List<Cell>,bool)`,
`AjiConch.ActivateAjiConch()`, `Disarming.Disarm(...)`,
`EngulfingClones.FireEvent(Event)`, `Fan.TurnTick(long,int)`, and
`HookOnMissileHit.FireEvent(Event)`. The new leaves cover intercept
pass-through/no-effect, teleport-to-lair, run-over/stopped-in-tracks, Aji conch
blowing, disarm-of, refract/try-to-refract, fan blown-back variants, and
dragged-toward frames. `SapOnPenetration.FireEvent(Event)` remains residual
because the current generic MessageFrame route leaves generated stat names such
as `Strength` untranslated without additional attribute-name proof or owner
handling. This removes another 8 rows / 105 text constructions from
`producer_message_family_audit`.
The next physical MessageFrame dictionary tranche closes eight additional pure
`MessageFrame` producer families through repository dictionary leaves:
`SunderMind.Blast(MentalAttackEvent)`, `Physics.AccelerateInternal(...)`,
`Butcherable.AttemptButcher(...)`, `PluckablePolyp.Pluck(GameObject)`,
`Interior.ShowMessage(GameObject,int)`,
`CyberneticsStasisProjector.HandleEvent(CommandEvent)`,
`TimeDilation.HandleEvent(CommandEvent)`, and
`SwapOnHit.SwapPositions(...)`. The new leaves cover sunder no-effect attempts,
knock/collide frames, butcher success/failure frames, coral polyp plucking,
stasis-field projection, time distortion, interior entry-denial messages, and
position swaps. `MissileWeapon.CalculateBulletTrajectory(...)` remains residual
because its source mixes projectile path/reflection and later missile-hit route
shapes, while `SapOnPenetration.FireEvent(Event)` still needs stat-name
translation proof. This removes another 8 rows / 117 text constructions from
`producer_message_family_audit`.
This leaves 727 residual rows: 433 child-issue-needed residuals and 294
runtime-evidence residuals. The Sifrah constructor-description prototype was
intentionally not promoted because the same Sifrah residual bucket still needs
slot configuration description coverage before whole-family closure is safe.
The next Sifrah constructor popup promotion tranche closes twelve pure-owner
constructor Popup families already targeted by
`SifrahPureOwnerPopupTranslationPatch`: `BaetylOfferingSifrah`,
`FormalWaterRitualSifrah`, `HagglingSifrah`, `DisarmingSifrah`,
`ExamineSifrah`, `HackingSifrah`, `ProselytizationSifrah`,
`RebukingSifrah`, `ItemModdingSifrah`, `ItemNamingSifrah`, `RepairSifrah`,
and `ReverseEngineeringSifrah`. This is promotion-only: existing L2 owner
tests, L2G target resolution, and `ui-popup.ja.json` evidence prove the route.
`BeguilingSifrah.BeguilingSifrah(GameObject,int,bool,int,int)` remains
residual because the existing pure-owner patch targets `CheckEarlyExit`, not
the constructor, and `PsychicCombatSifrah` remains `runtime_required` because
the decompiled class is not used in the base game. This removes another 12
rows / 168 text constructions from `sifrah_popup_route_split`.
This leaves 715 residual rows: 421 child-issue-needed residuals and 294
runtime-evidence residuals. The largest remaining action buckets are
`producer_message_family_audit` (221 rows) and `sifrah_description_route_split`
(140 rows); the next high-yield pass should split one of those by route shape
instead of taking single-family cleanup.
The next Sifrah token no-arg description tranche implements exact owner postfix
coverage for 98 no-argument Sifrah token constructors across psionic, ritual,
social, and tinkering token families. The route is `Description` assignment in
the constructor, not a popup/message sink. Fixed descriptions are translated by
`SifrahTokenDescriptionTranslator`; dynamic overloads such as
`RitualSifrahTokenEffectDazed(int)` and object/liquid/bit overloads stay out of
this tranche unless already covered by existing owner evidence. This removes
98 rows / 345 text constructions from `sifrah_description_route_split`.
This leaves 617 residual rows: 323 child-issue-needed residuals and 294
runtime-evidence residuals. The largest remaining action bucket is
`producer_message_family_audit` (221 rows); the Sifrah description bucket is
down to 42 rows and should now be handled as dynamic overload/object-name
residuals rather than as fixed no-arg constructor text.
The next environmental MessageFrame dictionary tranche closes fifteen pure
`MessageFrame` producer families through repository dictionary leaves:
`LayMineGoal.TakeAction`, `BurgeonOnHit.FireEvent`,
`BurnOffGas.FireEvent`, `GrabberArm.FireEvent`, `Ironshroom.FireEvent`,
`DropOnDamage.FireEvent`, `Sweeper.FireEvent`, `PetPhylactery.Spawn`,
`TemplarPhylactery.Spawn`, `ReflectShame.Shame`, `EelSpawn.Reveal`,
`EjectionSeat.Message`, `DiThermoBeam.FlipBeam`, `StickyOnHit.Entangle`,
and `Tonic.HandleEvent(ExamineCriticalFailureEvent)`. The new leaves cover
mine placement, germination, gas burn-off, grab-and-hold, impaling, dropping,
consuming, phylactery activation/appearance, shame reflection, sewage-eel
spotting, seat ejection, polarity flipping, entangling, and tonic applicator
mispricks. `ExtradimensionalHunterSummoner.Summon` remains residual because
the method mixes a MessageFrame `emerge from` frame with a `UsePopup: true`
`open wide...` frame, so whole-family closure would overclaim this tranche.
This removes 15 rows / 74 text constructions from
`producer_message_family_audit`.
This leaves 602 residual rows: 308 child-issue-needed residuals and 294
runtime-evidence residuals. The largest remaining action bucket is
`producer_message_family_audit` (206 rows); the next high-yield pass should
continue splitting that bucket by exact route shape before promoting more
MessageFrame leaves.
The next utility MessageFrame dictionary tranche closes twelve more pure
`MessageFrame` producer families through repository dictionary leaves:
`EnergyCellSocket.AttemptRemoveCell(...)`, `Domination.Dominate`,
`SlipRing.FireEvent`, `LavaSludge.HandleEvent(BeforeDieEvent)`,
`NoStandUp.HandleEvent(InventoryActionEvent)`, `StairsDown.FireEvent`,
`Thurible.SmokeThurible`, `Disintegration.HandleEvent(CommandEvent)`,
`Metamorphed.FireEvent`, `BlinkOnDamage.FireEvent`,
`Interdiction.BeginInterdiction`, and `QuantumFugue.Cohere`. The new leaves
cover energy-cell removal/pop-out, domination take-control/prevent/resist
frames, slip-ring evasion, cooling into shale, stand-up assistance/failure,
locked stairs, incense, destructive vibration, metamorphosis reversion,
interdiction lock-on, and existing blink/cohere frames. Mixed or generated
pure-MessageFrame families such as `SapOnPenetration.FireEvent`,
`PetFrondzie.taunt`, `PsychicHunterSystem.PsychicPresenceMessage`, and
`Shrine.PerformDesecration` remain residual because they need stat-name,
generated-taunt, popup-parameter, or runtime/owner proof beyond fixed leaves.
This removes 12 rows / 72 text constructions from
`producer_message_family_audit`.
This leaves 590 residual rows: 296 child-issue-needed residuals and 294
runtime-evidence residuals. The largest remaining action bucket is
`producer_message_family_audit` (194 rows).
The next pure EmitMessage pattern promotion tranche closes eight
`producer_message_family_audit` families already served by
`GameObjectEmitMessageTranslationPatch`, `MessagePatternTranslator`, and the
repository message pattern/leaf dictionaries: `Mutations.AddChimericBodyPart`,
`LiquidProteanGunk.ProcessTurns`, `GeomagneticDisc.FireEvent`,
`ForceBubble.CreateBubble`, `DecoyHologramEmitter.PlaceHologram`,
`RocketSkates.HandleEvent(JumpedEvent)`,
`PsychicHunterSystem.CheckPsychicHunters`, and
`CursedCellSocket.HandleEvent(CellDepletedEvent)`. No new translations were
needed; existing pattern/leaf coverage handles body-part growth, primordial
soup reactions, geomagnetic-disc collisions, force-bubble creation, hologram
appearance, rocket-skate flame jets, normality-field interloper denial, and
burning depleted cells into slag. Warm static wish/debug paths,
`DesalinationPellet.HandleEvent`, message-queue debug rows, and sink-only
`MessageQueue.AddPlayerMessage` remain residual because they are not the same
fixed EmitMessage owner route. This removes 8 rows / 51 text constructions from
`producer_message_family_audit`.
This leaves 582 residual rows: 288 child-issue-needed residuals and 294
runtime-evidence residuals. The largest remaining action bucket is
`producer_message_family_audit` (186 rows).
The follow-up SapOnPenetration MessageFrame tranche closes
`SapOnPenetration.FireEvent(Event)` by adding tier3 `permanently drain`
frames for singular/plural stat-drain tails such as
`Strength by 2 points` and translating the captured stat through the
repository attribute dictionaries. This removes 1 row / 29 text constructions
from `producer_message_family_audit`.
This leaves 581 residual rows: 287 child-issue-needed residuals and 294
runtime-evidence residuals. The largest remaining action bucket is
`producer_message_family_audit` (185 rows).
The next residual pure MessageFrame tranche closes 16 fixed
`producer_message_family_audit` families / 63 text constructions:
`FeelingOnTarget.FireEvent`, `TimeDilation.ApplyField`, `Chair.StandUp`,
`IrisdualBeam.InflictDamage`, `EngulfingHandOff.AttemptHandOff`,
`IStingerProperties.FailureMessage`, `ReflectProjectiles.Check`,
`ReflectProjectiles.FireEvent`, `RunOver.HandleEvent`,
`SkybearShroud.ActivateSkyshroud`, `Banner.HandleEvent`,
`CooldownOnStep.HandleEvent`, `CyberneticsCathedraBlackOpal.Activate`,
`CyberneticsCathedraWhiteOpal.Activate`,
`IfThenElseQuestWidget.TurnTick`, and
`PsychicMeridian.AfflictNosebleed`. The new leaves cover time-dilation
distortion, calm decisions, irisdual-beam damage, engulfing handoff, venom
resistance, neuronal and psychic thorn frames, cathedra activation, and
skybear dashing; existing leaves cover stand-up, reflection-shield, run-over,
banner raise, and disappear frames. `SpaceTimeVortex.SpaceTimeAnomalyPeriodicEvents`
remains residual because its method mixes MessageFrame with popup-capable
routes, and generated pet/liquid/reward/wish families remain unpromoted.
This leaves 565 residual rows: 271 child-issue-needed residuals and 294
runtime-evidence residuals. The largest remaining action bucket is
`producer_message_family_audit` (169 rows), with pure `MessageFrame` down to
20 residual families / 87 text constructions.
The residual Popup owner-route tranche closes 3 exact
`producer_message_family_audit` families / 20 text constructions:
`LifeDrain.FireEvent(Event)`, `Mutations.WishMutation(string)`, and
`WaterRitualRandomMutation.HandleEvent(EnteredElementEvent)`. The tranche adds
owner-route coverage for LifeDrain no-target, mutation-wish missing-name and
missing-variant, and water-ritual non-mutant popups. Existing branches already
covered LifeDrain invalid-target, mutation did-you-mean, water-ritual
incompatible-category, and generated mutation grant popups. Larger pure-Popup
families such as `Physics.ProcessTargetedMove`, `Scores.Show`,
`TradeUI.ShowVendorActions`, `GiveReshephSecret.HandleEvent`, and
`GritGateTerminalScreenRoot.UpdatePowerOptions` remain residual because their
methods still mix unowned popup branches or different sink/producer routes.
This leaves 562 residual rows: 268 child-issue-needed residuals and 294
runtime-evidence residuals. The largest remaining action bucket is
`producer_message_family_audit` (166 rows), with pure `Popup` down to 65
residual families / 608 text constructions.
The residual pure Does tranche closes 16 exact
`producer_message_family_audit` families / 109 text constructions:
`QuickenMind.Activate`, `CyberneticsStasisEntangler.ActivateStasisEntangler`,
`ModGlassArmor.HandleEvent`, `CyberneticsStasisArena.ActivateStasisArena`,
`Stomach.HandleEvent(InduceVomitingEvent)`,
`CooldownAmmoLoader.GetCoolingDownMessage`,
`TemplarPhylactery.HandleEvent(InventoryActionEvent)`,
`LiquidAmmoLoader.GetStatusMessage`, `PoweredFloating.FireEvent`,
`ConversationScript.AttemptConversation`,
`ElectricalDischargeLoader.GetStatusMessage`,
`MagazineAmmoLoader.HandleEvent(LoadAmmoEvent)`,
`EnergyAmmoLoader.GetStatusMessage`, `ModLiquidCooled.GetStatusMessage`,
`AIWiring.HandleEvent(IsConversationallyResponsiveEvent)`, and
`TemplarPhylactery.HandleEvent(GetShortDescriptionEvent)`. The tranche adds
MessageFrame leaves for "not loaded with the correct liquid" and
"your domination" while existing DoesVerb leaves cover active-part status,
reflect-damage, vomiting, ammo/floating/conversation, AI wiring, and phylactery
messages. `GameText.RoughConvertSecondPersonToThirdPerson`,
`Physics.UpdateTemperature`, `CyberneticsScreenMainMenu`,
`TrembleEarthquakes.RocksFall`, `LootOnStep.SteppedOn`,
`NeutronFluxContainment.GetWarningMessage`, `CyberneticsTerminal2.AttemptInterface`,
and `Domination.ProcessTarget` remain residual because they are broad
conversion, death-reason, UI paragraph, popup-adjacent, or mixed route shapes.
This leaves 546 residual rows: 252 child-issue-needed residuals and 294
runtime-evidence residuals. The largest remaining action bucket is
`producer_message_family_audit` (150 rows), with pure `Does` down to 8
residual families / 82 text constructions.
The residual mutation command popup/frame tranche closes 10 exact
`producer_message_family_audit` families / 100 text constructions:
`StickyTongue.HarpoonNearest`, `SlogGlands.FireEvent`,
`Stinger.HandleEvent(CommandEvent)`, `LeyShifting.HandleEvent(CommandEvent)`,
`Burgeoning.Burgeon`, `Phasing.FireEvent`, `SpacetimeVortex.FireEvent`,
`Burrowing.HandleEvent(CommandEvent)`, `Spinnerets.FireEvent`, and
`ElectricalGeneration.PerformDischarge`. The tranche adds StickyTongue
MessageFrame leaves for pull-to-self / pull-toward-self / failed-pull frames;
fixed mutation popup failures are served by existing shipped popup/world-message
dictionaries, and ElectricalGeneration grounding remains owned by the existing
mutation action-failure patch. `SunderMind.Tick` and `Wings.HandleEvent` remain
residual because their popup branches are covered only partially or by a
different owner patch, and broad UI/popup families are still unpromoted.
This leaves 536 residual rows: 242 child-issue-needed residuals and 294
runtime-evidence residuals. The largest remaining action bucket is
`producer_message_family_audit` (140 rows), with `MessageFrame+Popup` down to
11 residual families / 147 text constructions and pure `Popup` down to 60
families / 571 text constructions.
The residual `MessageFrame+Popup` split tranche closes 9 exact
`producer_message_family_audit` families and moves 2 mixed families to
runtime-required. The covered families are
`Stomach.HandleEvent(BeginTakeActionEvent)`, `ReshephsCrypt.FireEvent`,
`StiltWell.GiveArtifacts`, `RebornOnDeathInThinWorld.FireEvent`,
`EngulfingDescends.FireEvent`, `Infiltrate.FireEvent`,
`AmbientPowerReceiver.HandleEvent(EnteringZoneEvent)`,
`RestoreOnDeath.HandleEvent(BeforeDieEvent)`, and
`ModDisplacer.ExamineFailure`. The tranche adds fixed popup leaves for the
Resheph sarcophagus confirmation prompt and the Stilt well artifact picker,
plus MessageFrame leaves for throwing artifacts down the well, engulfing descent
through the floor, and displacer bump frames. Existing popup and MessageFrame
coverage handles Stomach hydration/travel prompts, Resheph fixed sarcophagus
states, death-in-Thin-World, infiltrate world-map failure, ambient power-grid
transition, health restore, sudden elsewhere, and the EngulfingDescends
passenger-fall owner popup. `MagazineAmmoLoader.FireEvent` and
`Brain.HandleEvent(InventoryActionEvent)` are now runtime-required because
their live picker/debug popup branches cannot be safely split from adjacent
MessageFrame routes by static inventory alone.
This leaves 527 residual rows: 231 child-issue-needed residuals and 296
runtime-evidence residuals. The largest remaining action bucket is
`producer_message_family_audit` (129 rows), with `MessageFrame+Popup` cleared
to zero, pure `Popup` at 59 families / 504 text constructions, and
`Does+MessageFrame` at 7 families / 132 text constructions.
The residual `Does+MessageFrame` split tranche processes all 7 remaining
families / 132 text constructions in that shape. It promotes
`Pettable.Pet`, `Robot.FireEvent`, `IProgrammableRecoiler.ProgramRecoiler`,
and `Hookah.SmokeHookah` through the existing Does fragment marking,
MessageFrame route, and concrete verb leaves. It moves
`TemporalFugue.PerformTemporalFugue`,
`AutomatedExternalDefibrillator.AttemptDefibrillate`, and
`CyberneticsPrecisionForceLathe.ActivatePrecisionForceLathe` to
runtime-required because their static family rows mix Does/MessageFrame with
generated Fail, confirmation, target, or body-part branches that need live route
evidence before the whole owner can be claimed. The tranche adds MessageFrame
leaves for recoiler charge failure, hookah puffing, temporal-fugue blur and
multiply frames, and defibrillator use/dodge frames.
This leaves 523 residual rows: 224 child-issue-needed residuals and 299
runtime-evidence residuals. The largest remaining action bucket is
`producer_message_family_audit` (122 rows). Fresh route-shape split for that
bucket is pure `Popup` 60 families / 571 text constructions, pure
`MessageFrame` 20 / 87, pure `Does` 8 / 82, and mixed
`AddPlayerMessage+Popup` 5 / 69. `Does+MessageFrame` is cleared to zero.

The residual pure Popup top split tranche processes the largest 8 pure-Popup
families / 238 text constructions in `producer_message_family_audit`. It
promotes `GritGateTerminalScreenRoot.UpdatePowerOptions` through the existing
Popup.Show dictionary route and shipped `ui-popup.ja.json` leaves for the
remote-management-offline chain-laser and force-projector warnings. It moves
`OptionsUI.Show`, `Scores.Show`, `ItemNaming.NameItem`, `Crayons.HandleEvent`,
`Description.HandleEvent`, `Inventory.HandleEvent`, and
`TradeUI.ShowVendorActions` to runtime-required because their decompiled
methods mix fixed popups with option screens, score deletion/details, naming
pickers, player-authored drawing or description text, inventory chooser
routes, generated item/trader/water-debt prompts, or other live sink branches
that static family rows cannot safely split. No new translations were added in
this tranche; the two GritGate strings were promotion-only existing leaves.
This leaves 522 residual rows: 216 child-issue-needed residuals and 306
runtime-evidence residuals. Policy counts are now
`covered_by_owner_route=2120` rows / `16741` text constructions,
`action_required=211` / `1419`, and `runtime_required=311` / `1488`. The
largest remaining action bucket is still `producer_message_family_audit` (114
rows). Fresh route-shape split for that bucket is pure `Popup` 51 families /
323 text constructions, pure `MessageFrame` 20 / 87, pure `Does` 8 / 82,
`AddPlayerMessage+Popup` 5 / 69, `Does+Popup` 5 / 60, `Does+EmitMessage` 4 /
57, and pure `AddPlayerMessage` 8 / 55. The next large tranche should continue
with the remaining pure `Popup` rows, led by `ObjectFinder.ConfigFilters`,
`TinkeringHelpers.CheckMakersMark`, `XRLCore.RestoreModsLoadedAsync`,
`PopulationManager.WishFindBlueprint`, and `Shrine.DesecrateShrine`.

The residual UI/picker pure Popup runtime tranche moves 18 UI screen, picker,
and config families / 114 text constructions from `producer_message_family_audit`
to runtime-required. The tranche covers `ObjectFinder.ConfigFilters`,
`EquipmentAPI.ShowInventoryActionMenu`,
`QudBuildLibraryModuleWindow.HandleMenuOption`, `QudBuildLibraryModuleWindow.AddBuild`,
`QudBuildLibraryModuleWindow.onSelect`, `EquipmentScreen.ShowBodypartEquipUI`,
`InventoryAndEquipmentStatusScreen.HandleShowOptions`,
`FactionsStatusScreen.HandleCmdOptions`, `CommandBindingManager.RestoreDefaults`,
`AbilityManagerScreen.showScreen`, `EmbarkBuilder.checkStateAsync`,
`QudBuildSummaryModuleWindow.HandleMenuOption`, `ModManagerUI.OnCancel`,
`QudMutationsModuleWindow.HandleMenuOption`,
`QudMutationsModuleWindow.SelectVariant`, `FrameworkSearchInput.ChangeValue`,
`OptionsScreen.HandleMenuOption`, and `Gender.CustomizeProcess`. The common
route shape is UI-owned Popup/PickOption/AskString/keybinding/menu-option
surface generation that mixes fixed leaves with dynamic player input, build
names, ability names, inventory action displays, classifier names, or rendered
screen state. Some of these leaves may later be promotion-only with focused
owner tests, but this tranche keeps them runtime-required to avoid claiming
whole static families from generic popup coverage alone. No translations were
added.
This leaves 522 residual rows: 198 child-issue-needed residuals and 324
runtime-evidence residuals. Policy counts are now
`covered_by_owner_route=2120` rows / `16741` text constructions,
`action_required=193` / `1305`, and `runtime_required=329` / `1602`. The
largest remaining action bucket is `producer_message_family_audit` (96 rows).
Fresh route-shape split for that bucket is pure `Popup` 33 families / 209 text
constructions, pure `MessageFrame` 20 / 87, pure `Does` 8 / 82,
`AddPlayerMessage+Popup` 5 / 69, `Does+Popup` 5 / 60, `Does+EmitMessage` 4 /
57, and pure `AddPlayerMessage` 8 / 55. Read-only agent review flags a likely
next large tranche in gameplay/world-owned pure Popup producers, around 100+
text constructions, with promotion-only candidates such as
`TinkeringHelpers.CheckMakersMark`, `Shrine.DesecrateShrine`,
`CyberneticsTerminal2.AskLowLevelHack`, and `CodaSystem.EndGamePrompt` needing
focused owner-route evidence before promotion.

The residual pure Popup remainder runtime tranche moves the remaining 33 pure
Popup families / 209 text constructions from `producer_message_family_audit`
to runtime-required. This clears the pure `Popup` shape from that bucket. The
runtime-required set includes gameplay/world prompts, system and mod/save error
paths, wish/debug routes, conversation reward/share routes, confirmation-token
AskString routes, object-name composition, and generic extension sinks. Fixed
leaves already exist for some prompts such as maker's marks, desecration
liquids, rewards, grip style, low-level hack confirmation, disguise creation,
and some ark/cybernetics messages, but the whole static families still need
route-specific focused owner tests before they can be promoted without
overclaiming adjacent generated branches. No translations or C# behavior
changes were added in this tranche.
This leaves 522 residual rows: 165 child-issue-needed residuals and 357
runtime-evidence residuals. Policy counts are now
`covered_by_owner_route=2120` rows / `16741` text constructions,
`action_required=160` / `1096`, and `runtime_required=362` / `1811`. The
largest remaining action bucket is `producer_message_family_audit` (63 rows).
Fresh route-shape split for that bucket is pure `MessageFrame` 20 families /
87 text constructions, pure `Does` 8 / 82, `AddPlayerMessage+Popup` 5 / 69,
`Does+Popup` 5 / 60, `Does+EmitMessage` 4 / 57, and pure `AddPlayerMessage`
8 / 55. The next large tranche should target pure `MessageFrame` plus pure
`Does`, or the `AddPlayerMessage+Popup` mixed shape if it can be safely split.

The residual pure MessageFrame / pure Does split tranche processes 28 families
/ 169 text constructions from `producer_message_family_audit`. It promotes 15
families / 73 text constructions through existing MessageFrame, death-reason,
cybernetics-terminal, neutron-flux, and conversation reward evidence:
`Physics.UpdateTemperature`, `CyberneticsScreenMainMenu.CyberneticsScreenMainMenu`,
`LootOnStep.SteppedOn`, `NeutronFluxContainment.GetWarningMessage`,
`ExtradimensionalHunterSummoner.Summon`, `Combat.SwoopAttack`,
`Shrine.PerformDesecration`, `PsychicHunterSystem.PsychicPresenceMessage`,
`Skills.WishSkillAdd`, `Skills.WishSkillAll`,
`ConversationDelegates.AwardXP`, `SpiderWebs.HandleEvent(LeftCellEvent)`,
`BaetylHostility.CheckBaetylHostility`, `Mutations.WishMutationAdd`, and
`GameObjectBaetylUnit.GiveRewards`. It moves 13 families / 96 text
constructions to runtime-required: `GameText.RoughConvertSecondPersonToThirdPerson`,
`Domination.ProcessTarget`, `TrembleEarthquakes.RocksFall`,
`CyberneticsTerminal2.AttemptInterface`, `PetFrondzie.taunt`,
`SpaceTimeVortex.SpaceTimeAnomalyPeriodicEvents`,
`LiquidVolume.CleaningMessage`, `LiquidVolume.ProcessContact`,
`PetEbenshabat.HandleEvent`, `AIBarathrumShuttle.ActionShipLaunch`,
`CyberneticsPrecisionForceLathe.HandleEvent(ReplaceThrownWeaponEvent)`,
`HeatSelfOnFreeze.FireEvent`, and `NephalProperties.AbsorbChords`. No
translations or C# behavior changes were added; the promoted side is
promotion-only through existing focused tests and dictionary leaves, while the
runtime side needs live owner-route proof for generated object/liquid names,
conversion helpers, death reasons, wish/debug commands, or UsePopup
MessageFrame branches.
This leaves 507 residual rows: 137 child-issue-needed residuals and 370
runtime-evidence residuals. Policy counts are now
`covered_by_owner_route=2135` rows / `16814` text constructions,
`action_required=132` / `927`, and `runtime_required=375` / `1907`. The
largest remaining action bucket is `producer_message_family_audit` (35 rows).
Fresh route-shape split for that bucket is `AddPlayerMessage+Popup` 5 families
/ 69 text constructions, `Does+Popup` 5 / 60, `Does+EmitMessage` 4 / 57, pure
`AddPlayerMessage` 8 / 55, `EmitMessage+Popup` 2 / 24, and
`AddPlayerMessage+Does` 2 / 21. Pure `Popup`, pure `MessageFrame`, and pure
`Does` are now cleared from `producer_message_family_audit`.

The residual mixed popup runtime tranche moves 10 mixed `AddPlayerMessage+Popup`
and `Does+Popup` families / 129 text constructions from
`producer_message_family_audit` to runtime-required. It covers
`SunderMind.Tick`, `Stomach.FireEvent`, `ElevatorSwitch.FireEvent`,
`BiomeManager.DisplaySurfaceDistribution`, `GiantClamProperties.TeleportFromClamWorld`,
`Examiner.HandleEvent(InventoryActionEvent)`, `TinkerItem.HandleEvent`,
`FixitSpray.HandleEvent`, `MagnetizedApplicator.HandleEvent`, and
`VehicleMeleeInfiltration.TryInfiltrate`. These families mix queue and popup
sinks, inventory-action prompts, generated object names, debug output, or
Does-based composition, so whole-family static promotion would overclaim
adjacent branches. No translations or C# behavior changes were added.
This leaves 507 residual rows: 127 child-issue-needed residuals and 380
runtime-evidence residuals. Policy counts are now
`covered_by_owner_route=2135` rows / `16814` text constructions,
`action_required=122` / `798`, and `runtime_required=385` / `2036`. The largest
remaining action bucket is `producer_message_family_audit` (25 rows). Fresh
route-shape split for that bucket is `Does+EmitMessage` 4 families / 57 text
constructions, pure `AddPlayerMessage` 8 / 55, `EmitMessage+Popup` 2 / 24,
`AddPlayerMessage+Does` 2 / 21, `EmitMessage+MessageFrame+Popup` 1 / 17, and
pure `EmitMessage` 4 / 15.

The residual queue / Does runtime tranche moves pure `AddPlayerMessage` 8
families / 55 text constructions and `Does+EmitMessage` 4 families / 57 text
constructions from `producer_message_family_audit` to runtime-required: 12
families / 112 text constructions total. It covers
`CyberneticsButcherableCybernetic.AttemptButcher`, `Chat.PerformChat`,
`FungalInfection.FireEvent`,
`VehicleMeleeInfiltration.HandleEvent(CanEnterInteriorEvent)`,
`DanceRitualOpponent.HandleEvent(BeforeAITakingActionEvent)`,
`PlayerDanceRitual.FireEvent`, `DanceRitualOpponent.Register`,
`SoundManager._PlaySound`, `SoundManager._PlayWorldSound`,
`Interior.HandleEvent(TookDamageEvent)`, `MessageQueue.AddPlayerMessage`, and
`FindASiteDynamicQuestManager.DynamicQuestWhere`. Existing owner patches cover
adjacent dance and sound routes, but these residual families are different
owner methods or generic/debug queue diagnostics. The static rows mix generated
object names, chat data, sound-log diagnostics, generic queue helpers, and
Does-based emitted world messages, so they need runtime route evidence before
promotion. No translations or C# behavior changes were added.
This leaves 507 residual rows: 115 child-issue-needed residuals and 392
runtime-evidence residuals. Policy counts are now
`covered_by_owner_route=2135` rows / `16814` text constructions,
`action_required=110` / `686`, and `runtime_required=397` / `2148`. The largest
remaining action bucket is `producer_message_family_audit` (13 rows). Fresh
route-shape split for that bucket is `EmitMessage+Popup` 2 families / 24 text
constructions, `EmitMessage+MessageFrame+Popup` 1 / 17,
`Does+EmitMessage+Popup` 1 / 12, `AddPlayerMessage+Does` 2 / 21,
`Does+EmitMessage+MessageFrame` 1 / 8, pure `EmitMessage` 4 / 15,
pure `Popup` 1 / 10, and `TutorialManagerPopup` 1 / 2. The next tranche should
clear the remaining heterogeneous EmitMessage/mixed shapes together, then move
to non-`producer_message_family_audit` residual buckets.

The residual message-family mixed remainder tranche clears the final
`producer_message_family_audit` entries by moving 13 heterogeneous families /
109 text constructions to runtime-required. It covers
`ShevaStarshipControl.CheckTimer`, `SpaceTimeVortex.ApplyVortex`,
`Carapace.Loosen`, `Physics.HandleEvent(ObjectEnteringCellEvent)`,
`GolemQuestMound.DisplayOptions`, `ThiefBot.FireEvent`,
`Campfire.Extinguish`, `CyberneticsHolographicVisage.SelectVisage`,
`LiquidWarmStatic.WishWarmEffectSpec`,
`LiquidWarmStatic.GlitchLiquidComponents`, `LiquidWarmStatic.WishWarmEffect`,
`DesalinationPellet.HandleEvent(InventoryActionEvent)`, and
`FadeText.Update`. Existing patches cover only adjacent subroutes for some of
these names, such as SpaceTimeVortex popup branches, Carapace popup branches,
DesalinationPellet popup composition, LiquidWarmStatic skill/mutation glitches,
ThiefBot fixed queue leaves, and campfire cooking/nostrum routes. The remaining
families combine EmitMessage, popup, MessageFrame, AddPlayerMessage, Does,
tutorial popup, wish/debug, object-name, and generated world-message branches,
so they need runtime route evidence before whole-family promotion. No
translations or C# behavior changes were added.
This leaves 507 residual rows: 102 child-issue-needed residuals and 405
runtime-evidence residuals. Policy counts are now
`covered_by_owner_route=2135` rows / `16814` text constructions,
`action_required=97` / `577`, and `runtime_required=410` / `2257`.
`producer_message_family_audit` is now cleared. The next large target should be
the non-message `sifrah_description_route_split` bucket (42 rows), followed by
`sifrah_popup_route_split` (29 rows) or `generated_display_name_child_issue`
(14 rows).

The Sifrah route-split runtime tranche keeps the existing Sifrah bucket names
but changes the remaining description/popup route-split disposition to
runtime-required under #719. It covers `sifrah_description_route_split` 42 rows
and `sifrah_popup_route_split` 29 rows, 71 rows / 161 text constructions total.
`PsychicCombatSifrah.PsychicCombatSifrah` was already runtime-required from
prior evidence, so the count delta for this tranche is 70 rows / 89 text
constructions moved out of `action_required`. The residual Sifrah set combines
constructor text, token descriptions, `GetDescription` returns,
`CheckOutOfOptions` / result popups, token-use popups, and generated
object/secret/faction/item/liquid slots; exact owner-route Sifrah overlays still
win first, and the remaining route-split rows require runtime route evidence.
No translations or C# behavior changes were added.
This leaves 507 residual rows: 31 child-issue-needed residuals and 476
runtime-evidence residuals. Policy counts are now
`covered_by_owner_route=2135` rows / `16814` text constructions,
`action_required=27` / `488`, and `runtime_required=480` / `2346`. The remaining
child-needed buckets are `producer_broad_route_split` 10 rows,
`generated_display_name_child_issue` 14 rows, `misc_route_split` 4 rows,
`ui_popup_sink_route_split` 2 rows, and `active_effect_popup_route_split` 1 row.

The final child-bucket runtime tranche moves the remaining 31 child-needed
residual rows / 488 text constructions to runtime-required under #719. This
covers `producer_broad_route_split` 10 rows, `generated_display_name_child_issue`
14 rows, `misc_route_split` 4 rows, `ui_popup_sink_route_split` 2 rows, and
`active_effect_popup_route_split` 1 row. These are broad owner or sink route
families, generated village/sultan/mural display-name producers, text-filter and
conversation text transforms, popup sink internals, and active-effect popup
branches; none can be safely promoted as covered without runtime route evidence
or a narrower owner implementation. No translations or C# behavior changes were
added.
This clears `action_required`: policy counts are now
`covered_by_owner_route=2135` rows / `16814` text constructions and
`runtime_required=507` rows / `2834` text constructions, with no
`action_required` rows. The residual bucket ledger still has 507 rows, all with
`runtime_evidence_required` disposition.

The deferred heavy gate passed after one allowlist-key repair for the shifted
`SingleCallsiteOwnerPopupTranslationPatch.TryTranslatePhysicsAttackConfirm`
line number in `ColorTagAllowlistCoverageTests`. `just check` now passes:
C# build and 9,216 tests, Python lint, 1,429 Python tests with 1 skipped,
encoding/glossary/pattern-route/XML/token checks, markdown report checks,
localization coverage tests, and basedpyright.

The active-effect display-name reclassification tranche promotes 165
`generated_display_name_runtime` rows / 169 text constructions through existing
active-effect producer inventory evidence. `docs/active-effect-producer-inventory.json`
classifies these DisplayName assignments as 148 `fixed-leaf translated` rows and
17 `generated/composed route translated` rows, with existing route coverage in
`EffectDescriptionPatch`, `EffectDetailsPatch`,
`CharacterStatusScreenHighlightEffectPatch`, `ActiveEffectTextTranslator`,
`ActiveEffectTextTranslatorTests`, `ActiveEffectsOwnerPatchTests`, and
`LocalizationCoverageTests`. This is promotion-only: no translations or C#
behavior changes were added. Policy counts are now
`covered_by_owner_route=2299` rows / `16979` text constructions and
`runtime_required=342` rows / `2665` text constructions.

The same tranche also splits the remaining generated display-name residuals so
the next work can target owner shapes instead of a heterogeneous bucket:
`generated_display_name_world_part_route_split` 17 rows / 82 constructions,
`generated_display_name_child_issue` 14 / 184,
`generated_display_name_cooking_recipe_runtime` 10 / 10,
`generated_display_name_core_runtime` 9 / 36,
`generated_display_name_mutation_route_split` 6 / 25,
`generated_display_name_ui_runtime` 5 / 14, and
`world_zone_display_name_runtime` 1 / 15. The residual ledger after this tranche
is `/tmp/qudjp-issue719-residual-after-displayname-reclassification.json`.
The largest remaining bucket is still `producer_runtime_evidence_required` at
115 rows / 1219 constructions; the largest non-message follow-up is
`sifrah_description_route_split` at 42 rows / 127 constructions.

The producer-runtime reclassification tranche splits that 115-row producer
runtime bucket by broad owner route shape without changing disposition:
`producer_runtime_gameplay_route_split` 70 rows / 762 constructions,
`producer_runtime_ui_route_split` 20 / 188,
`producer_runtime_core_system_route_split` 11 / 131,
`producer_runtime_mutation_route_split` 6 / 84,
`producer_runtime_conversation_route_split` 5 / 26, and
`producer_runtime_api_route_split` 3 / 28. All remain
`runtime_evidence_required`; this tranche is a routing refinement so later work
can target gameplay producers, UI/picker producers, mutation producers, and
system/debug producers separately instead of treating them as one sink-adjacent
bucket. Policy counts are unchanged from the previous tranche:
`covered_by_owner_route=2299` rows / `16979` text constructions and
`runtime_required=342` rows / `2665`. The residual ledger after this tranche is
`/tmp/qudjp-issue719-residual-after-producer-runtime-reclassification.json`.
The next largest homogeneous target is now the gameplay producer split at 70
rows / 762 constructions; if avoiding message producers, the next target remains
`sifrah_description_route_split` at 42 rows / 127 constructions.

The gameplay-producer reclassification tranche further splits that 70-row
gameplay bucket by owner family without changing disposition:
`producer_runtime_world_part_route_split` 42 rows / 480 constructions,
`producer_runtime_inventory_action_route_split` 11 / 129,
`producer_runtime_cybernetics_route_split` 8 / 63,
`producer_runtime_capability_route_split` 3 / 69,
`producer_runtime_liquid_route_split` 3 / 12, and
`producer_runtime_quest_route_split` 3 / 9. All remain
`runtime_evidence_required`. Policy counts are still
`covered_by_owner_route=2299` rows / `16979` text constructions and
`runtime_required=342` rows / `2665`. The residual ledger after this tranche is
`/tmp/qudjp-issue719-residual-after-gameplay-producer-reclassification.json`.
The next largest buckets are now tied by row count:
`producer_runtime_world_part_route_split` 42 rows / 480 constructions and
`sifrah_description_route_split` 42 rows / 127 constructions.

The world-part producer reclassification tranche splits the 42-row world-part
bucket by sink/producer shape: `producer_runtime_world_part_mixed_route_split`
17 rows / 230 constructions, `producer_runtime_world_part_popup_route_split`
11 / 81, `producer_runtime_world_part_message_frame_route_split` 10 / 124,
and `producer_runtime_world_part_queue_route_split` 4 / 45. All remain
`runtime_evidence_required`. Policy counts are still
`covered_by_owner_route=2299` rows / `16979` text constructions and
`runtime_required=342` rows / `2665`. The residual ledger after this tranche is
`/tmp/qudjp-issue719-residual-after-world-part-producer-reclassification.json`.
The largest remaining row bucket is now `sifrah_description_route_split` at 42
rows / 127 constructions; the largest world-part follow-up by construction count
is `producer_runtime_world_part_mixed_route_split` at 17 rows / 230
constructions.

The Sifrah description static reclassification tranche answers the route-evidence
question by splitting the old `sifrah_description_route_split` bucket instead of
keeping all 42 rows as runtime-required. Static source evidence separates the
remaining rows into `sifrah_description_token_dynamic_constructor_gap` 32 rows /
34 constructions, `sifrah_description_token_getdescription_gap` 8 / 8, and
`sifrah_description_unused_base_game_runtime` 2 / 85. The first two are now
`likely_implementation_gap`: they are exact Sifrah token owner shapes with
dynamic quantities, item/liquid/bit/faction/scan slots, or `[have N]`
description returns. Only the two unused base-game Sifrah constructors remain
`runtime_evidence_required`. The residual ledger after this tranche is
`/tmp/qudjp-issue719-residual-after-sifrah-description-static-reclassification.json`.
Policy counts are now `covered_by_owner_route=2299` rows / `16979` text
constructions, `action_required=40` rows / `42` constructions, and
`runtime_required=302` rows / `2623` constructions. The highest-value
implementation tranche is the 32-row dynamic Sifrah token constructor bucket,
followed by the 8-row Sifrah token `GetDescription` bucket; the next runtime
classification bucket by row count is `ui_description_assignment_runtime` at 30
rows / 82 constructions.

The UI description static reclassification tranche then splits that 30-row
Qud.UI description-assignment bucket by menu-option owner shape:
`ui_menu_option_static_description_gap` 23 rows / 69 constructions and
`ui_options_control_description_gap` 7 rows / 13 constructions. Both are
`likely_implementation_gap`, not runtime-only, because the decompiled source
shows fixed `MenuOption.Description` assignments in line/static option fields
or options controls (`Toggle Option`, `Change Value`, `Save`, `Cancel`, and
combo-box display choices). The XRL.UI sink fallback remains separate as
`ui_popup_sink_route_split`. The residual ledger after this tranche is
`/tmp/qudjp-issue719-residual-after-ui-description-static-reclassification.json`.
Policy counts are now `covered_by_owner_route=2299` rows / `16979` text
constructions, `action_required=70` rows / `124` constructions, and
`runtime_required=272` rows / `2541` constructions. The largest action bucket is
still `sifrah_description_token_dynamic_constructor_gap` at 32 rows; the next
runtime classification bucket by row count is `sifrah_popup_route_split` at 29
rows / 34 constructions, followed by `ui_screen_route_runtime` at 26 / 69.

The Sifrah popup static reclassification tranche then splits the 29-row
`sifrah_popup_route_split` bucket by exact owner method shape. Static source
evidence separates it into `sifrah_popup_check_out_of_options_gap` 8 rows / 8
constructions, `sifrah_popup_result_owner_gap` 12 / 12,
`sifrah_popup_token_check_use_gap` 6 / 6,
`sifrah_popup_secret_use_token_gap` 1 / 6, and
`sifrah_popup_hacking_partial_success_gap` 1 / 1. These 28 rows / 33
constructions are now `likely_implementation_gap`: the popup producer is a fixed
owner method, not sink-only runtime evidence. Only
`PsychicCombatSifrah.CheckOutOfOptions` remains
`sifrah_popup_unused_base_game_runtime` because the decompiled class is marked
unused in the base game. The residual ledger after this tranche is
`/tmp/qudjp-issue719-residual-after-sifrah-popup-static-reclassification.json`.
Policy counts are now `covered_by_owner_route=2299` rows / `16979` text
constructions, `action_required=98` rows / `157` constructions, and
`runtime_required=244` rows / `2508` constructions. The largest implementation
bucket remains `sifrah_description_token_dynamic_constructor_gap` at 32 rows;
the next runtime classification bucket by row count is
`ui_screen_route_runtime` at 26 rows / 69 constructions.

The UI screen static reclassification tranche then splits the 26-row
`ui_screen_route_runtime` bucket by Qud.UI owner shape. Fixed label owners
(`SkillsAndPowersStatusScreen.ShowScreen` and `KeybindBox.Update`) are now
`ui_screen_fixed_label_gap` with 2 rows / 11 constructions and disposition
`likely_implementation_gap`. The remaining rows stay runtime-required but are no
longer one broad bucket: `ui_screen_trade_inventory_runtime` 11 rows / 21
constructions, `ui_screen_data_bound_runtime` 7 / 13,
`ui_screen_options_control_runtime` 4 / 10,
`ui_screen_world_generation_runtime` 1 / 9, and
`ui_screen_popup_message_runtime` 1 / 5. The residual ledger after this tranche
is `/tmp/qudjp-issue719-residual-after-ui-screen-static-reclassification.json`.
Policy counts are now `covered_by_owner_route=2299` rows / `16979` text
constructions, `action_required=100` rows / `168` constructions, and
`runtime_required=242` rows / `2497` constructions. The largest implementation
bucket remains `sifrah_description_token_dynamic_constructor_gap` at 32 rows;
the largest remaining runtime row buckets are `producer_runtime_ui_route_split`
and `tutorial_popup_runtime`, both at 20 rows.

The UI producer reclassification tranche splits the 20-row
`producer_runtime_ui_route_split` popup bucket without changing disposition.
The new runtime-required route buckets are
`producer_runtime_ui_options_popup_route_split` 3 rows / 75 constructions,
`producer_runtime_ui_chargen_popup_route_split` 8 / 42,
`producer_runtime_ui_inventory_trade_popup_route_split` 3 / 42,
`producer_runtime_ui_status_popup_route_split` 3 / 21,
`producer_runtime_ui_misc_popup_route_split` 2 / 6, and
`producer_runtime_ui_tutorial_popup_route_split` 1 / 2. The residual ledger
after this tranche is
`/tmp/qudjp-issue719-residual-after-ui-producer-reclassification.json`. Policy
counts are unchanged from the UI screen split:
`covered_by_owner_route=2299` rows / `16979` text constructions,
`action_required=100` rows / `168` constructions, and `runtime_required=242`
rows / `2497` constructions. The next largest runtime bucket by row count is
`tutorial_popup_runtime` at 20 rows / 445 constructions.

The tutorial popup static reclassification tranche splits the 20-row
`tutorial_popup_runtime` bucket and moves it to implementation-gap work. Static
JoppaTutorial source review shows fixed tutorial-step producer owners rather
than a route that requires live evidence to classify. The new buckets are
`tutorial_lateupdate_popup_gap` 8 rows / 373 constructions,
`tutorial_command_guard_popup_gap` 4 / 56,
`tutorial_cell_guard_popup_gap` 4 / 7, `tutorial_trigger_popup_gap` 2 / 5, and
`tutorial_seen_popup_gap` 2 / 4. The existing `TutorialManager*TranslationPatch`
family still proves the sink API behavior, but these residual rows remain
producer-owner implementation candidates, not covered owner-route rows. The
residual ledger after this tranche is
`/tmp/qudjp-issue719-residual-after-tutorial-popup-static-reclassification.json`.
Policy counts are now `covered_by_owner_route=2299` rows / `16979` text
constructions, `action_required=120` rows / `613` constructions, and
`runtime_required=222` rows / `2052` constructions. The largest implementation
bucket remains `sifrah_description_token_dynamic_constructor_gap` at 32 rows;
the largest remaining runtime row buckets are `generated_display_name_world_part_route_split`
and `producer_runtime_world_part_mixed_route_split`, both at 17 rows.

The world-part mixed producer reclassification tranche splits the 17-row
`producer_runtime_world_part_mixed_route_split` bucket by exact surface set
without changing disposition. The new runtime-required route buckets include
`producer_runtime_world_part_queue_popup_route_split` 4 rows / 44 constructions,
`producer_runtime_world_part_does_emit_route_split` 3 / 33,
`producer_runtime_world_part_does_emit_message_frame_route_split` 2 / 47,
`producer_runtime_world_part_queue_does_route_split` 2 / 21, and six single-row
mixed surface buckets for Does-only, Does+Popup, Does+MessageFrame,
EmitMessage+Popup, MessageFrame+Popup, and EmitMessage+MessageFrame+Popup. The
residual ledger after this tranche is
`/tmp/qudjp-issue719-residual-after-world-part-mixed-reclassification.json`.
Policy counts are unchanged from the tutorial split:
`covered_by_owner_route=2299` rows / `16979` text constructions,
`action_required=120` rows / `613` constructions, and `runtime_required=222`
rows / `2052` constructions. The largest remaining runtime row bucket is
`generated_display_name_world_part_route_split` at 17 rows / 82 constructions.

The world-part display-name reclassification tranche splits the 17-row
`generated_display_name_world_part_route_split` bucket by owner shape. Fixed
leaf/world-part state rows are now implementation-gap work:
`generated_display_name_world_part_fixed_leaf_gap` 5 rows / 43 constructions
and `generated_display_name_stat_shift_gap` 2 / 3. Generated object/name routes
remain runtime-required but narrower:
`generated_display_name_world_part_generated_object_runtime` 6 rows / 30
constructions, `generated_display_name_world_part_cybernetics_runtime` 3 / 4,
and `generated_display_name_world_part_item_mod_runtime` 1 / 2. The residual
ledger after this tranche is
`/tmp/qudjp-issue719-residual-after-world-part-displayname-reclassification.json`.
Policy counts are now `covered_by_owner_route=2299` rows / `16979` text
constructions, `action_required=127` rows / `659` constructions, and
`runtime_required=215` rows / `2006` constructions. The largest implementation
bucket remains `sifrah_description_token_dynamic_constructor_gap` at 32 rows;
the largest remaining runtime row bucket is `generated_display_name_child_issue`
at 14 rows / 184 constructions.

The generated display-name child reclassification tranche splits the 14-row
`generated_display_name_child_issue` bucket by generated-data owner shape
without changing disposition. The new runtime-required buckets are
`generated_display_name_mural_runtime` 5 rows / 77 constructions,
`generated_display_name_sultan_entity_runtime` 3 / 33,
`generated_display_name_village_signature_dish_runtime` 2 / 28,
`generated_display_name_village_signature_item_runtime` 2 / 10,
`generated_display_name_village_faction_runtime` 1 / 24, and
`generated_display_name_village_dynamic_quest_reward_runtime` 1 / 12. Static
source review shows these rows compose from sultan/village history snapshots,
mural events, signature dish/item generation, or quest reward object names; no
exact current owner patch or fixed leaf closes them safely. The residual ledger
after this tranche is
`/tmp/qudjp-issue719-residual-after-generated-displayname-child-reclassification.json`.
Policy counts remain `covered_by_owner_route=2299` rows / `16979` text
constructions, `action_required=127` rows / `659` constructions, and
`runtime_required=215` rows / `2006` constructions. The largest implementation
bucket remains `sifrah_description_token_dynamic_constructor_gap` at 32 rows;
the largest remaining runtime row buckets by row count are
`producer_runtime_core_system_route_split`, `producer_runtime_inventory_action_route_split`,
`producer_runtime_world_part_popup_route_split`, and
`ui_screen_trade_inventory_runtime`, each at 11 rows.

The InventoryActionEvent producer reclassification tranche splits the 11-row
`producer_runtime_inventory_action_route_split` bucket by sink/producer surface
shape without changing disposition. The new runtime-required buckets are
`producer_runtime_inventory_action_popup_route_split` 5 rows / 66
constructions, `producer_runtime_inventory_action_does_popup_route_split` 4 /
52, `producer_runtime_inventory_action_message_frame_popup_route_split` 1 / 8,
and `producer_runtime_inventory_action_emit_route_split` 1 / 3. This separates
pure popup handlers such as `Crayons`, `Description`, `Inventory`, `Vehicle`,
and `IGrenade` from Does+Popup handlers (`Examiner`, `TinkerItem`,
`FixitSpray`, `MagnetizedApplicator`), the Brain MessageFrame+Popup route, and
the DesalinationPellet EmitMessage route. The residual ledger after this
tranche is
`/tmp/qudjp-issue719-residual-after-inventory-action-runtime-reclassification.json`.
Policy counts remain `covered_by_owner_route=2299` rows / `16979` text
constructions, `action_required=127` rows / `659` constructions, and
`runtime_required=215` rows / `2006` constructions. The largest implementation
bucket remains `sifrah_description_token_dynamic_constructor_gap` at 32 rows;
the largest remaining runtime row buckets by row count are
`producer_runtime_core_system_route_split`, `producer_runtime_world_part_popup_route_split`,
and `ui_screen_trade_inventory_runtime`, each at 11 rows.

The world-part popup reclassification tranche splits the 11-row
`producer_runtime_world_part_popup_route_split` bucket by static owner shape.
Seven rows / 58 constructions move to implementation-gap work:
`producer_runtime_world_part_tinkering_popup_gap` 1 row / 15 constructions,
`producer_runtime_world_part_shrine_popup_gap` 1 / 13,
`producer_runtime_world_part_disguise_popup_gap` 1 / 9,
`producer_runtime_world_part_ship_ark_popup_gap` 2 / 13, and
`producer_runtime_world_part_grip_recoil_popup_gap` 2 / 8. These rows have
fixed popup prompts or confirmation text in exact owner methods. Four rows / 23
constructions remain runtime-required but narrower:
`producer_runtime_world_part_golem_popup_runtime` 1 / 10,
`producer_runtime_world_part_movement_popup_runtime` 1 / 9, and
`producer_runtime_world_part_wish_debug_popup_runtime` 2 / 4. The residual
ledger after this tranche is
`/tmp/qudjp-issue719-residual-after-world-part-popup-reclassification.json`.
Policy counts are now `covered_by_owner_route=2299` rows / `16979` text
constructions, `action_required=134` rows / `717` constructions, and
`runtime_required=208` rows / `1948` constructions. The largest implementation
bucket remains `sifrah_description_token_dynamic_constructor_gap` at 32 rows;
the largest remaining runtime row buckets by row count are
`producer_runtime_core_system_route_split` and `ui_screen_trade_inventory_runtime`,
both at 11 rows.

The core/system producer reclassification tranche splits the 11-row
`producer_runtime_core_system_route_split` bucket by exact owner shape. Three
rows / 37 constructions move to implementation-gap work:
`producer_runtime_core_mod_config_popup_gap` 1 row / 15 constructions,
`producer_runtime_core_mod_failure_popup_gap` 1 / 12, and
`producer_runtime_core_coda_endgame_popup_gap` 1 / 10. These rows have fixed
popup prompts, options, or confirmation frames in exact core owner methods.
Eight rows / 94 constructions remain runtime-required but narrower:
`producer_runtime_core_scores_popup_runtime` 1 / 46,
`producer_runtime_core_game_text_does_runtime` 1 / 24,
`producer_runtime_core_population_wish_popup_runtime` 2 / 14,
`producer_runtime_core_sound_debug_queue_runtime` 2 / 8, and
`producer_runtime_core_generic_sink_runtime` 2 / 2. The residual ledger after
this tranche is
`/tmp/qudjp-issue719-residual-after-core-system-reclassification.json`.
Policy counts are now `covered_by_owner_route=2299` rows / `16979` text
constructions, `action_required=137` rows / `754` constructions, and
`runtime_required=205` rows / `1911` constructions. The largest implementation
bucket remains `sifrah_description_token_dynamic_constructor_gap` at 32 rows;
the largest remaining runtime row bucket by row count is
`ui_screen_trade_inventory_runtime` at 11 rows.

The trade/inventory UI reclassification tranche splits the 11-row
`ui_screen_trade_inventory_runtime` bucket by widget and text shape without
changing disposition. The new runtime-required buckets are
`ui_screen_trade_highlight_runtime` 1 row / 7 constructions,
`ui_screen_hotkey_control_runtime` 2 / 6,
`ui_screen_trade_drag_numeric_runtime` 4 / 4,
`ui_screen_status_stat_runtime` 2 / 2,
`ui_screen_inventory_drag_numeric_runtime` 1 / 1, and
`ui_screen_progress_numeric_runtime` 1 / 1. Static source review shows these
routes are item-detail display-name/weight/price fields, key-only hotkey
labels, numeric drag/count indicators, stat abbreviations, and numeric
progress text; no fixed owner translation leaf or existing owner patch closes
them safely without runtime UI evidence. The residual ledger after this tranche
is
`/tmp/qudjp-issue719-residual-after-ui-trade-inventory-reclassification.json`.
Policy counts remain `covered_by_owner_route=2299` rows / `16979` text
constructions, `action_required=137` rows / `754` constructions, and
`runtime_required=205` rows / `1911` constructions. The largest implementation
bucket remains `sifrah_description_token_dynamic_constructor_gap` at 32 rows;
the largest remaining runtime row buckets by row count now include
`producer_broad_route_split`, `producer_runtime_world_part_message_frame_route_split`,
and `generated_display_name_cooking_recipe_runtime` at 10 rows each.

The broad producer reclassification tranche splits the 10-row
`producer_broad_route_split` bucket by exact `GameObject` / `MissileWeapon`
member shape. Five rows / 97 constructions move to implementation-gap work:
`producer_broad_gameobject_inventory_companion_gap` 1 row / 51 constructions,
`producer_broad_gameobject_destroy_gap` 1 / 22,
`producer_broad_gameobject_pulldown_gap` 1 / 15,
`producer_broad_gameobject_explode_death_gap` 1 / 7, and
`producer_broad_gameobject_replace_cell_gap` 1 / 2. These rows have exact
owner methods for companion command prompts, destroy/companion-death text,
pulldown destination labels, explosion death reasons, and cell-slotting popup
text. Five rows / 177 constructions remain runtime-required but narrower:
`producer_broad_gameobject_autoequip_runtime` 1 / 72,
`producer_broad_missile_trajectory_message_runtime` 1 / 45,
`producer_broad_gameobject_death_runtime` 1 / 44,
`producer_broad_gameobject_regenera_runtime` 1 / 13, and
`producer_broad_gameobject_hostile_spot_runtime` 1 / 3. The residual ledger
after this tranche is
`/tmp/qudjp-issue719-residual-after-broad-producer-reclassification.json`.
Policy counts are now `covered_by_owner_route=2299` rows / `16979` text
constructions, `action_required=142` rows / `851` constructions, and
`runtime_required=200` rows / `1814` constructions. The largest implementation
bucket remains `sifrah_description_token_dynamic_constructor_gap` at 32 rows;
the largest remaining runtime row buckets by row count are
`producer_runtime_world_part_message_frame_route_split` and
`generated_display_name_cooking_recipe_runtime` at 10 rows each.

The world-part MessageFrame reclassification tranche splits the 10-row
`producer_runtime_world_part_message_frame_route_split` bucket by exact owner
method and moves all rows to implementation-gap work. The new buckets are
`producer_runtime_world_part_pseudopod_death_frame_gap` 2 rows / 83
constructions, `producer_runtime_world_part_pet_taunt_frame_gap` 1 / 8,
`producer_runtime_world_part_vortex_periodic_frame_gap` 1 / 8,
`producer_runtime_world_part_liquid_cleaning_frame_gap` 1 / 7,
`producer_runtime_world_part_liquid_contact_frame_gap` 1 / 5,
`producer_runtime_world_part_pet_recipe_frame_gap` 1 / 4,
`producer_runtime_world_part_shuttle_frame_gap` 1 / 3,
`producer_runtime_world_part_heat_self_frame_gap` 1 / 3, and
`producer_runtime_world_part_nephal_absorb_frame_gap` 1 / 3. Static source
review shows exact MessageFrame owners for pseudopod death explosions,
Frondzie taunts, vortex emergence/destabilization frames, LiquidVolume
cleaning/contact frames, recipe teaching, shuttle launch interaction, warming,
and Nephal chord absorption. These rows no longer need live runtime evidence to
identify owner route, but they still need route-specific implementations,
translation capture tests, or promotion evidence before closure. The residual
ledger after this tranche is
`/tmp/qudjp-issue719-residual-after-world-part-message-frame-reclassification.json`.
Policy counts are now `covered_by_owner_route=2299` rows / `16979` text
constructions, `action_required=152` rows / `975` constructions, and
`runtime_required=190` rows / `1690` constructions. The largest implementation
bucket remains `sifrah_description_token_dynamic_constructor_gap` at 32 rows;
the largest remaining runtime row bucket by row count is
`generated_display_name_cooking_recipe_runtime` at 10 rows.

The cooking preset display-name reclassification tranche moves the 10-row
`generated_display_name_cooking_recipe_runtime` bucket to a fixed-owner
implementation gap:
`generated_display_name_cooking_preset_recipe_gap` 10 rows / 10 constructions.
Static source review shows all rows are `CookingRecipe` subclass
`GetDisplayName()` overrides returning fixed white-marked preset meal names
(`AppleMatz`, `BoneBabka`, `CloacaSurprise`, `CrystalDelight`,
`GoatAndSweetLeaf`, `HotandSpiny`, `MahLahSoup`, `MushroomCider`,
`ThePorridge`, and `TongueAndCheek`). The existing generated
`CookingRecipe.GetDisplayName` patch covers generated recipe grammar, but these
override methods need a preset owner route, scoped leaves, or explicit
promotion evidence before closure. The residual ledger after this tranche is
`/tmp/qudjp-issue719-residual-after-cooking-preset-displayname-reclassification.json`.
Policy counts are now `covered_by_owner_route=2299` rows / `16979` text
constructions, `action_required=162` rows / `985` constructions, and
`runtime_required=180` rows / `1680` constructions. The largest implementation
bucket remains `sifrah_description_token_dynamic_constructor_gap` at 32 rows;
the largest remaining runtime row bucket by row count is
`generated_display_name_core_runtime` at 9 rows.

The core display-name reclassification tranche splits the 9-row
`generated_display_name_core_runtime` bucket by core producer shape. Three rows
/ 12 constructions move to implementation-gap work as
`generated_display_name_core_invalid_object_gap`; these are exact invalid
blueprint/cache fallback display names and descriptions in
`GameObjectFactory.CreateObject` and `ZoneManager.GetCachedObjects`. Six rows /
24 constructions remain runtime-required but narrower:
`generated_display_name_core_possessive_runtime` 2 rows / 2 constructions,
`generated_display_name_core_running_behavior_runtime` 1 / 18,
`generated_display_name_core_faction_runtime` 1 / 2, and
`generated_display_name_core_metadata_runtime` 2 / 2. Static source review
shows these remaining runtime buckets are possessive display-name helpers,
event-driven running behavior aggregation, dynamic faction display names, and
base Effect / PointOfInterest metadata fallbacks. The residual ledger after
this tranche is
`/tmp/qudjp-issue719-residual-after-core-displayname-reclassification.json`.
Policy counts are now `covered_by_owner_route=2299` rows / `16979` text
constructions, `action_required=165` rows / `997` constructions, and
`runtime_required=177` rows / `1668` constructions. The largest implementation
bucket remains `sifrah_description_token_dynamic_constructor_gap` at 32 rows;
the largest remaining runtime row buckets by row count are
`producer_runtime_cybernetics_route_split` and
`producer_runtime_ui_chargen_popup_route_split`, both at 8 rows.

The cybernetics producer reclassification tranche splits the 8-row
`producer_runtime_cybernetics_route_split` bucket by exact cybernetics owner
shape and moves all rows to implementation-gap work. The new buckets are
`producer_runtime_cybernetics_butcher_message_gap` 1 row / 24 constructions,
`producer_runtime_cybernetics_force_lathe_activation_gap` 1 / 12,
`producer_runtime_cybernetics_low_level_hack_popup_gap` 1 / 9,
`producer_runtime_cybernetics_holographic_visage_gap` 1 / 6,
`producer_runtime_cybernetics_cathedra_flight_popup_gap` 1 / 4,
`producer_runtime_cybernetics_recoiler_popup_gap` 1 / 3,
`producer_runtime_cybernetics_force_lathe_replace_gap` 1 / 3, and
`producer_runtime_cybernetics_terminal_interface_gap` 1 / 2. Static source
review shows exact owners in `CyberneticsButcherableCybernetic.AttemptButcher`,
`CyberneticsPrecisionForceLathe.ActivatePrecisionForceLathe`,
`CyberneticsPrecisionForceLathe.HandleEvent(ReplaceThrownWeaponEvent)`,
`CyberneticsTerminal2.AskLowLevelHack`,
`CyberneticsTerminal2.AttemptInterface`,
`CyberneticsHolographicVisage.SelectVisage`,
`CyberneticsCathedra.HandleEvent(CommandEvent)`, and
`CyberneticsOnboardRecoilerTeleporter.ActuateTeleport`. These rows use fixed
popup prompts, fixed failure frames, exact `XDidYToZ` / `EmitMessage` frames,
or owner-local cybernetics/faction captures, so live runtime evidence is no
longer needed to identify the owner route. The residual ledger after this
tranche is
`/tmp/qudjp-issue719-residual-after-cybernetics-runtime-reclassification.json`.
Policy counts are now `covered_by_owner_route=2299` rows / `16979` text
constructions, `action_required=173` rows / `1060` constructions, and
`runtime_required=169` rows / `1605` constructions. The largest implementation
bucket remains `sifrah_description_token_dynamic_constructor_gap` at 32 rows;
the largest remaining runtime row bucket by row count is
`producer_runtime_ui_chargen_popup_route_split` at 8 rows.

The chargen popup reclassification tranche splits the 8-row
`producer_runtime_ui_chargen_popup_route_split` bucket by exact chargen owner
shape and moves all rows to implementation-gap work. The new buckets are
`producer_runtime_ui_chargen_build_library_manage_gap` 1 row / 11
constructions, `producer_runtime_ui_chargen_build_library_add_gap` 1 / 7,
`producer_runtime_ui_chargen_gender_customize_gap` 1 / 6,
`producer_runtime_ui_chargen_build_library_import_gap` 1 / 5,
`producer_runtime_ui_chargen_build_summary_gap` 1 / 4,
`producer_runtime_ui_chargen_validation_popup_gap` 1 / 4,
`producer_runtime_ui_chargen_mutation_menu_gap` 1 / 3, and
`producer_runtime_ui_chargen_mutation_variant_gap` 1 / 2. Static source review
shows exact owners in `QudBuildLibraryModuleWindow.HandleMenuOption`,
`QudBuildLibraryModuleWindow.AddBuild`,
`QudBuildLibraryModuleWindow.onSelect`,
`QudBuildSummaryModuleWindow.HandleMenuOption`,
`EmbarkBuilder.checkStateAsync`, `QudMutationsModuleWindow.HandleMenuOption`,
`QudMutationsModuleWindow.SelectVariant`, and `Gender.CustomizeProcess`.
Validation popups are still statically traceable through `DataErrors` /
`DataWarnings` implementations, so this tranche is implementation work rather
than live route discovery. The residual ledger after this tranche is
`/tmp/qudjp-issue719-residual-after-chargen-popup-reclassification.json`.
Policy counts are now `covered_by_owner_route=2299` rows / `16979` text
constructions, `action_required=181` rows / `1102` constructions, and
`runtime_required=161` rows / `1563` constructions. The largest implementation
bucket remains `sifrah_description_token_dynamic_constructor_gap` at 32 rows;
the largest remaining runtime row bucket by row count is
`ui_screen_data_bound_runtime` at 7 rows.

The data-bound UI reclassification tranche splits the 7-row
`ui_screen_data_bound_runtime` bucket by source widget while keeping all rows
runtime-required. The new buckets are `ui_screen_left_side_category_runtime` 1
row / 4 constructions, `ui_screen_mod_manager_back_button_runtime` 1 / 3,
`ui_screen_notification_runtime` 1 / 2,
`ui_screen_cybernetics_terminal_runtime` 2 / 2,
`ui_screen_console_input_runtime` 1 / 1, and
`ui_screen_missile_weapon_status_runtime` 1 / 1. Static source review shows
data-bound category/menu/help/options labels in `LeftSideCategory.setData`,
caller-provided back-button text in `ModManagerUI.SetBackButtonText`, queued
notification title/body text in `Notification.Routine`, debug-console input
reset in `ConsoleWindow.Update`, cybernetics terminal line text in
`CyberneticsTerminalRow`, and missile weapon status text from
`MissileWeaponAreaInfo.UpdateFrom`. These remain runtime-required because the
visible English is supplied by upstream row/status/queue producers rather than
fixed owner leaves in the widget itself. The residual ledger after this tranche
is `/tmp/qudjp-issue719-residual-after-ui-data-bound-reclassification.json`.
Policy counts remain `covered_by_owner_route=2299` rows / `16979` text
constructions, `action_required=181` rows / `1102` constructions, and
`runtime_required=161` rows / `1563` constructions. The largest remaining
runtime row buckets by row count are `generated_display_name_mutation_route_split`,
`generated_display_name_world_part_generated_object_runtime`, and
`producer_runtime_mutation_route_split`, each at 6 rows.

The mutation display-name reclassification tranche splits the 6-row
`generated_display_name_mutation_route_split` bucket by exact mutation owner
shape and moves all rows to implementation-gap work. The new buckets are
`generated_display_name_mutation_base_display_gap` 2 rows / 3 constructions,
`generated_display_name_mutation_temporal_fugue_copy_gap` 1 / 18,
`generated_display_name_mutation_stat_shift_gap` 1 / 2,
`generated_display_name_mutation_light_manipulation_ability_gap` 1 / 1, and
`generated_display_name_mutation_effect_display_gap` 1 / 1. Static source
review shows exact owners in `BaseMutation.GetDisplayName`,
`MutationEntry.GetDisplayName`, `TemporalFugue.CreateFugueCopyOf`,
`PhotosyntheticSkin.CheckCamouflage`, `LightManipulation.SyncAbilityName`, and
`Metamorphed.Metamorphed`. These rows are owner-route implementation or
promotion candidates rather than runtime route discovery. The residual ledger
after this tranche is
`/tmp/qudjp-issue719-residual-after-mutation-displayname-reclassification.json`.
Policy counts are now `covered_by_owner_route=2299` rows / `16979` text
constructions, `action_required=187` rows / `1127` constructions, and
`runtime_required=155` rows / `1538` constructions. The largest remaining
runtime row buckets by row count are
`generated_display_name_world_part_generated_object_runtime` and
`producer_runtime_mutation_route_split`, each at 6 rows.

The world-part generated-object display-name reclassification tranche splits
the 6-row `generated_display_name_world_part_generated_object_runtime` bucket
by exact generated-object owner shape. Five rows / 26 constructions move to
implementation-gap work: `generated_display_name_world_part_figurine_gap` 1
row / 12 constructions, `generated_display_name_world_part_pet_phylactery_gap`
1 / 4, `generated_display_name_world_part_statue_gap` 1 / 4,
`generated_display_name_world_part_hologram_gap` 1 / 3, and
`generated_display_name_world_part_tomb_cultist_gap` 1 / 3. One row / 4
constructions remains runtime-required as
`generated_display_name_world_part_wish_debug_runtime` because
`PointedAsteriskBuilder.AsteriskWish` is a WishCommand-only route. Static
source review shows exact implementable owners in
`RandomFigurine.HandleEvent(ObjectCreatedEvent)`,
`PetPhylactery.HandleEvent(AfterObjectCreatedEvent)`,
`RandomStatue.SetCreature`, `ModQuantumReverb.CreateHologramOf`, and
`TombCultistTemplate.Apply`. The residual ledger after this tranche is
`/tmp/qudjp-issue719-residual-after-world-part-generated-object-reclassification.json`.
Policy counts are now `covered_by_owner_route=2299` rows / `16979` text
constructions, `action_required=192` rows / `1153` constructions, and
`runtime_required=150` rows / `1512` constructions. The largest remaining
runtime row bucket by row count is `producer_runtime_mutation_route_split` at
6 rows.

The mutation producer reclassification tranche splits the 6-row
`producer_runtime_mutation_route_split` bucket by exact mutation owner shape
and moves all rows to implementation-gap work. The new buckets are
`producer_runtime_mutation_sunder_mind_gap` 1 row / 25 constructions,
`producer_runtime_mutation_domination_failure_gap` 1 / 21,
`producer_runtime_mutation_temporal_fugue_gap` 1 / 19,
`producer_runtime_mutation_carapace_loosen_gap` 1 / 12,
`producer_runtime_mutation_base_variant_popup_gap` 1 / 5, and
`producer_runtime_mutation_wings_flight_gap` 1 / 2. Static source review shows
exact owners in `SunderMind.Tick`, `Domination.ProcessTarget`,
`TemporalFugue.PerformTemporalFugue`, `Carapace.Loosen`,
`BaseMutation.SelectVariant`, and `Wings.HandleEvent(CommandEvent)`. These
rows no longer need live runtime evidence to identify owner route; they need
route-specific implementations, translation capture tests, or promotion
evidence. The residual ledger after this tranche is
`/tmp/qudjp-issue719-residual-after-mutation-producer-reclassification.json`.
Policy counts are now `covered_by_owner_route=2299` rows / `16979` text
constructions, `action_required=198` rows / `1237` constructions, and
`runtime_required=144` rows / `1428` constructions. The largest remaining
runtime row buckets by row count are `generated_display_name_mural_runtime`,
`generated_display_name_ui_runtime`, `producer_runtime_conversation_route_split`,
and `producer_runtime_inventory_action_popup_route_split`, each at 5 rows.

The inventory-action popup reclassification tranche splits the 5-row
`producer_runtime_inventory_action_popup_route_split` bucket by exact
`InventoryActionEvent` owner shape and moves all rows to implementation-gap
work. The new buckets are
`producer_runtime_inventory_action_crayons_popup_gap` 1 row / 20 constructions,
`producer_runtime_inventory_action_description_look_popup_gap` 1 / 20,
`producer_runtime_inventory_action_inventory_drop_popup_gap` 1 / 18,
`producer_runtime_inventory_action_vehicle_follower_popup_gap` 1 / 5, and
`producer_runtime_inventory_action_grenade_detonate_popup_gap` 1 / 3. Static
source review shows exact popup owners in `Crayons.HandleEvent`,
`Description.HandleEvent`, `Inventory.HandleEvent`, `Vehicle.HandleEvent`, and
`IGrenade.HandleEvent`. The `Description` row can still require generated
display-name/description capture work, but it no longer needs live runtime
evidence to identify the owning popup route. The residual ledger after this
tranche is
`/tmp/qudjp-issue719-residual-after-inventory-action-popup-reclassification.json`.
Policy counts are now `covered_by_owner_route=2299` rows / `16979` text
constructions, `action_required=203` rows / `1303` constructions, and
`runtime_required=139` rows / `1362` constructions. The largest remaining
runtime row buckets by row count are `generated_display_name_mural_runtime`,
`generated_display_name_ui_runtime`, and
`producer_runtime_conversation_route_split`, each at 5 rows.

The mural display-name reclassification tranche splits the 5-row
`generated_display_name_mural_runtime` bucket by exact mural controller owner
shape and moves all rows to implementation-gap work. The new buckets are
`generated_display_name_mural_blank_slate_gap` 2 rows / 34 constructions,
`generated_display_name_mural_historic_event_gap` 1 row / 17 constructions,
`generated_display_name_mural_ruined_historic_gap` 1 / 15, and
`generated_display_name_mural_player_event_gap` 1 / 11. Static source review
shows exact owners in `PlayerMuralController.blankMural`,
`SultanMuralController.blankMural`,
`SultanMuralController.updateHistoricMural`,
`SultanMuralController.ruinMural`, and
`PlayerMuralController.updatePlayerMural`. The historic/player rows still need
generated sultan/player name and mural inscription handling, but the owner route
is statically identified. The residual ledger after this tranche is
`/tmp/qudjp-issue719-residual-after-mural-displayname-reclassification.json`.
Policy counts are now `covered_by_owner_route=2299` rows / `16979` text
constructions, `action_required=208` rows / `1380` constructions, and
`runtime_required=134` rows / `1285` constructions. The largest remaining
runtime row buckets by row count are `producer_runtime_conversation_route_split`
and `generated_display_name_ui_runtime`, each at 5 rows.

The conversation producer reclassification tranche splits the 5-row
`producer_runtime_conversation_route_split` bucket by exact conversation popup
owner shape and moves all rows to implementation-gap work. The new buckets are
`producer_runtime_conversation_resheph_secret_gap` 1 row / 9 constructions,
`producer_runtime_conversation_endgame_confirm_gap` 1 / 5,
`producer_runtime_conversation_give_artifact_gap` 1 / 5,
`producer_runtime_conversation_water_ritual_secret_gap` 1 / 4, and
`producer_runtime_conversation_api_reward_pick_gap` 1 / 3. Static source
review shows direct popup owners in `GiveReshephSecret.HandleEvent`,
`EndGame.HandleEvent`, `GiveArtifact.HandleEvent`,
`WaterRitualSellSecret.Share`, and `ConversationsAPI.chooseOneItem`. These
rows may need route-local option/name capture translation, but they do not need
runtime evidence to identify the owning producer. The residual ledger after
this tranche is
`/tmp/qudjp-issue719-residual-after-conversation-producer-reclassification.json`.
Policy counts are now `covered_by_owner_route=2299` rows / `16979` text
constructions, `action_required=213` rows / `1406` constructions, and
`runtime_required=129` rows / `1259` constructions. The largest remaining
runtime row bucket by row count is `generated_display_name_ui_runtime` at
5 rows.

The UI display-name reclassification tranche splits the 5-row
`generated_display_name_ui_runtime` bucket by exact UI owner shape and moves
all rows to implementation-gap work. The new buckets are
`generated_display_name_ui_object_finder_context_gap` 2 rows / 2
constructions, `generated_display_name_ui_object_finder_sorter_gap` 2 / 2, and
`generated_display_name_ui_cybernetics_install_gap` 1 row / 10 constructions.
Static source review shows fixed `GetDisplayName` owners in
`AutogotItems`, `NearbyItems`, `IdSorter`, and `ValueSorter`, plus direct
install-option text construction in `CyberneticsScreenInstall.OnUpdate`. The
residual ledger after this tranche is
`/tmp/qudjp-issue719-residual-after-ui-displayname-reclassification.json`.
Policy counts are now `covered_by_owner_route=2299` rows / `16979` text
constructions, `action_required=218` rows / `1420` constructions, and
`runtime_required=124` rows / `1245` constructions. The largest remaining
runtime row buckets by row count are
`producer_runtime_inventory_action_does_popup_route_split`,
`producer_runtime_world_part_queue_route_split`,
`producer_runtime_world_part_queue_popup_route_split`, `misc_route_split`,
`ui_screen_options_control_runtime`, and `ui_screen_trade_drag_numeric_runtime`,
each at 4 rows.

The inventory-action Does+Popup reclassification tranche splits the 4-row
`producer_runtime_inventory_action_does_popup_route_split` bucket by exact
`InventoryActionEvent` owner shape and moves all rows to implementation-gap
work. The new buckets are `producer_runtime_inventory_action_examiner_popup_gap`
1 row / 15 constructions, `producer_runtime_inventory_action_tinker_item_popup_gap`
1 / 15, `producer_runtime_inventory_action_fixit_spray_popup_gap` 1 / 11, and
`producer_runtime_inventory_action_magnetized_applicator_popup_gap` 1 / 11.
Static source review shows direct owner routes in `Examiner.HandleEvent`,
`TinkerItem.HandleEvent`, `FixitSpray.HandleEvent`, and
`MagnetizedApplicator.HandleEvent`. The `Does` constructions are route-local
object/pronoun captures inside these owner popups/failures, not evidence that a
separate runtime sink must discover the route. The residual ledger after this
tranche is
`/tmp/qudjp-issue719-residual-after-inventory-action-does-popup-reclassification.json`.
Policy counts are now `covered_by_owner_route=2299` rows / `16979` text
constructions, `action_required=222` rows / `1472` constructions, and
`runtime_required=120` rows / `1193` constructions. The largest remaining
runtime row buckets by row count are
`producer_runtime_world_part_queue_route_split`,
`producer_runtime_world_part_queue_popup_route_split`, `misc_route_split`,
`ui_screen_options_control_runtime`, and `ui_screen_trade_drag_numeric_runtime`,
each at 4 rows.

The world-part queue reclassification tranche splits the 4-row
`producer_runtime_world_part_queue_route_split` bucket by exact pure
AddPlayerMessage owner shape and moves all rows to implementation-gap work. The
new buckets are `producer_runtime_world_part_dance_opponent_debug_queue_gap` 1
row / 22 constructions,
`producer_runtime_world_part_player_dance_ritual_queue_gap` 1 / 14,
`producer_runtime_world_part_dance_opponent_register_queue_gap` 1 / 6, and
`producer_runtime_world_part_interior_damage_queue_gap` 1 / 3. Static source
review shows direct owner routes in
`DanceRitualOpponent.HandleEvent(BeforeAITakingActionEvent)`,
`PlayerDanceRitual.FireEvent`, `DanceRitualOpponent.Register`, and
`Interior.HandleEvent(TookDamageEvent)`. The dance rows include debug-colored
messages, but the owner routes are statically identified and do not need live
runtime evidence for route discovery. The residual ledger after this tranche is
`/tmp/qudjp-issue719-residual-after-world-part-queue-reclassification.json`.
Policy counts are now `covered_by_owner_route=2299` rows / `16979` text
constructions, `action_required=226` rows / `1517` constructions, and
`runtime_required=116` rows / `1148` constructions. The largest remaining
runtime row bucket by row count is
`producer_runtime_world_part_queue_popup_route_split` at 4 rows / 44
constructions.

The world-part queue+popup reclassification tranche splits the 4-row
`producer_runtime_world_part_queue_popup_route_split` bucket by exact mixed
AddPlayerMessage+Popup owner shape and moves all rows to implementation-gap
work. The new buckets are
`producer_runtime_world_part_stomach_water_queue_popup_gap` 1 row / 24
constructions, `producer_runtime_world_part_elevator_switch_queue_popup_gap` 1
/ 9, `producer_runtime_world_part_biome_distribution_queue_popup_gap` 1 / 8,
and `producer_runtime_world_part_giant_clam_dimension_queue_popup_gap` 1 / 3.
Static source review shows direct owner routes in `Stomach.FireEvent`,
`ElevatorSwitch.FireEvent`, `BiomeManager.DisplaySurfaceDistribution`, and
`GiantClamProperties.TeleportFromClamWorld`. `BiomeManager` appears diagnostic,
but that is an implementation/pass-through classification question, not a
runtime route discovery blocker. The residual ledger after this tranche is
`/tmp/qudjp-issue719-residual-after-world-part-queue-popup-reclassification.json`.
Policy counts are now `covered_by_owner_route=2299` rows / `16979` text
constructions, `action_required=230` rows / `1561` constructions, and
`runtime_required=112` rows / `1104` constructions. The largest remaining
runtime row buckets by row count are `misc_route_split`,
`ui_screen_options_control_runtime`, and `ui_screen_trade_drag_numeric_runtime`,
each at 4 rows.

The UI options popup reclassification tranche splits the 3-row
`producer_runtime_ui_options_popup_route_split` bucket by exact options UI
owner shape and moves all rows to implementation-gap work. The new buckets are
`producer_runtime_ui_options_legacy_popup_gap` 1 row / 67 constructions,
`producer_runtime_ui_options_command_binding_gap` 1 / 7, and
`producer_runtime_ui_options_help_popup_gap` 1 / 1. Static source review shows
direct owner routes in `OptionsUI.Show`, `CommandBindingManager.RestoreDefaults`,
and `OptionsScreen.HandleMenuOption`. These rows may still need route-local
option-label/help-text handling, but live runtime evidence is not needed to
identify the owning producer. The residual ledger after this tranche is
`/tmp/qudjp-issue719-residual-after-ui-options-popup-reclassification.json`.
Policy counts are now `covered_by_owner_route=2299` rows / `16979` text
constructions, `action_required=233` rows / `1636` constructions, and
`runtime_required=109` rows / `1029` constructions. The largest remaining
runtime construction buckets are `sifrah_description_unused_base_game_runtime`
2 rows / 85 constructions, `producer_broad_gameobject_autoequip_runtime` 1 / 72,
and `producer_runtime_capability_route_split` 3 / 69.

The capability producer reclassification tranche splits the 3-row
`producer_runtime_capability_route_split` bucket by exact capability owner
shape and moves all rows to implementation-gap work. The new buckets are
`producer_runtime_capability_firefighting_gap` 1 row / 37 constructions,
`producer_runtime_capability_item_naming_gap` 1 / 27, and
`producer_runtime_capability_item_naming_wish_debug_gap` 1 / 5. Static source
review shows route-local player-visible owners in
`Firefighting.AttemptFirefightingCore`, `ItemNaming.NameItem`, and
`ItemNaming.HandleItemNamingWish`: firefighting failure popups plus
`Messaging.XDidY*` frames, item naming menu/prompt popups, and itemnaming Wish
debug popups. These rows still need owner-route implementation/pass-through
decisions, but live runtime evidence is not needed to identify the producers.
The residual ledger after this tranche is
`/tmp/qudjp-issue719-residual-after-capability-reclassification.json`. Policy
counts are now `covered_by_owner_route=2299` rows / `16979` text constructions,
`action_required=236` rows / `1705` constructions, and `runtime_required=106`
rows / `960` constructions. The largest remaining runtime construction buckets
are `sifrah_description_unused_base_game_runtime` 2 rows / 85 constructions,
`producer_broad_gameobject_autoequip_runtime` 1 / 72, and
`producer_runtime_world_part_does_emit_message_frame_route_split` 2 / 47.

The Sifrah unused-base-game static reclassification tranche removes the 2-row
`sifrah_description_unused_base_game_runtime` bucket from the residual ledger
by promoting its static evidence overlay. The affected rows are
`PsychicCombatSifrah.PsychicCombatSifrah` 1 row / 72 constructions and
`BeguilingSifrah.BeguilingSifrah` 1 / 13. Both decompiled source files state
that the class is not used in the base game, so these rows do not require live
runtime evidence for Issue #719's base-game residual closure. `PsychicCombat`
also retains existing pure-owner Sifrah patch/test references; `Beguiling` is
closed specifically by the static unused-base-game evidence and is not claimed
as covered by the pure-owner Sifrah patch.

Fresh residual output is
`/tmp/qudjp-issue719-residual-after-sifrah-unused-static-reclassification.json`.
Fresh policy output is
`/tmp/qudjp-issue719-policy-after-sifrah-unused-static-reclassification.json`.
After regeneration, residual rows are 340, with dispositions
`likely_implementation_gap=236` and `runtime_evidence_required=104`. Policy
closure counts are `covered_by_owner_route=2301` rows / `17064` constructions,
`action_required=236` rows / `1705` constructions, and `runtime_required=104`
rows / `875` constructions.

Next high-construction runtime buckets are
`producer_broad_gameobject_autoequip_runtime` 1 row / 72 constructions,
`producer_runtime_world_part_does_emit_message_frame_route_split` 2 / 47, and
`producer_runtime_core_scores_popup_runtime` 1 / 46.

The world-part Does+EmitMessage+MessageFrame reclassification tranche splits the
2-row `producer_runtime_world_part_does_emit_message_frame_route_split` bucket
by exact world-part owner shape and moves both rows to implementation-gap work.
The new buckets are `producer_runtime_world_part_harvestable_attempt_gap` 1 row
/ 39 constructions and `producer_runtime_world_part_campfire_extinguish_gap` 1
/ 8. Static source review shows direct owner routes in
`Harvestable.AttemptHarvest` for harvest messages and in `Campfire.Extinguish`
for extinguish messages. These rows mix object/pronoun grammar, `EmitMessage`,
and message-frame helpers inside one method, but live runtime evidence is not
needed to identify the owning producer.

Fresh residual output is
`/tmp/qudjp-issue719-residual-after-world-part-does-emit-frame-reclassification.json`.
Fresh policy output is
`/tmp/qudjp-issue719-policy-after-world-part-does-emit-frame-reclassification.json`.
After regeneration, residual rows remain 340, with dispositions
`likely_implementation_gap=238` and `runtime_evidence_required=102`. Policy
closure counts are `covered_by_owner_route=2301` rows / `17064` constructions,
`action_required=238` rows / `1752` constructions, and `runtime_required=102`
rows / `828` constructions.

Next high-construction runtime buckets are
`producer_broad_gameobject_autoequip_runtime` 1 row / 72 constructions,
`producer_runtime_core_scores_popup_runtime` 1 / 46, and
`producer_broad_missile_trajectory_message_runtime` 1 / 45.

The AutoEquip reclassification tranche moves the single largest remaining
runtime construction row, `producer_broad_gameobject_autoequip_runtime`, to
`producer_broad_gameobject_autoequip_gap`. The row is
`XRL.World/GameObject.cs::GameObject.AutoEquip(GameObject,bool,bool,bool)` and
contains 72 popup constructions. Static source review shows the route is owned
by `GameObject.AutoEquip` plus its local `AutoEquipFail`, `AutoEquipSucceed`,
and `DescribeUnequip` helper path. The text is player-visible auto-equip
failure/success popup output with generated item/body-part captures, so this is
an owner-route implementation candidate rather than a live runtime route
discovery blocker.

Fresh residual output is
`/tmp/qudjp-issue719-residual-after-autoequip-reclassification.json`. Fresh
policy output is `/tmp/qudjp-issue719-policy-after-autoequip-reclassification.json`.
After regeneration, residual rows remain 340, with dispositions
`likely_implementation_gap=239` and `runtime_evidence_required=101`. Policy
closure counts are `covered_by_owner_route=2301` rows / `17064` constructions,
`action_required=239` rows / `1824` constructions, and `runtime_required=101`
rows / `756` constructions.

Next high-construction runtime buckets are
`producer_runtime_core_scores_popup_runtime` 1 row / 46 constructions,
`producer_broad_missile_trajectory_message_runtime` 1 / 45, and
`producer_broad_gameobject_death_runtime` 1 / 44.

The UI inventory/trade popup reclassification tranche splits the 3-row
`producer_runtime_ui_inventory_trade_popup_route_split` bucket by exact UI owner
shape and moves all rows to implementation-gap work. The new buckets are
`producer_runtime_ui_trade_vendor_actions_gap` 1 row / 17 constructions,
`producer_runtime_ui_object_finder_filters_gap` 1 / 16, and
`producer_runtime_ui_equipment_slot_gap` 1 / 9. Static source review shows
direct owner routes in `TradeUI.ShowVendorActions`,
`ObjectFinder.ConfigFilters`, and `EquipmentScreen.ShowBodypartEquipUI`.
These rows contain menu/picker popup labels and generated item/filter/slot
captures, but live runtime evidence is not needed to identify the owning
producer.

Fresh residual output is
`/tmp/qudjp-issue719-residual-after-ui-inventory-trade-reclassification.json`.
Fresh policy output is
`/tmp/qudjp-issue719-policy-after-ui-inventory-trade-reclassification.json`.
After regeneration, residual rows remain 340, with dispositions
`likely_implementation_gap=242` and `runtime_evidence_required=98`. Policy
closure counts are `covered_by_owner_route=2301` rows / `17064` constructions,
`action_required=242` rows / `1866` constructions, and `runtime_required=98`
rows / `714` constructions.

Next row-count runtime buckets are `misc_route_split`,
`ui_screen_options_control_runtime`, and `ui_screen_trade_drag_numeric_runtime`,
each at 4 rows. The largest remaining runtime construction buckets are
`producer_runtime_core_scores_popup_runtime` 1 row / 46 constructions,
`producer_broad_missile_trajectory_message_runtime` 1 / 45, and
`producer_broad_gameobject_death_runtime` 1 / 44.

The Options control promotion tranche removes the 4-row
`ui_screen_options_control_runtime` bucket from the residual ledger using
existing owner-route evidence. The covered rows are
`OptionsCategoryControl.Render` 1 row / 3 constructions,
`OptionsCheckboxControl.Render` 1 / 3, `OptionsRow.setData` 1 / 3, and
`OptionsButtonControl.Render` 1 / 1. Static source review shows these controls
bind `OptionsDataRow.Title` and `HelpText` through `SetText`. The existing
`OptionsLocalizationPatch` translates options row `Title` and `HelpText` in
`OptionsScreen.Show` before control binding, and the existing
`UITextSkinTranslationPatch` tests cover the Options context sink guard. This
is a promotion-only closure, not a new implementation gap.

Fresh residual output is
`/tmp/qudjp-issue719-residual-after-options-control-promotion.json`. Fresh
policy output is `/tmp/qudjp-issue719-policy-after-options-control-promotion.json`.
After regeneration, residual rows are 336, with dispositions
`likely_implementation_gap=242` and `runtime_evidence_required=94`. Policy
closure counts are `covered_by_owner_route=2305` rows / `17074` constructions,
`action_required=242` rows / `1866` constructions, and `runtime_required=94`
rows / `704` constructions.

Next row-count runtime buckets are `misc_route_split` and
`ui_screen_trade_drag_numeric_runtime`, each at 4 rows. The largest remaining
runtime construction buckets are `producer_runtime_core_scores_popup_runtime`
1 row / 46 constructions, `producer_broad_missile_trajectory_message_runtime`
1 / 45, and `producer_broad_gameobject_death_runtime` 1 / 44.

The TradeLine drag numeric promotion tranche removes the 4-row
`ui_screen_trade_drag_numeric_runtime` bucket from the residual ledger using
static numeric-only evidence. The covered rows are `TradeLine.Update` 1 row / 1
construction, `TradeLine.OnBeginDrag` 1 / 1, `TradeLine.OnDrag` 1 / 1, and
`TradeLine.OnScroll` 1 / 1. Decompiled source shows all four rows set
`TradeScreen.dragIndicatorText` to a colored numeric count such as
`{{W|{num}}}` or `{{W|{num2}}}`. These are quantity indicators for trade drag,
typed amount, and scroll adjustment flows, not English player text requiring a
translation owner or runtime route proof.

Fresh residual output is
`/tmp/qudjp-issue719-residual-after-trade-line-numeric-promotion.json`. Fresh
policy output is
`/tmp/qudjp-issue719-policy-after-trade-line-numeric-promotion.json`. After
regeneration, residual rows are 332, with dispositions
`likely_implementation_gap=242` and `runtime_evidence_required=90`. Policy
closure counts are `covered_by_owner_route=2309` rows / `17078`
constructions, `action_required=242` rows / `1866` constructions, and
`runtime_required=90` rows / `700` constructions.

Verification for this tranche passed focused TradeLine/UI split policy tests,
the full text-construction policy suite, ruff, markdown report check,
translation-token-check, and `git diff --check`.

Next row-count runtime bucket is `misc_route_split` at 4 rows / 10
constructions. The largest remaining runtime construction buckets are
`producer_runtime_core_scores_popup_runtime` 1 row / 46 constructions,
`producer_broad_missile_trajectory_message_runtime` 1 / 45, and
`producer_broad_gameobject_death_runtime` 1 / 44.

The misc route reclassification tranche eliminates the 4-row
`misc_route_split` bucket. `GlotrotFilter.HandleEvent(PrepareTextEvent)` is
promoted out of residual as static pass-through evidence: decompiled source
shows it clears conversation text and emits only disease speech gibberish
(`N` or `G`, repeated `n`, and a period), not translatable English. The
remaining rows are split by route shape: `TextFilters.Angry` and
`TextFilters.Lallated` move to
`history_text_filter_speech_status_runtime` 2 rows / 6 constructions, and
`InsertRandomBookLine.HandleEvent(PrepareTextEvent)` moves to
`conversation_book_line_data_runtime` 1 row / 2 constructions.

Fresh residual output is
`/tmp/qudjp-issue719-residual-after-misc-route-reclassification.json`. Fresh
policy output is
`/tmp/qudjp-issue719-policy-after-misc-route-reclassification.json`. After
regeneration, residual rows are 331, with dispositions
`likely_implementation_gap=242` and `runtime_evidence_required=89`. Policy
closure counts are `covered_by_owner_route=2310` rows / `17080`
constructions, `action_required=242` rows / `1866` constructions, and
`runtime_required=89` rows / `698` constructions.

TextFilters remains runtime-required because existing static evidence traces
multiple owners (`StyledStatus`, `Preacher`, and conversation `TextFilter`)
that mutate already-composed speech/status text; completion needs observed
owner-specific final output. Inserted book lines remain runtime-required
because the route depends on `BookUI.Books` data localization rather than an
inline fixed phrase.

Verification for this tranche passed focused misc/conversation/TextFilters
policy tests, the full text-construction policy suite, ruff, markdown report
check, translation-token-check, and `git diff --check`.

Next row-count runtime buckets are
`generated_display_name_sultan_entity_runtime`,
`producer_runtime_world_part_does_emit_route_split`, and
`producer_runtime_api_route_split`, each at 3 rows. The largest remaining
runtime construction buckets are `producer_runtime_core_scores_popup_runtime`
1 row / 46 constructions, `producer_broad_missile_trajectory_message_runtime`
1 / 45, and `producer_broad_gameobject_death_runtime` 1 / 44.

The world-part Does+Emit reclassification tranche splits the 3-row
`producer_runtime_world_part_does_emit_route_split` bucket by exact owner
shape and moves all rows to implementation-gap work. The new buckets are
`producer_runtime_world_part_chat_emit_gap` 1 row / 16 constructions,
`producer_runtime_world_part_fungal_cure_emit_gap` 1 / 14, and
`producer_runtime_world_part_vehicle_infiltration_emit_gap` 1 / 3. Static
source review shows direct owner routes in `Chat.PerformChat`,
`FungalInfection.FireEvent`, and
`VehicleMeleeInfiltration.HandleEvent(CanEnterInteriorEvent)`. These rows
compose message text with `Does`, object/pronoun captures, or data-driven chat
content, but live runtime evidence is not needed to identify the owning
producer.

Fresh residual output is
`/tmp/qudjp-issue719-residual-after-world-part-does-emit-reclassification.json`.
Fresh policy output is
`/tmp/qudjp-issue719-policy-after-world-part-does-emit-reclassification.json`.
After regeneration, residual rows remain 331, with dispositions
`likely_implementation_gap=245` and `runtime_evidence_required=86`. Policy
closure counts are `covered_by_owner_route=2310` rows / `17080`
constructions, `action_required=245` rows / `1899` constructions, and
`runtime_required=86` rows / `665` constructions.

Verification for this tranche passed focused world-part Does+Emit policy
tests, the full text-construction policy suite, ruff, markdown report check,
translation-token-check, and `git diff --check`.

Next row-count runtime buckets are
`generated_display_name_sultan_entity_runtime`,
`producer_runtime_api_route_split`,
`producer_runtime_ui_status_popup_route_split`,
`producer_runtime_liquid_route_split`, and
`producer_runtime_quest_route_split`, each at 3 rows. The largest remaining
runtime construction buckets are `producer_runtime_core_scores_popup_runtime`
1 row / 46 constructions, `producer_broad_missile_trajectory_message_runtime`
1 / 45, and `producer_broad_gameobject_death_runtime` 1 / 44.

The VillageCoda generated-name reclassification tranche moves the 3-row
`generated_display_name_sultan_entity_runtime` bucket to the implementation
gap bucket `generated_display_name_sultan_entity_gap`. The rows are
`VillageCoda.GenerateSultanEntity` 1 row / 22 constructions,
`VillageCoda.SetStatueVisuals` 1 / 8, and
`VillageCoda.GenerateMechanicalGolem` 1 / 3. Static source review shows direct
owner routes for `Cult of <display-name>`, `shrine to <sultan-name>`, and
`mechanical <body-display-name>` generated display names. Runtime evidence is
not needed to identify the owner, but implementation still needs route-local
component/capture handling and focused generated-name tests.

Fresh residual output is
`/tmp/qudjp-issue719-residual-after-village-coda-generated-name-reclassification.json`.
Fresh policy output is
`/tmp/qudjp-issue719-policy-after-village-coda-generated-name-reclassification.json`.
After regeneration, residual rows remain 331, with dispositions
`likely_implementation_gap=248` and `runtime_evidence_required=83`. Policy
closure counts are `covered_by_owner_route=2310` rows / `17080`
constructions, `action_required=248` rows / `1932` constructions, and
`runtime_required=83` rows / `632` constructions.

Verification for this tranche passed focused VillageCoda/generated-name
policy tests, the full text-construction policy suite, ruff, markdown report
check, translation-token-check, and `git diff --check`.

Next row-count runtime buckets are `producer_runtime_api_route_split`,
`producer_runtime_ui_status_popup_route_split`,
`producer_runtime_liquid_route_split`,
`producer_runtime_quest_route_split`, and
`generated_display_name_world_part_cybernetics_runtime`, each at 3 rows. The
largest remaining runtime construction buckets are
`producer_runtime_core_scores_popup_runtime` 1 row / 46 constructions,
`producer_broad_missile_trajectory_message_runtime` 1 / 45, and
`producer_broad_gameobject_death_runtime` 1 / 44.

The API popup reclassification tranche eliminates the 3-row
`producer_runtime_api_route_split` bucket. `EquipmentAPI.ShowInventoryActionMenu`
is moved to `producer_runtime_api_equipment_action_menu_gap` 1 row / 12
constructions, and `SavesAPI.FatalSaveError` is moved to
`producer_runtime_api_save_error_gap` 1 / 9. Static source review shows exact
owner routes for inventory action picker labels and save-directory fatal error
popup text. `JournalAPI.WishGospelAccomplishments` is split to
`producer_runtime_api_journal_wish_gospel_runtime` 1 / 7 because it is a
`WishCommand("gospelme")` debug/gospel dump path that formats current journal
accomplishment/gospel text.

Fresh residual output is
`/tmp/qudjp-issue719-residual-after-api-route-reclassification.json`. Fresh
policy output is `/tmp/qudjp-issue719-policy-after-api-route-reclassification.json`.
After regeneration, residual rows remain 331, with dispositions
`likely_implementation_gap=250` and `runtime_evidence_required=81`. Policy
closure counts are `covered_by_owner_route=2310` rows / `17080`
constructions, `action_required=250` rows / `1953` constructions, and
`runtime_required=81` rows / `611` constructions.

Verification for this tranche passed focused API/popup policy tests, the full
text-construction policy suite, ruff, markdown report check,
translation-token-check, and `git diff --check`.

Next row-count runtime buckets are `producer_runtime_ui_status_popup_route_split`,
`producer_runtime_liquid_route_split`, `producer_runtime_quest_route_split`,
and `generated_display_name_world_part_cybernetics_runtime`, each at 3 rows.
The largest remaining runtime construction buckets are
`producer_runtime_core_scores_popup_runtime` 1 row / 46 constructions,
`producer_broad_missile_trajectory_message_runtime` 1 / 45, and
`producer_broad_gameobject_death_runtime` 1 / 44.

The UI status popup reclassification tranche eliminates the 3-row
`producer_runtime_ui_status_popup_route_split` bucket. The new buckets are
`producer_runtime_ui_factions_status_sort_gap` 1 row / 8 constructions,
`producer_runtime_ui_inventory_status_options_gap` 1 / 8, and
`producer_runtime_ui_ability_manager_empty_gap` 1 / 5. Static source review
shows exact owner routes for factions sort options, inventory/equipment status
view options, and Ability Manager empty-state popups. Runtime evidence is not
needed to identify the owner methods, but implementation still needs focused UI
popup/option translation tests.

Fresh residual output is
`/tmp/qudjp-issue719-residual-after-ui-status-popup-reclassification.json`.
Fresh policy output is
`/tmp/qudjp-issue719-policy-after-ui-status-popup-reclassification.json`. After
regeneration, residual rows remain 331, with dispositions
`likely_implementation_gap=253` and `runtime_evidence_required=78`. Policy
closure counts are `covered_by_owner_route=2310` rows / `17080`
constructions, `action_required=253` rows / `1974` constructions, and
`runtime_required=78` rows / `590` constructions.

Verification for this tranche passed focused UI status popup policy tests, the
full text-construction policy suite, ruff, markdown report check,
translation-token-check, and `git diff --check`.

Next row-count runtime buckets are `producer_runtime_liquid_route_split`,
`producer_runtime_quest_route_split`, and
`generated_display_name_world_part_cybernetics_runtime`, each at 3 rows. The
largest remaining runtime construction buckets are
`producer_runtime_core_scores_popup_runtime` 1 row / 46 constructions,
`producer_broad_missile_trajectory_message_runtime` 1 / 45, and
`producer_broad_gameobject_death_runtime` 1 / 44.

The LiquidWarmStatic runtime reclassification tranche eliminates the 3-row
`producer_runtime_liquid_route_split` bucket. The new buckets are
`producer_runtime_liquid_wish_warm_effect_gap` 2 rows / 8 constructions and
`producer_runtime_liquid_glitch_components_gap` 1 row / 4 constructions. Static
source review shows exact decompiled owners in
`XRL.Liquids/LiquidWarmStatic.cs`: `WishWarmEffect` and `WishWarmEffectSpec`
are `[WishCommand("warmeffect")]` EmitMessage frames over effect/object display
captures, while `GlitchLiquidComponents` owns the liquid-mixture glitch
EmitMessage frame. Runtime evidence is not needed to identify these owner
methods, but implementation still needs route-local capture handling and
focused liquid message tests.

Fresh residual output is
`/tmp/qudjp-issue719-residual-after-liquid-runtime-reclassification.json`.
Fresh policy output is
`/tmp/qudjp-issue719-policy-after-liquid-runtime-reclassification.json`. Fresh
follow-up output is
`/tmp/qudjp-issue719-followup-after-liquid-runtime-reclassification.json`. After
regeneration, residual rows remain 331, with dispositions
`likely_implementation_gap=256` and `runtime_evidence_required=75`. Policy
closure counts are `covered_by_owner_route=2310` rows / `17080`
constructions, `action_required=256` rows / `1986` constructions, and
`runtime_required=75` rows / `578` constructions.

Verification for this tranche passed focused LiquidWarmStatic policy tests, the
full text-construction policy suite, ruff, markdown report check,
translation-token-check, and `git diff --check`.

Next row-count runtime buckets are `producer_runtime_quest_route_split` and
`generated_display_name_world_part_cybernetics_runtime`, each at 3 rows. The
largest remaining runtime construction buckets are
`producer_runtime_core_scores_popup_runtime` 1 row / 46 constructions,
`producer_broad_missile_trajectory_message_runtime` 1 / 45, and
`producer_broad_gameobject_death_runtime` 1 / 44.

The quest runtime reclassification tranche eliminates the 3-row
`producer_runtime_quest_route_split` bucket. `ReclamationSystem.HandleEvent`
is promoted to `covered_by_owner_route` 1 row / 2 constructions because the
visible `Popup.ShowYesNo(GetProperty("MessageLeaving"), ...)` text is loaded
from the localized `Reclamation` quest property in
`Mods/QudJP/Localization/Quests.jp.xml`. The remaining rows move to
implementation-gap buckets: `producer_runtime_quest_reward_choice_gap` 1 row /
6 constructions for `DynamicQuestRewardElement_ChoiceFromPopulation.award`,
and `producer_runtime_quest_find_site_wish_debug_gap` 1 row / 1 construction
for the `[WishCommand]` `FindASiteDynamicQuestManager.DynamicQuestWhere`
debug AddPlayerMessage frame.

Fresh residual output is
`/tmp/qudjp-issue719-residual-after-quest-runtime-reclassification.json`.
Fresh policy output is
`/tmp/qudjp-issue719-policy-after-quest-runtime-reclassification.json`. Fresh
follow-up output is
`/tmp/qudjp-issue719-followup-after-quest-runtime-reclassification.json`. After
regeneration, residual rows are 330 / 2562 constructions, with dispositions
`likely_implementation_gap=258` rows / `1993` constructions and
`runtime_evidence_required=72` rows / `569` constructions. Policy closure
counts are `covered_by_owner_route=2311` rows / `17082` constructions,
`action_required=258` rows / `1993` constructions, and `runtime_required=72`
rows / `569` constructions.

Verification for this tranche passed focused quest policy tests, the full
text-construction policy suite, ruff, markdown report check,
translation-token-check, and `git diff --check`.

Next row-count runtime bucket is
`generated_display_name_world_part_cybernetics_runtime` 3 rows / 4
constructions. The largest remaining runtime construction buckets are
`producer_runtime_core_scores_popup_runtime` 1 row / 46 constructions,
`producer_broad_missile_trajectory_message_runtime` 1 / 45, and
`producer_broad_gameobject_death_runtime` 1 / 44.

The cybernetics generated display-name reclassification tranche eliminates the
3-row `generated_display_name_world_part_cybernetics_runtime` bucket. The new
buckets are `generated_display_name_world_part_cybernetics_recoiler_gap` 1 row
/ 2 constructions and `generated_display_name_world_part_cybernetics_skillsoft_gap`
2 rows / 2 constructions. Static source review shows exact generated display
owners: `CyberneticsOnboardRecoilerImprinting.UpdateName` sets the activated
ability display name to `Recoil` or `Recoil to <zone display name>`, while
`CyberneticsSingleSkillsoft.InitChip` and `CyberneticsTreeSkillsoft.InitChip`
set render display names to `Skillsoft [<skill>]` and
`Skillsoft Plus [<skill tree>]`.

Fresh residual output is
`/tmp/qudjp-issue719-residual-after-cybernetics-display-name-reclassification.json`.
Fresh policy output is
`/tmp/qudjp-issue719-policy-after-cybernetics-display-name-reclassification.json`.
Fresh follow-up output is
`/tmp/qudjp-issue719-followup-after-cybernetics-display-name-reclassification.json`.
After regeneration, residual rows remain 330 / 2562 constructions, with
dispositions `likely_implementation_gap=261` rows / `1997` constructions and
`runtime_evidence_required=69` rows / `565` constructions. Policy closure
counts are `covered_by_owner_route=2311` rows / `17082` constructions,
`action_required=261` rows / `1997` constructions, and `runtime_required=69`
rows / `565` constructions.

Verification for this tranche passed focused cybernetics display-name policy
tests, the full text-construction policy suite, ruff, markdown report check,
translation-token-check, and `git diff --check`.

Next row-count runtime buckets are
`generated_display_name_village_signature_dish_runtime`,
`ui_popup_sink_route_split`, and
`producer_runtime_world_part_queue_does_route_split`, each 2 rows. The largest
remaining runtime construction buckets are
`producer_runtime_core_scores_popup_runtime` 1 row / 46 constructions,
`producer_broad_missile_trajectory_message_runtime` 1 / 45, and
`producer_broad_gameobject_death_runtime` 1 / 44.

The village signature-dish promotion tranche removes
`generated_display_name_village_signature_dish_runtime` 2 rows / 28
constructions from residual. `VillageBase.generateSignatureDish` and
`VillageCodaBase.generateSignatureDish` both assign `signatureDish` through
`CookingRecipe.FromIngredients(...)` or an authored `signatureDishName`
property, while visible recipe names are served by the existing
`CookingRecipe.GetDisplayName` owner route. This is a promotion-only closure
through `CookingRecipeDisplayNameTranslationPatch`, not a new implementation
gap.

Fresh residual output is
`/tmp/qudjp-issue719-residual-after-village-signature-dish-promotion.json`.
Fresh policy output is
`/tmp/qudjp-issue719-policy-after-village-signature-dish-promotion.json`.
Fresh follow-up output is
`/tmp/qudjp-issue719-followup-after-village-signature-dish-promotion.json`.
After regeneration, residual rows are 328 / 2534 constructions, with
dispositions `likely_implementation_gap=261` rows / `1997` constructions and
`runtime_evidence_required=67` rows / `537` constructions. Policy closure
counts are `covered_by_owner_route=2313` rows / `17110` constructions,
`action_required=261` rows / `1997` constructions, and `runtime_required=67`
rows / `537` constructions.

Verification for this tranche passed focused village signature-dish policy
tests, the full text-construction policy suite, ruff, markdown report check,
translation-token-check, and `git diff --check`.

Next row-count runtime buckets are `ui_popup_sink_route_split` and
`producer_runtime_world_part_queue_does_route_split`, each 2 rows. The largest
remaining runtime construction buckets are
`producer_runtime_core_scores_popup_runtime` 1 row / 46 constructions,
`producer_broad_missile_trajectory_message_runtime` 1 / 45, and
`producer_broad_gameobject_death_runtime` 1 / 44.

The world-part queue/Does promotion tranche removes
`producer_runtime_world_part_queue_does_route_split` 2 rows / 21 constructions
from residual. `Physics.HandleEvent(ObjectEnteringCellEvent)` is promoted
because its collision, overland-block, and blocked-way `AddPlayerMessage`
branches are served by the existing exact
`PhysicsObjectEnteringCellTranslationPatch` owner route, including the
Does-composed overland block shape covered by focused queue tests.
`ThiefBot.FireEvent` is promoted because its pincer `AddPlayerMessage`
branches are served by `SingleCallsiteOwnerQueueTranslationPatch`, while the
`snag` Does/EmitMessage branch is served by the existing Does verb route and
`MessageFrames/verbs.ja.json`.

Fresh residual output is
`/tmp/qudjp-issue719-residual-after-world-part-queue-does-promotion.json`.
Fresh policy output is
`/tmp/qudjp-issue719-policy-after-world-part-queue-does-promotion.json`.
Fresh follow-up output is
`/tmp/qudjp-issue719-followup-after-world-part-queue-does-promotion.json`.
After regeneration, residual rows are 326 / 2513 constructions, with
dispositions `likely_implementation_gap=261` rows / `1997` constructions and
`runtime_evidence_required=65` rows / `516` constructions. Policy closure
counts are `covered_by_owner_route=2315` rows / `17131` constructions,
`action_required=261` rows / `1997` constructions, and `runtime_required=65`
rows / `516` constructions.

Verification for this tranche passed focused world-part queue/Does policy
tests, the full text-construction policy suite, ruff, markdown report check,
translation-token-check, and `git diff --check`.

Next row-count runtime buckets are `ui_popup_sink_route_split`,
`producer_runtime_core_population_wish_popup_runtime`,
`producer_runtime_core_sound_debug_queue_runtime`, and
`generated_display_name_village_signature_item_runtime`, each 2 rows. The
largest remaining runtime construction buckets are
`producer_runtime_core_scores_popup_runtime` 1 row / 46 constructions,
`producer_broad_missile_trajectory_message_runtime` 1 / 45, and
`producer_broad_gameobject_death_runtime` 1 / 44.

The village signature-item reclassification tranche moves
`generated_display_name_village_signature_item_runtime` 2 rows / 10
constructions to `generated_display_name_village_signature_item_gap`.
`VillageBase.generateSignatureItems` and
`VillageCodaBase.generateSignatureItems` both assign `signatureItemBlueprint`
from the village snapshot and assign
`signatureHistoricObjectInstance.DisplayName` from the generated
`signatureHistoricObjectName` snapshot property. `BecomesKnownFor` creates
that snapshot name from `<spice.villages.SignatureHistoricObject>`, so the
owner is static, but implementation still needs a route-local generated-name /
HistorySpice reconstruction path.

Fresh residual output is
`/tmp/qudjp-issue719-residual-after-village-signature-item-reclassification.json`.
Fresh policy output is
`/tmp/qudjp-issue719-policy-after-village-signature-item-reclassification.json`.
Fresh follow-up output is
`/tmp/qudjp-issue719-followup-after-village-signature-item-reclassification.json`.
After regeneration, residual rows remain 326 / 2513 constructions, with
dispositions `likely_implementation_gap=263` rows / `2007` constructions and
`runtime_evidence_required=63` rows / `506` constructions. Policy closure
counts are `covered_by_owner_route=2315` rows / `17131` constructions,
`action_required=263` rows / `2007` constructions, and `runtime_required=63`
rows / `506` constructions.

Verification for this tranche passed focused village signature-item policy
tests, the full text-construction policy suite, ruff, markdown report check,
translation-token-check, and `git diff --check`.

Next row-count runtime buckets are `ui_popup_sink_route_split`,
`producer_runtime_core_population_wish_popup_runtime`, and
`producer_runtime_core_sound_debug_queue_runtime`, each 2 rows. The largest
remaining runtime construction buckets are
`producer_runtime_core_scores_popup_runtime` 1 row / 46 constructions,
`producer_broad_missile_trajectory_message_runtime` 1 / 45, and
`producer_broad_gameobject_death_runtime` 1 / 44.

The PopulationManager popup reclassification tranche moves
`producer_runtime_core_population_wish_popup_runtime` 2 rows / 14
constructions to exact implementation-gap buckets. `WishFindBlueprint` is a
`[WishCommand("population:findblueprint")]` debug popup that builds
population-table probability text from static owner code.
`RollOneFrom` owns the population-generation error popup emitted from its
`Generate(...)` exception path. Both owners are statically identifiable, so
they no longer need runtime evidence to classify; implementation still needs
route-local popup translation and focused tests.

Fresh residual output is
`/tmp/qudjp-issue719-residual-after-population-manager-popup-reclassification.json`.
Fresh policy output is
`/tmp/qudjp-issue719-policy-after-population-manager-popup-reclassification.json`.
Fresh follow-up output is
`/tmp/qudjp-issue719-followup-after-population-manager-popup-reclassification.json`.
After regeneration, residual rows remain 326 / 2513 constructions, with
dispositions `likely_implementation_gap=265` rows / `2021` constructions and
`runtime_evidence_required=61` rows / `492` constructions. Policy closure
counts are `covered_by_owner_route=2315` rows / `17131` constructions,
`action_required=265` rows / `2021` constructions, and `runtime_required=61`
rows / `492` constructions.

Verification for this tranche passed focused PopulationManager/core-system
policy tests, the full text-construction policy suite, ruff, markdown report
check, translation-token-check, and `git diff --check`.

Next row-count runtime buckets are `ui_popup_sink_route_split` and
`producer_runtime_core_sound_debug_queue_runtime`, each 2 rows. The largest
remaining runtime construction buckets are
`producer_runtime_core_scores_popup_runtime` 1 row / 46 constructions,
`producer_broad_missile_trajectory_message_runtime` 1 / 45, and
`producer_broad_gameobject_death_runtime` 1 / 44.

The SoundManager debug queue promotion tranche removes
`producer_runtime_core_sound_debug_queue_runtime` 2 rows / 8 constructions
from residual. `_PlaySound` and `_PlayWorldSound` only emit these
`AddPlayerMessage` rows when `WriteSoundsToLog` is enabled, and the visible
payloads are sound identifiers plus `(missing)` / `(invalid)` diagnostics.
`Options` wires `WriteSoundsToLog` from `OptionWriteSoundsToLog`, and the
existing SoundManager sound-log route tests preserve missing-track debug
diagnostics unchanged, so these rows are classified as intentional debug
pass-through rather than localizable gameplay text.

Fresh residual output is
`/tmp/qudjp-issue719-residual-after-sound-manager-debug-promotion.json`.
Fresh policy output is
`/tmp/qudjp-issue719-policy-after-sound-manager-debug-promotion.json`.
Fresh follow-up output is
`/tmp/qudjp-issue719-followup-after-sound-manager-debug-promotion.json`.
After regeneration, residual rows are 324 / 2505 constructions, with
dispositions `likely_implementation_gap=265` rows / `2021` constructions and
`runtime_evidence_required=59` rows / `484` constructions. Policy closure
counts are `covered_by_owner_route=2317` rows / `17139` constructions,
`action_required=265` rows / `2021` constructions, and `runtime_required=59`
rows / `484` constructions.

Verification for this tranche passed focused SoundManager/core-system policy
tests, the full text-construction policy suite, ruff, markdown report check,
translation-token-check, and `git diff --check`.

Next row-count runtime bucket is `ui_popup_sink_route_split` 2 rows / 26
constructions. The largest remaining runtime construction buckets are
`producer_runtime_core_scores_popup_runtime` 1 row / 46 constructions,
`producer_broad_missile_trajectory_message_runtime` 1 / 45, and
`producer_broad_gameobject_death_runtime` 1 / 44.

The Scores.Show reclassification tranche moves
`producer_runtime_core_scores_popup_runtime` 1 row / 46 constructions to
`producer_runtime_core_scores_legacy_screen_gap`. `Scores.Show` is the legacy
high-score screen owner: it writes fixed screen labels and navigation text
through `Buffer.Write`, renders dynamic `ScoreEntry2.Details`, and owns the
local delete-confirmation popup. Existing `HighScoresDeletePopupTranslationPatch`
coverage handles the delete-confirmation branch, but the legacy score screen
and score-detail body still need owner-route implementation. The owner is
static, so the row no longer needs runtime evidence for classification.

Fresh residual output is
`/tmp/qudjp-issue719-residual-after-scores-show-reclassification.json`.
Fresh policy output is
`/tmp/qudjp-issue719-policy-after-scores-show-reclassification.json`.
Fresh follow-up output is
`/tmp/qudjp-issue719-followup-after-scores-show-reclassification.json`.
After regeneration, residual rows remain 324 / 2505 constructions, with
dispositions `likely_implementation_gap=266` rows / `2067` constructions and
`runtime_evidence_required=58` rows / `438` constructions. Policy closure
counts are `covered_by_owner_route=2317` rows / `17139` constructions,
`action_required=266` rows / `2067` constructions, and `runtime_required=58`
rows / `438` constructions.

Verification for this tranche passed focused Scores/core-system/pure-popup
policy tests, the full text-construction policy suite, ruff, markdown report
check, translation-token-check, and `git diff --check`.

Next row-count runtime bucket is `ui_popup_sink_route_split` 2 rows / 26
constructions. The largest remaining runtime construction buckets are
`producer_broad_missile_trajectory_message_runtime` 1 row / 45 constructions
and `producer_broad_gameobject_death_runtime` 1 / 44.

The MissileWeapon trajectory promotion tranche removes
`producer_broad_missile_trajectory_message_runtime` 1 row / 45 constructions
from residual. `MissileWeapon.CalculateBulletTrajectory` is statically
traceable: it builds `RefractLight` and `ReflectProjectile` events, defaults
the verb to `refract` or `reflect`, then emits the visible line through
`IComponent<GameObject>.XDidYToZ(Object, verb, Projectile, null, "!")`.
`refract` was already covered by the MessageFrame tier1/tier2 route; this
tranche adds the missing `reflect` tier1 leaf and restores the localized
Mirrorshades `RefractLight Verb` attribute to the English message-frame token
`reflect` rather than visible Japanese text.

Fresh residual output is
`/tmp/qudjp-issue719-residual-after-missile-trajectory-promotion.json`.
Fresh policy output is
`/tmp/qudjp-issue719-policy-after-missile-trajectory-promotion.json`.
Fresh follow-up output is
`/tmp/qudjp-issue719-followup-after-missile-trajectory-promotion.json`.
After regeneration, residual rows are 323 / 2460 constructions, with
dispositions `likely_implementation_gap=266` rows / `2067` constructions and
`runtime_evidence_required=57` rows / `393` constructions. Policy closure
counts are `covered_by_owner_route=2318` rows / `17184` constructions,
`action_required=266` rows / `2067` constructions, and `runtime_required=57`
rows / `393` constructions.

Verification for this tranche passed the Red/Green focused
`MessageFrameTranslatorTests` reflect XDidYToZ case, the focused
`RefractLight` XML route-token contract, focused MissileWeapon policy tests,
the full text-construction policy suite, XML/JSON syntax checks, ruff,
markdown report check, translation-token-check, and `git diff --check`.

Next row-count runtime bucket is `ui_popup_sink_route_split` 2 rows / 26
constructions. The largest remaining runtime construction buckets are
`producer_broad_gameobject_death_runtime` 1 row / 44 constructions,
`generated_display_name_village_faction_runtime` 1 / 24, and
`producer_runtime_core_game_text_does_runtime` 1 / 24.

The GameObject.Die reclassification tranche moves
`producer_broad_gameobject_death_runtime` 1 row / 44 constructions to
`producer_broad_gameobject_death_gap`. `GameObject.Die` is statically
traceable, but it is not a homogeneous MessageFrame family: one method mixes
tutorial death popups, checkpoint death text, journal death accomplishments,
debug confirmation, custom `EmitMessage` death messages, and `DidX` death
verbs. Existing `DeathReasonTranslationPatch` and `GameObjectDieTranslationPatch`
cover only death-reason parameters and companion queued death messages, so the
remaining route family should stay action-required under #719 rather than
runtime-required or covered.

Fresh residual output is
`/tmp/qudjp-issue719-residual-after-gameobject-die-reclassification.json`.
Fresh policy output is
`/tmp/qudjp-issue719-policy-after-gameobject-die-reclassification.json`.
Fresh follow-up output is
`/tmp/qudjp-issue719-followup-after-gameobject-die-reclassification.json`.
After regeneration, residual rows remain 323 / 2460 constructions, with
dispositions `likely_implementation_gap=267` rows / `2111` constructions and
`runtime_evidence_required=56` rows / `349` constructions. Policy closure
counts are `covered_by_owner_route=2318` rows / `17184` constructions,
`action_required=267` rows / `2111` constructions, and `runtime_required=56`
rows / `349` constructions.

Verification for this tranche passed focused GameObject.Die/broad-producer
policy tests, the full text-construction policy suite, and ruff. The previously
run tranche-wide XML/JSON syntax checks, markdown report check,
translation-token-check, and `git diff --check` remained clean before this
report update; markdown/diff checks were rerun after the report update.

Next row-count runtime bucket is `ui_popup_sink_route_split` 2 rows / 26
constructions. The largest remaining runtime construction buckets are
`generated_display_name_village_faction_runtime` 1 row / 24 constructions,
`producer_runtime_core_game_text_does_runtime` 1 / 24, and
`producer_runtime_world_part_does_message_frame_route_split` 1 / 19.

The Popup wrapper sink promotion tranche removes `ui_popup_sink_route_split`
2 rows / 26 constructions from residual. `Popup.NewPopupMessageAsync` and
`Popup.WaitNewPopupMessage` are generic `PopupMessage.ShowPopup(...)` wrappers:
they pass caller-owned `message`, `title`, `options`, and `inputDefault` through,
perform CP437/input escaping, and return selected/input text. They do not own a
route-local fixed English leaf, so the rows are exact sink-wrapper pass-through
rather than runtime-required owner work.

Fresh residual output is
`/tmp/qudjp-issue719-residual-after-popup-wrapper-promotion.json`.
Fresh policy output is
`/tmp/qudjp-issue719-policy-after-popup-wrapper-promotion.json`.
Fresh follow-up output is
`/tmp/qudjp-issue719-followup-after-popup-wrapper-promotion.json`.
After regeneration, residual rows are 321 / 2434 constructions, with
dispositions `likely_implementation_gap=267` rows / `2111` constructions and
`runtime_evidence_required=54` rows / `323` constructions. Policy closure
counts are `covered_by_owner_route=2320` rows / `17210` constructions,
`action_required=267` rows / `2111` constructions, and `runtime_required=54`
rows / `323` constructions.

Verification for this tranche passed focused Popup wrapper/final-child/tooltip
policy tests, the full text-construction policy suite, ruff, markdown report
check, translation-token-check, and `git diff --check`.

The largest remaining runtime construction buckets are
`generated_display_name_village_faction_runtime` 1 row / 24 constructions,
`producer_runtime_core_game_text_does_runtime` 1 / 24, and
`producer_runtime_world_part_does_message_frame_route_split` 1 / 19.

The GameText third-person death reason reclassification tranche moves
`producer_runtime_core_game_text_does_runtime` 1 row / 24 constructions to
`producer_runtime_core_game_text_third_person_death_gap`.
`GameText.RoughConvertSecondPersonToThirdPerson` is statically traceable: the
decompiled helper is a second-to-third-person death-reason grammar converter,
and the only callers found are `Physics.UpdateTemperature`, which assigns
`LastThirdPersonDeathReason` when no explicit `ThirdPersonDeathReason` exists,
and `GameObject.Die`, which uses the helper for companion death narration when
`ThirdPersonReason` is empty. Existing `DeathReasonTranslationPatch` translates
the `Die` reason parameters, but this helper-owned converted grammar still
needs a static owner/helper implementation.

Fresh residual output is
`/tmp/qudjp-issue719-residual-after-game-text-third-person-reclassification.json`.
Fresh policy output is
`/tmp/qudjp-issue719-policy-after-game-text-third-person-reclassification.json`.
Fresh follow-up output is
`/tmp/qudjp-issue719-followup-after-game-text-third-person-reclassification.json`.
After regeneration, residual rows remain 321 / 2434 constructions, with
dispositions `likely_implementation_gap=268` rows / `2135` constructions and
`runtime_evidence_required=53` rows / `299` constructions. Policy closure
counts are `covered_by_owner_route=2320` rows / `17210` constructions,
`action_required=268` rows / `2135` constructions, and `runtime_required=53`
rows / `299` constructions.

Verification for this tranche passed focused GameText/core-system/frame-Does
policy tests, the full text-construction policy suite, ruff, markdown report
check, translation-token-check, and `git diff --check`.

The largest remaining runtime construction buckets are now
`generated_display_name_village_faction_runtime` 1 row / 24 constructions,
`producer_runtime_world_part_does_message_frame_route_split` 1 / 19,
`generated_display_name_core_running_behavior_runtime` 1 / 18, and
`world_zone_display_name_runtime` 1 / 15.

The village faction display-name reclassification tranche moves
`generated_display_name_village_faction_runtime` 1 row / 24 constructions to
`generated_display_name_village_faction_gap`. `VillageBase.CreateVillageFaction`
is statically traceable: it keeps `Faction.Name` as the English faction key,
but directly owns the visible `Faction.DisplayName` assignment and
`FormatWithArticle` fallback. It assigns `DisplayName` from the generated
`newFactionName` snapshot property or falls back to `"villagers of " + name`.
Existing generated-name helpers translate villagers-of display-name phrases and
`ImportedFoodorDrink.generateFactionName` output, but the faction DisplayName
storage point still needs an owner/helper route.

Fresh residual output is
`/tmp/qudjp-issue719-residual-after-village-faction-reclassification.json`.
Fresh policy output is
`/tmp/qudjp-issue719-policy-after-village-faction-reclassification.json`.
Fresh follow-up output is
`/tmp/qudjp-issue719-followup-after-village-faction-reclassification.json`.
After regeneration, residual rows remain 321 / 2434 constructions, with
dispositions `likely_implementation_gap=269` rows / `2159` constructions and
`runtime_evidence_required=52` rows / `275` constructions. Policy closure
counts are `covered_by_owner_route=2320` rows / `17210` constructions,
`action_required=269` rows / `2159` constructions, and `runtime_required=52`
rows / `275` constructions.

Verification for this tranche passed focused generated-display-name child/final
child policy tests, the full text-construction policy suite, ruff, markdown
report check, translation-token-check, and `git diff --check`.

The largest remaining runtime construction buckets are now
`producer_runtime_world_part_does_message_frame_route_split` 1 row / 19
constructions, `generated_display_name_core_running_behavior_runtime` 1 / 18,
`world_zone_display_name_runtime` 1 / 15, and
`producer_broad_gameobject_regenera_runtime` 1 / 13.

The AutomatedExternalDefibrillator reclassification tranche moves
`producer_runtime_world_part_does_message_frame_route_split` 1 row / 19
constructions to `producer_runtime_world_part_defibrillator_gap`.
`AutomatedExternalDefibrillator.AttemptDefibrillate` is an exact static owner:
it builds `Actor.Fail` strings for missing skill, power/status failure, and
no-target branches, builds a `Popup.ShowYesNo` confirmation for non-cardiac
arrest targets, and emits `WDidXToYWithZ` success/dodge frames. Existing
MessageFrame tests cover the defibrillator frame verb, but the generated
item/target failure and confirmation branches still need owner-route handling.

Fresh residual output is
`/tmp/qudjp-issue719-residual-after-defibrillator-reclassification.json`.
Fresh policy output is
`/tmp/qudjp-issue719-policy-after-defibrillator-reclassification.json`.
Fresh follow-up output is
`/tmp/qudjp-issue719-followup-after-defibrillator-reclassification.json`.
After regeneration, residual rows remain 321 / 2434 constructions, with
dispositions `likely_implementation_gap=270` rows / `2178` constructions and
`runtime_evidence_required=51` rows / `256` constructions. Policy closure
counts are `covered_by_owner_route=2320` rows / `17210` constructions,
`action_required=270` rows / `2178` constructions, and `runtime_required=51`
rows / `256` constructions.

Verification for this tranche passed focused world-part mixed-route and
Does/MessageFrame policy tests, the full text-construction policy suite, ruff,
markdown report check, translation-token-check, and `git diff --check`.

The largest remaining runtime construction buckets are now
`generated_display_name_core_running_behavior_runtime` 1 row / 18 constructions,
`world_zone_display_name_runtime` 1 / 15,
`producer_broad_gameobject_regenera_runtime` 1 / 13, and
`generated_display_name_village_dynamic_quest_reward_runtime` 1 / 12.

The RunningBehavior event bridge promotion tranche removes
`generated_display_name_core_running_behavior_runtime` 1 row / 18
constructions from residual. `GetRunningBehaviorEvent.Retrieve` only bridges
legacy `GetRunningBehavior` events and pooled `GetRunningBehaviorEvent`
handlers, copies handler-provided `AbilityName`, `Verb`, `EffectDisplayName`,
`EffectMessageName`, duration, and springing state into out parameters, and
does not own a visible English display-name leaf. Static handlers such as
`Tactics_Run` and `RocketSkates` own the actual visible ability/effect strings.

Fresh residual output is
`/tmp/qudjp-issue719-residual-after-running-behavior-promotion.json`.
Fresh policy output is
`/tmp/qudjp-issue719-policy-after-running-behavior-promotion.json`.
Fresh follow-up output is
`/tmp/qudjp-issue719-followup-after-running-behavior-promotion.json`.
After regeneration, residual rows are 320 / 2416 constructions, with
dispositions `likely_implementation_gap=270` rows / `2178` constructions and
`runtime_evidence_required=50` rows / `238` constructions. Policy closure
counts are `covered_by_owner_route=2321` rows / `17228` constructions,
`action_required=270` rows / `2178` constructions, and `runtime_required=50`
rows / `238` constructions.

Verification for this tranche passed focused generated-display-name policy
tests and the full text-construction policy suite. The tranche-wide ruff,
markdown report check, translation-token-check, and `git diff --check` were
run after the following world-part mixed reclassification and cover this report
state.

The world-part mixed static-gap reclassification tranche moves 3 rows / 53
constructions from runtime-required to implementation-gap buckets:
`ShevaStarshipControl.CheckTimer` 18 constructions to
`producer_runtime_world_part_ship_ark_popup_gap`,
`MagazineAmmoLoader.FireEvent` 18 to
`producer_runtime_world_part_magazine_supply_gap`, and
`SpaceTimeVortex.ApplyVortex` 17 to
`producer_runtime_world_part_vortex_apply_gap`. All three owners are statically
traceable in decompiled source, but each still needs route-local implementation:
Sheva mixes launch countdown popups/messages and post-launch entry failure,
Magazine owns `SupplyIntegratedHostWithAmmo` prompt/transfer frames, and Vortex
mixes vortex-contact frames with the companion-sucked popup path.

Fresh residual output is
`/tmp/qudjp-issue719-residual-after-world-part-mixed-reclassification.json`.
Fresh policy output is
`/tmp/qudjp-issue719-policy-after-world-part-mixed-reclassification.json`.
Fresh follow-up output is
`/tmp/qudjp-issue719-followup-after-world-part-mixed-reclassification.json`.
After regeneration, residual rows remain 320 / 2416 constructions, with
dispositions `likely_implementation_gap=273` rows / `2231` constructions and
`runtime_evidence_required=47` rows / `185` constructions. Policy closure
counts are `covered_by_owner_route=2321` rows / `17228` constructions,
`action_required=273` rows / `2231` constructions, and `runtime_required=47`
rows / `185` constructions.

Verification for this tranche passed focused world-part mixed/popup-frame/
message-mixed policy tests and the full text-construction policy suite, with
ruff, markdown report check, translation-token-check, and `git diff --check`
run after the report update.

The largest remaining runtime construction buckets are now
`world_zone_display_name_runtime` 1 row / 15 constructions,
`producer_broad_gameobject_regenera_runtime` 1 / 13,
`generated_display_name_village_dynamic_quest_reward_runtime` 1 / 12, and
`producer_runtime_world_part_golem_popup_runtime` 1 / 10.

The top runtime static reclassification tranche removes 4 rows / 50
constructions from runtime-required. `WorldFactory.LoadWorldNode` 15
constructions is covered by the XML data route: the method only loads
`DisplayName` attributes from world XML, QudJP ships localized
`Mods/QudJP/Localization/Worlds.jp.xml`, and live zone-display output is
separately covered by `ZoneDisplayNameTranslationPatch`. `GameObject.FireEvent`
13 constructions is covered by the existing Regenera owner patch and tests for
cure/malady messages and regenerated-limb frames. The other two rows are
static implementation gaps: `VillageDynamicQuestContext.getQuestReward` 12
constructions directly assigns a generated village recoiler display name and
village reputation rewards, and `GolemQuestMound.DisplayOptions` 10
constructions owns the golem menu popup built from mound description text,
golem selection option text, and the route-local build command label.

Fresh residual output is
`/tmp/qudjp-issue719-residual-after-top-runtime-static-reclassification.json`.
Fresh policy output is
`/tmp/qudjp-issue719-policy-after-top-runtime-static-reclassification.json`.
Fresh follow-up output is
`/tmp/qudjp-issue719-followup-after-top-runtime-static-reclassification.json`.
After regeneration, residual rows are 318 / 2388 constructions, with
dispositions `likely_implementation_gap=275` rows / `2253` constructions and
`runtime_evidence_required=43` rows / `135` constructions. Policy closure
counts are `covered_by_owner_route=2323` rows / `17256` constructions,
`action_required=275` rows / `2253` constructions, and `runtime_required=43`
rows / `135` constructions.

Verification for this tranche passed focused generated-display-name child,
world-part popup, world-zone description, and broad-producer policy tests; the
full text-construction policy suite; ruff; markdown report check;
translation-token-check; and `git diff --check`.

The largest remaining runtime construction buckets are now
`action_description_runtime` 1 row / 9 constructions,
`producer_runtime_world_part_movement_popup_runtime` 1 / 9,
`ui_screen_world_generation_runtime` 1 / 9, and
`producer_runtime_inventory_action_message_frame_popup_route_split` 1 / 8.

The AutoAct/Physics/WorldGeneration static audit removes 2 rows / 18
constructions from runtime-required and replaces the remaining AutoAct runtime
row with exact static evidence. `Physics.ProcessTargetedMove` 9 constructions
is covered because its attack confirmation is already owned by
`PhysicsProcessTargetedMoveOwner` in `SingleCallsiteOwnerPopupTranslationPatch`,
and its `NoTeleport` popup body is localized data from
`HiddenObjects.jp.xml`. `WorldGenerationScreen._ShowWorldGenerationScreen` 9
constructions is covered because the visible quote/attribution strings come
from `BookUI.Books["Quotes"]`, while `Books.jp.xml` ships that book in
Japanese; the method's inline empty/space strings are placeholders. Static
tracing keeps `AutoAct.GetDescription(string,OngoingAction)` runtime-required:
the direct `Interrupt(...)` routes are covered by `AutoActTranslationPatch`, but
`AutoAct.ShouldHostilesInterrupt` can emit `GameObject.GenerateSpotMessage`
before `Interrupt()`, and the `o` branch returns
`action?.GetDescription() ?? "acting"`.

Fresh residual output is
`/tmp/qudjp-issue719-residual-after-autoact-physics-worldgen-reclassification.json`.
Fresh policy output is
`/tmp/qudjp-issue719-policy-after-autoact-physics-worldgen-reclassification.json`.
Fresh follow-up output is
`/tmp/qudjp-issue719-followup-after-autoact-physics-worldgen-reclassification.json`.
After regeneration, residual rows are 316 / 2370 constructions, with
dispositions `likely_implementation_gap=275` rows / `2253` constructions and
`runtime_evidence_required=41` rows / `117` constructions. Policy closure
counts are `covered_by_owner_route=2325` rows / `17274` constructions,
`action_required=275` rows / `2253` constructions, and `runtime_required=41`
rows / `117` constructions.

Verification for this tranche passed focused UI screen/world-part
popup/AutoAct-description/pure-popup policy tests, the full text-construction
policy suite, ruff, markdown report check, translation-token-check, and
`git diff --check`.

The largest remaining runtime construction buckets are now
`action_description_runtime` 1 row / 9 constructions,
`producer_runtime_inventory_action_message_frame_popup_route_split` 1 / 8,
`producer_runtime_world_part_does_popup_route_split` 1 / 8,
`producer_runtime_api_journal_wish_gospel_runtime` 1 / 7, and
`ui_screen_trade_highlight_runtime` 1 / 7.

The debug/data-route static reclassification tranche removes 5 rows / 35
constructions from runtime-required and moves 1 row / 8 constructions to an
implementation-gap bucket. Covered rows: `Brain.HandleEvent(InventoryActionEvent)`
2 constructions is debug-only inventory UI (`DebugInternals` /
`DebugAttitude`) and its adjacent thinking-out-loud branch is debug
MessageFrame output; `JournalAPI.WishGospelAccomplishments` 7 is a wish/debug
popup over stored `JournalAccomplishment` data already translated on the
JournalAPI add route; `TradeScreen.HandleHighlightObject` 7 binds
`DisplayNameSingle` plus weight/price glyph data; and
`TrembleEarthquakes.RocksFall` 5 is covered by the falling-rocks damage,
death-reason, and MessageFrame owner routes. The
`VehicleMeleeInfiltration.TryInfiltrate` row is now
`producer_runtime_world_part_vehicle_infiltration_popup_gap`: the owner method
statically owns both the hostile-entry confirmation popup and the infiltration
Does/EmitMessage success frame, but still needs focused owner-route coverage.

Fresh residual output is
`/tmp/qudjp-issue719-residual-after-debug-data-route-reclassification.json`.
Fresh policy output is
`/tmp/qudjp-issue719-policy-after-debug-data-route-reclassification.json`.
Fresh follow-up output is
`/tmp/qudjp-issue719-followup-after-debug-data-route-reclassification.json`.
After regeneration, residual rows are 312 / 2343 constructions, with
dispositions `likely_implementation_gap=276` rows / `2261` constructions and
`runtime_evidence_required=36` rows / `82` constructions. Policy closure
counts are `covered_by_owner_route=2329` rows / `17301` constructions,
`action_required=276` rows / `2261` constructions, and `runtime_required=36`
rows / `82` constructions.

Issue `#719` comment:
<https://github.com/ToaruPen/coq-japanese_stable/issues/719#issuecomment-4543595853>.

Verification for this tranche passed focused debug/data-route policy tests, the
full text-construction policy suite, ruff, translation-token-check, and
`git diff --check`.

The largest remaining runtime construction buckets are now
`action_description_runtime` 1 row / 9 constructions,
`history_text_filter_speech_status_runtime` 2 / 6,
`producer_runtime_ui_misc_popup_route_split` 2 / 6,
`ui_screen_hotkey_control_runtime` 2 / 6, and
`ui_screen_popup_message_runtime` 1 / 5.

The UI widget data-binding/control reclassification tranche removes 13 rows /
24 constructions from runtime-required and moves 3 rows / 10 constructions to
implementation-gap buckets. Covered rows: `PopupMessage.Update` input-state
clearing, `EquipmentLine.UpdateHotkey` and `InventoryLine.UpdateHotkey` hotkey
glyph labels, `InventoryLine.OnBeginDragObject` numeric/empty drag state,
`ProgressBar.Set` numeric progress, `ConsoleWindow.Update` debug console input,
`Notification.Routine` caller-owned title/text data,
`CyberneticsTerminalRow.setData/Update` terminal screen data already translated
upstream, `MissileWeaponAreaInfo.UpdateFrom` status data owned upstream,
`StatusBarStatBlock.Update/UpdateStats` stat abbreviations/numeric values, and
`ModManagerUI.SetBackButtonText` caller-owned bottom-context text. Static
implementation gaps: `LeftSideCategory.setData` remains exact UI row work
because current sink prerequisite coverage is observation-only,
`ModManagerUI.OnCancel` owns the changed-mod-configuration restart popup, and
`FrameworkSearchInput.ChangeValue` owns the default `Enter search text` prompt
title.

Fresh residual output is
`/tmp/qudjp-issue719-residual-after-ui-widget-reclassification.json`.
Fresh policy output is
`/tmp/qudjp-issue719-policy-after-ui-widget-reclassification.json`.
Fresh follow-up output is
`/tmp/qudjp-issue719-followup-after-ui-widget-reclassification.json`.
After regeneration, residual rows are 299 / 2319 constructions, with
dispositions `likely_implementation_gap=279` rows / `2271` constructions and
`runtime_evidence_required=20` rows / `48` constructions. Policy closure
counts are `covered_by_owner_route=2343` rows / `17329` constructions,
`action_required=279` rows / `2271` constructions, and `runtime_required=20`
rows / `48` constructions.

Issue `#719` comment:
<https://github.com/ToaruPen/coq-japanese_stable/issues/719#issuecomment-4543705455>.

Verification for this tranche passed focused UI data-bound/control/popup policy
tests, the full text-construction policy suite, ruff, markdown report check,
translation-token-check, and `git diff --check`.

The largest remaining runtime construction buckets are now
`action_description_runtime` 1 row / 9 constructions,
`history_text_filter_speech_status_runtime` 2 / 6,
`producer_runtime_ui_tutorial_popup_route_split` 1 / 2,
`producer_runtime_inventory_action_emit_route_split` 1 / 3, and
`active_effect_popup_route_split` 1 / 4.

The final sink/sentinel/static-owner reclassification tranche removes 3 rows /
4 constructions from residual and moves 2 rows / 7 constructions from
runtime-required to implementation-gap buckets. Covered rows:
`Extensions.ShowSuccess` is a final sink that forwards caller-owned `Message`
to `Popup.Show`, `MessageQueue.AddPlayerMessage(string,char,bool)` only converts
the color char and delegates to the string-color overload, and `FadeText.Update`
passes only the `<nohighlight>` tutorial control sentinel. Static
implementation gaps: `FungalSporeInfection.ChooseLimbForInfection` owns both
the no-infectable-body-parts popup and the generated choose-limb prompt, and
`DesalinationPellet.HandleEvent/PurifyLiquid` owns the `You drop` frame plus
liquid conversion message templates.

Fresh residual output is
`/tmp/qudjp-issue719-residual-after-final-sink-static-reclassification.json`.
Fresh policy output is
`/tmp/qudjp-issue719-policy-after-final-sink-static-reclassification.json`.
Fresh follow-up output is
`/tmp/qudjp-issue719-followup-after-final-sink-static-reclassification.json`.
After regeneration, residual rows are 296 / 2315 constructions, with
dispositions `likely_implementation_gap=281` rows / `2278` constructions and
`runtime_evidence_required=15` rows / `37` constructions. Policy closure
counts are `covered_by_owner_route=2346` rows / `17333` constructions,
`action_required=281` rows / `2278` constructions, and `runtime_required=15`
rows / `37` constructions.

Issue `#719` comment:
<https://github.com/ToaruPen/coq-japanese_stable/issues/719#issuecomment-4543738069>.

Verification for this tranche passed the focused final sink/sentinel/static
owner policy test, the full text-construction policy suite, ruff, markdown
report check, translation-token-check, and `git diff --check`.

The largest remaining runtime construction buckets are now
`action_description_runtime` 1 row / 9 constructions,
`generated_display_name_world_part_wish_debug_runtime` 1 / 4,
`history_text_filter_speech_status_runtime` 2 / 6,
`producer_broad_gameobject_hostile_spot_runtime` 1 / 3, and
`producer_runtime_world_part_wish_debug_popup_runtime` 2 / 4.

The AutoAct/hostile spot and WishCommand debug static reclassification tranche
moves 5 rows / 20 constructions from runtime-required to implementation-gap
buckets. Static implementation gaps: `AutoAct.GetDescription` ->
`action_description_autoact_gap`, `GameObject.ArePerceptibleHostilesNearby` ->
`producer_broad_gameobject_hostile_spot_gap`,
`PointedAsteriskBuilder.AsteriskWish` ->
`generated_display_name_world_part_wish_debug_gap`, and
`IZoneLandmark.WishCurrent` plus `ModExtradimensional.MakeExtradimensional`
-> `producer_runtime_world_part_wish_debug_popup_gap`. Decompiled sources show
the exact owner/helper routes: AutoAct action labels feed
`GameObject.GenerateSpotMessage`; the hostile-spot producer owns the
popup/queue sentence shape; the WishCommand helpers own fixed debug display
names, popup frame text, and picker title leaves.

Fresh residual output is
`/tmp/qudjp-issue719-residual-after-autoact-wish-static-reclassification.json`.
Fresh policy output is
`/tmp/qudjp-issue719-policy-after-autoact-wish-static-reclassification.json`.
Fresh follow-up output is
`/tmp/qudjp-issue719-followup-after-autoact-wish-static-reclassification.json`.
After regeneration, residual rows remain 296 / 2315 constructions, with
dispositions `likely_implementation_gap=286` rows / `2298` constructions and
`runtime_evidence_required=10` rows / `17` constructions. Policy closure
counts are `covered_by_owner_route=2346` rows / `17333` constructions,
`action_required=286` rows / `2298` constructions, and `runtime_required=10`
rows / `17` constructions.

Issue `#719` comment:
<https://github.com/ToaruPen/coq-japanese_stable/issues/719#issuecomment-4543766724>.

Verification for this tranche passed focused AutoAct/Wish/static route policy
tests, the full text-construction policy suite, ruff, markdown report check,
translation-token-check, and `git diff --check`.

The remaining runtime-required buckets are now
`history_text_filter_speech_status_runtime` 2 rows / 6 constructions,
`conversation_book_line_data_runtime` 1 / 2,
`generated_display_name_core_faction_runtime` 1 / 2,
`generated_display_name_core_metadata_runtime` 2 / 2,
`generated_display_name_core_possessive_runtime` 2 / 2,
`generated_display_name_world_part_item_mod_runtime` 1 / 2, and
`sifrah_popup_unused_base_game_runtime` 1 / 1.

The final runtime static audit tranche removes the remaining 10 rows / 17
constructions from runtime-required. Covered rows: `InsertRandomBookLine`
inserts Japanese `AlchemistMutterings` book data, `Faction.DisplayName`,
`Effect.Effect`, and `PointOfInterest.DisplayName` are data/sentinel metadata
routes, `PhaseSticky.HandleEvent` compares the base English `phase web` sentinel
while QudJP replaces PhaseWeb display data, and
`PsychicCombatSifrah.CheckOutOfOptions` belongs to a decompiled class marked not
used in the base game. Static implementation gaps: `TextFilters.Angry` and
`TextFilters.Lallated` now sit in `history_text_filter_speech_status_gap`, and
`GameObject.Poss` / `GameObject.poss` sit in
`generated_display_name_core_possessive_gap`.

Fresh PR residual output is
`/tmp/qudjp-issue719-pr-residual.json`.
Fresh PR policy output is
`/tmp/qudjp-issue719-pr-policy.json`.
Fresh PR follow-up output is
`/tmp/qudjp-issue719-pr-followup.json`.
After regeneration, residual rows are 290 / 2306 constructions, all with
disposition `likely_implementation_gap`; `runtime_evidence_required=0`. Policy
closure counts are `covered_by_owner_route=2351` rows / `17338`
constructions, `action_required=290` rows / `2306` constructions, and
`runtime_required=0`.

Issue `#719` comment:
<https://github.com/ToaruPen/coq-japanese_stable/issues/719#issuecomment-4543798973>.

Verification for this tranche passed focused final-runtime policy tests, the
full text-construction policy suite, ruff, markdown report check,
translation-token-check, and `git diff --check`.

## Closeout Gate

Issue #719 should not close solely because `unreviewed=0`. It can close only
after the current 290 residual rows are either:

- tracked in #719's consolidated residual bucket ledger,
- promoted to `covered_by_owner_route` or `runtime_required` by exact evidence,
  or
- documented as scoped implementation gaps inside #719.
