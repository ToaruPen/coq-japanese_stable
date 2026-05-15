# 2026-05-15 issue 699 static producer message-candidate policy pass

## Scope

This report resolves the `messages_candidate` side of the static producer
inventory after the issue-576 owner-patch closure work. It uses
`docs/static-producer-inventory.json` plus the current
`scripts/static_producer_closure.py` coverage overlay. Covered owner families
and covered mixed-family owner callsites are excluded before policy grouping.

This is not an owner-route patch batch. `AddPlayerMessage` remains
sink-observed and is not a fixed-leaf destination shortcut.

## Export command

```bash
uv run python scripts/static_producer_closure.py \
  --queue message-candidates \
  --format json \
  --limit 0
```

The text summary form is:

```bash
uv run python scripts/static_producer_closure.py \
  --queue message-candidates \
  --limit 0
```

## Current decision summary

After adding the accepted popup leaves in this batch, the remaining
`messages_candidate` text arguments group as follows:

| Decision | Text args | Destination | Policy result |
| --- | ---: | --- | --- |
| `existing_dictionary_coverage` | 542 | existing popup/message dictionaries | Already covered by exact dictionary keys; no new import work. |
| `existing_message_pattern_coverage` | 144 | existing message patterns | Already covered by current `messages.ja.json` patterns and L1/L2 tests. |
| `existing_does_verb_route_coverage` | 5 | existing DoesVerb route | Already covered by `verbs.ja.json` frame routing and DoesVerb tests. |
| `existing_owner_route_coverage` | 2 | existing owner route | Already covered by current owner patches and L2 tests. |
| `reject_pseudo_leaf` | 5 | none | Empty popup arguments; pseudo-leaf noise. |

Surface split:

| Surface | Text args |
| --- | ---: |
| `Popup.Show*` | 536 |
| `EmitMessage` | 162 |

## Accepted popup leaves

These were fixed `Popup.Show*` literals with no exact dictionary coverage, so
they were added to `Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json`.
Concrete mutation range leaves such as `That is out of range! (8 squares)` were
kept out of the exact-leaf additions because the existing parameterized
out-of-range popup entries already cover them.

| Source | Key |
| --- | --- |
| `CodeRedemptionManager.cs` | `That code is invalid.` |
| `CodeRedemptionManager.cs` | `Your new pet is ready to love.` |
| `Qud.UI/AbilityManagerScreen.cs` | `You have no activated abilities.` |
| `MetricsManager.cs` | <code>{{R&#124;Error}}</code> |
| `Qud.UI/MouseBlocker.cs` | `Mouse input is disabled but you clicked on the screen several times. Would you like to enable mouse input?` |
| `XRL.CharacterBuilds.Qud/QudChartypeModule.cs` | `There is no valid last character to use.` |
| `XRL.Core/XRLCore.cs` | <code> {{W&#124;C}} - Copy seed  {{W&#124;ESC}} - Exit </code> |
| `XRL.UI/Popup.cs` | `Choose color` |
| `XRL.World.Parts/Crayons.cs` | `What color do you want to draw with?` |
| `XRL.World.Parts/TattooGun.cs` | `Choose a primary color.` |
| `XRL.World.Parts/TattooGun.cs` | `Choose a secondary color.` |
| `XRL.World.Tinkering/TinkeringHelpers.cs` | `Choose a color for your maker's mark.` |
| `XRL.World/Gender.cs` | `That name is already in use.` |
| `XRL/XRLGame.cs` | `There was a fatal exception attempting to save your game. Caves of Qud attempted to recover your prior save. You probably want to close the game and reload your most recent save. It'd be helpful to send the save and logs to support@freeholdgames.com` |

## Rejected and deferred groups

- The five empty popup arguments are pseudo-leaf rows from color/prompt helper
  calls. They are intentionally not dictionary entries.
- 144 generated `EmitMessage` rows are now classified as existing message-pattern
  coverage because current `messages.ja.json` patterns and tests already prove
  the emitted shapes. The covered groups are LiquidProteanGunk, LiquidWarmStatic
  glitch and wish-effect messages, Bleeding stop messages, FungalCureQueasy, IrisdualCallow,
  Luminous, Nosebleed, Carapace loosen messages, CherubimLock, Combat reach/pass-through
  messages, CyberneticsButcherableCybernetic butcher/rip messages,
  CyberneticsHolographicVisage, DecoyHologramEmitter image messages, Door open/close messages,
  ForceBubble, ChevronWall/HexCrystal, CursedCellSocket, FungalInfection skin
  messages, Garbage rifle-through messages, GeomagneticDisc, Harvestable,
  HelpingHands, Joppa/SixDay zealot speech, MagazineAmmoLoader reload/no ammo
  messages, Mutations chimeric growth, RocketSkates, SoupSludge,
  Tinkering_Mine disarm results, VehicleMeleeInfiltration, SpaceTimeVortex,
  and the MissileWeapon vital-area, penetration, suppressive/flattening fire,
  wound/disorient, wild-shot, pass-by, hit-output, direction, critical, and
  outcome-fragment messages. `ShevaStarshipControl.CheckTimer` is covered by the
  new `Exodus launch in N...` pattern.
- Five generated `EmitMessage` rows are classified as existing DoesVerb route
  coverage: `Campfire.Extinguish`, `FungalInfection.FireEvent` line 77, and
  `MagazineAmmoLoader.HandleEvent` line 361, plus the two `Chat.PerformChat`
  speech-frame rows. For `Chat`, the dynamic speech payload is preserved while
  only the generated `says, '{{|...}}'.` frame is counted as covered.
- Two generated `EmitMessage` rows are classified as existing owner-route
  coverage: `DesalinationPellet.HandleEvent` and
  `DeployableInfrastructure.DeployOne`.
- No `messages_candidate` text arguments remain deferred in this policy export.
- `EmitMessage` static literals with exact dictionary coverage are counted as
  existing coverage, not new additions.

## Verification targets

This batch should pass:

```bash
just static-producer-check
just localization-check
just translation-token-check
just release-note-check origin/main HEAD
```
