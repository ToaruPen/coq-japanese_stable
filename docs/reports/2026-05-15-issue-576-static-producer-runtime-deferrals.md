# 2026-05-15 issue-576 static producer runtime deferrals

This report records static-producer rows that remain visible in
`docs/static-producer-inventory.json` as `runtime_required`, but are not
actionable owner-route implementation work without runtime evidence or a
dedicated generator owner. The closure overlay keeps them explicit through
`DEFERRED_RUNTIME_CALLSITES` and removes them from `just
static-producer-owner-queue` so the queue reflects static owner work.

## Campfire cooking runtime deferrals

`XRL.World.Parts.Campfire.CookPresetMeal`, `CookFromIngredients`, and
`CookFromRecipe` mix fixed popup leaves, already-covered owner-generated popup
frames, and runtime cooking text.

Deferred runtime callsites:

- `CookPresetMeal`: line `738`,
  `HistoricStringExpander.ExpandString("<spice.cooking.ate.!random>")`
- `CookFromIngredients`: lines `1012`, `1039`, and `1068`, `DescribeMeal(list3)`
- `CookFromIngredients`: lines `1017`, `1023`, `1044`, `1050`, `1075`, and
  `1082`, `HistoricStringExpander.ExpandString("<spice.cooking.ate.!random>")`
- `CookFromRecipe`: lines `1221` and `1228`,
  `HistoricStringExpander.ExpandString("<spice.cooking.ate.!random>")`

Static decision: `DescribeMeal` depends on selected runtime ingredients and
cooking template expansion. The `HistoricStringExpander` calls depend on
runtime HistorySpice selection. These rows should stay outside owner-action
closure until a scoped cooking/HistorySpice owner route is implemented.

## XRLCore PlayerTurn runtime deferrals

`XRL.Core.XRLCore.PlayerTurn` already has covered owner callsites for the HP
warning, invalid inventory object, invalid wait count, autoattack/flee/reach
path popups, nearby-hostile message, and terse/verbose message toggles.

Deferred runtime callsites:

- line `945`, `text4`
- line `1057`, generated game-mode, turn, and world-seed popup block plus copy
  footer state
- line `1335`, `currentCell8.ParentZone.SpecialUpMessage()`
- line `1780`, `text5 ?? ("You need to reload! (" + ControlManager...)`

Static decision: final text depends on runtime ability/use failure state, game
state, zone-supplied movement text, or active control binding descriptions.

## Inventory FireEvent runtime deferrals

`XRL.World.Parts.Inventory.FireEvent` already has covered owner callsites for
the graveyard-zone queued recovery line and container ownership popup.

Deferred runtime callsites:

- lines `1657`, `1726`, and `1745`, `FailureMessage2`
- line `1974`, `text3`
- lines `2036`, `2046`, `2089`, `2100`, and `2147`, `FailureMessage3`

Static decision: these messages are supplied by runtime inventory action and
equipment procedure branches. They need route-specific runtime evidence or a
separate failure-message owner before implementation.

## Physics and ConversationScript runtime deferrals

`XRL.World.Parts.Physics.HandleEvent` already has covered owner callsites for
object-entering-cell messages and inventory-action popups. `ProcessTargetedMove`
already has covered owner callsite coverage for the attack-confirm popup.

Owner-covered Physics callsites:

- `HandleEvent` line `2582`, `OUCH! You collide with ...`, is handled by
  `PhysicsObjectEnteringCellTranslationPatch` through the repository message
  pattern for collision text.
- `HandleEvent` lines `2593` and `2598` are handled by the same owner route for
  world-map traversal and blocked-way messages.
- `ProcessTargetedMove` line `3938` is handled by
  `SingleCallsiteOwnerPopupTranslationPatch` for the attack-confirm popup.

Deferred Physics runtime callsites:

- `HandleEvent` line `2589`, `ParentObject.GetTag("OverlandBlockMessage")`
- `HandleEvent` line `2847`, `GetDebugInternalsEvent.GetFor(ParentObject)`
- `ProcessTargetedMove` line `3912`,
  `TargetCell.GetFirstObjectWithPropertyOrTag("NoTeleport").GetPropertyOrTag("NoTeleport")`

`XRL.World.Parts.ConversationScript` already has covered owner callsites for
the generated physical and mental conversation popups.

Deferred ConversationScript runtime callsites:

- `IsPhysicalConversationPossible` lines `273` and `323`, runtime `Message` /
  `FailureMessage`
- `IsMentalConversationPossible` lines `363` and `381`, runtime `Message` /
  `FailureMessage`

Static decision: these shapes are supplied by runtime object tags, debug
internals, target-cell properties, conversation event messages, or failure
message variables. They are not fixed popup leaves and should not be counted as
owner-action work until runtime evidence identifies a concrete owner route.

## Next owner queue runtime deferrals

After the high-volume mixed families above were split, the next queue leaders
were small mixed families whose remaining owner-action rows were also
runtime-only or pseudo-runtime rows.

`XRL.World.Parts.Crayons.HandleEvent`:

- line `63`, `Popup.ShowColorPicker("What color do you want to draw with?", 0,
  null, 60, ..., "", includeNone: false)`.
- Static decision: the visible title is an exact `ui-popup.ja.json` leaf. The
  callsite is classified as `runtime_required` because Roslyn records the
  `null` intro and empty spacing text formal arguments; those are not visible
  owner-action work.

`XRL.World.Parts.Chat.PerformChat`:

- lines `182`, `191`, and `206`, direct `Says` payload pass-through from star
  and bracket-authored chat text.
- Static decision: the generated speech-frame rows are covered separately by
  DoesVerb route evidence. The direct payload rows are runtime-authored object
  data and need data-source/runtime proof before translation.

`XRL.World.Parts.ITeleporter.AttemptTeleport`:

- line `297`, `customTeleportFailure`.
- Static decision: this text is supplied by the active teleporter implementation
  at runtime. The fixed and generated owner popups plus queued activation line
  are already covered by `ITeleporterTranslationPatch`.

`XRL.World.Parts.MissileWeapon.FireEvent`:

- lines `2472` and `2490`, `Message` / `Message2` returned by ammo/load events.
- Static decision: these are runtime event failure messages. The fixed/generic
  shot-wild and pass-by rows are handled through existing message leaves and
  message patterns.

`XRL.World.Parts.Garbage.AttemptRifle`:

- line `148`, `text2`, the trash-divining journal note message.
- Static decision: the prefix frame is generated from actor/object/direction,
  then concatenated with `randomUnrevealedNote.Text`. The actor rifling rows
  and player rifling result patterns are covered separately; the journal-note
  body remains runtime data.

## Small mixed-family runtime deferrals

The next queue pass found additional small mixed families where owner-generated
rows are already covered, fixed leaves are already in dictionaries, and only
runtime/pseudo-runtime rows kept the source file in the owner queue.

`XRL.World.Parts.TattooGun.AttemptTattoo`:

- line `177`, primary color `Popup.ShowColorPicker`.
- Static decision: the visible primary-color title is an exact popup leaf. The
  owner-generated tattoo success lines remain covered by
  `TattooGunTranslationPatch`; the color-picker null/empty optional arguments
  are not owner-action work.

`XRL.UI.Popup.AskStringAsync`:

- line `1395`, `ShowColorPickerAsync("Choose color", ..., StripFormatting(result))`.
- Static decision: the visible title is an exact popup leaf. The preview content
  is runtime user input and should not be treated as a static owner queue item.

`XRL.World.Tinkering.TinkeringHelpers.CheckMakersMark`:

- line `99`, `ShowColorPicker("Choose a color for your maker's mark.", ..., text)`.
- Static decision: the visible title is an exact popup leaf. The maker-mark
  preview value is runtime-selected.

`XRL.UI.TradeUI.ShowTradeScreen`:

- line `1087`, `stringBuilder.ToString()`.
- Static decision: haggle text is assembled from runtime trade offer state.
  Existing trade-owner popups are covered separately.

`XRL.World.Parts.LiquidVolume.HandleEvent`:

- lines `3192`, `3227`, and `3538`, liquid interaction string builders.
- Static decision: ownership and fixed queue/popup fragments are covered by
  existing LiquidVolume owner routes; these rows are runtime detail builders.

`XRL.World.Parts.RandomAltarBaetyl.BaetylWantsSacrifice`:

- lines `759` and `805`, sacrifice/reward string builders.
- Static decision: fixed and generated reward popups are covered separately;
  these rows depend on runtime sacrifice/reward details.

`XRL.World.Parts.Stomach.FireEvent`:

- line `538`, runtime `stringBuilder`.
- Static decision: fixed moisture-loss messages are covered separately; this row
  is runtime drinking/eating detail text.

`XRL.SifrahGame.UseInsight`:

- line `856`, runtime `stringBuilder.ToString()`.
- Static decision: option-elimination text depends on the current Sifrah option
  state. The fixed no-op insight leaves are dictionary-covered.

## Final small mixed-family runtime deferrals

The remaining owner queue pass reduced to small mixed families where all
`owner_patch_required` rows were already covered by owner-route tests and the
only actionable residue was scanner `runtime_required` text. These rows are
registered as callsite-level runtime deferrals rather than full-family owner
closure.

- `XRL.UI.AbilityManager.Show` line `483`: ability `NotUsableDescription`
  supplied by the selected runtime `ActivatedAbilityEntry`.
- `XRL.UI.OptionsUI.Show` line `546`: restart prompt assembled from runtime
  option display text.
- `WaterRitualRandomMutation.HandleEvent` line `98`: conversation-authored
  reward text expanded with runtime speaker and mutation data.
- `ShadeOil_Tonic.FireEvent` line `196`: phasing prompt assembled from runtime
  body-part and source-object context.
- `Rifle_SuppressiveFire.FireEvent` line `64` and
  `Rifle_WoundingFire.FireEvent` line `60`: reload popup text supplied by
  runtime ammo events or current control binding text.
- `FixitSpray.HandleEvent` line `90`: fallback popup supplied by runtime
  inventory-action message data.
- `IZoneLandmark.WishCurrent` line `139`: landmark wish output built from
  runtime zone/location landmark data.
- `TerrainTravel.HandleEvent` lines `120` and `124`: encounter prompts supplied
  by runtime encounter definitions.
- `ZoneManager.SetActiveZone` lines `1889` and `1912`: zone display names and
  journal-note entry text are runtime/source-data payloads. Line `1885` remains
  owner-covered separately because it adds the generated time suffix frame.
- `MetricsManager.LogException` line `563`: exception popup body is runtime
  diagnostic text; the `{{R|Error}}` title is a fixed popup leaf.
- `Scores.Show` line `265`: high-score details are saved scoreboard detail
  text.
- `StatusScreen.Show` line `460`: psychic glimmer description generated from
  the runtime glimmer value.
- `WaterRitualBuySecret.RevealEntry` line `64`: gossip text combines
  HistorySpice lead-in and runtime journal observation text.
- `Snapjaw_Howl.FireEvent` line `146`: affected-object list assembled from
  runtime nearby objects.
- `ActivatedAbilityEntry.TrySendCommandEventOnPlayer` line `555`: ability
  failure text returned by runtime command handling.
- `DestroyOnUnequip.HandleEvent` line `33`: part-authored message
  variable-replaced against the runtime object.
- `Examiner.ResultCriticalFailure` line `951`: critical failure text selected
  from runtime examination/object state.
- `Fetches.HandleEvent` line `35`: configured sniff message variable-replaced
  against the runtime object.
- `MagnetizedApplicator.HandleEvent` line `66`: callback popup supplied by
  runtime event message data.
- `Mutations.WishMutation` line `976`: wish/debug failure text composed from
  runtime wish arguments.
- `NephalProperties.HandleEvent` line `161`: configured phase message
  variable-replaced with runtime actor/object data.
- `NeutronFluxContainment.HandleEvent` line `99`: warning body built from
  runtime containment state.
- `StairsDown.CheckPullDown` line `443`: pull-down message supplied by
  part/runtime data.
- `ThiefBot.FireEvent` line `69`: steal message assembled from runtime
  actor/target/item data.
- `Tonic.HandleEvent` line `212`: tonic action popup supplied by part/runtime
  message data.
- `GameObjectFactory.HandleBlueprintXML` line `1761`: wish/debug output dumps
  runtime blueprint XML.
- `PopulationManager.WishGenerate` line `1015`: wish/debug generation report
  assembled from runtime generated-object counts/results.
- `XRLGame.LoadGame` line `1916`: load-game failure popup displays runtime
  save/mod/exception context.

One near miss was not deferred: `RealityStabilized.FailedToContest` line `477`
is a local fixed string-builder shape, not runtime payload. It is covered by
`RealityStabilizedEventTranslationPatch` with focused popup tests instead.
