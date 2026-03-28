# Display-Path Ownership

This document defines, per route family, what constitutes proof that the player sees translated text on screen.

## Classification

Each route family falls into one of three categories:

| Category | Definition | L2 proves | L3 role |
|----------|-----------|-----------|---------|
| **Statically-provable** | DummyTarget verifies the final returned/displayed value end-to-end | Owner translates, sink observation = 0, return value is Japanese | Regression only |
| **Narrowable** | L2 proves ownership and sink suppression; UI rendering depends on Unity runtime | Owner translates, sink suppressed | Screen navigation to visually confirm |
| **Runtime-required** | Route fires under specific game state; ownership may be producer-side or observation-only depending on the family | Producer translation logic or observation-only contract, as declared in the matrix | Reproduction steps with specific game actions |

## Route Ownership Matrix

| Family | Category | Patch type | Key patches |
|--------|----------|-----------|-------------|
| Conversation display text | Statically-provable | Postfix, `ref __result` | ConversationDisplayTextPatch |
| Descriptions/tooltips | Statically-provable | Postfix, `ref __result` / StringBuilder | DescriptionShortDescriptionPatch, DescriptionLongDescriptionPatch, LookTooltipContentPatch |
| Death reason | Runtime-required | Prefix, `ref` args rewrite | DeathReasonTranslationPatch |
| Zone display names | Statically-provable | Postfix, `ref __result` | ZoneDisplayNameTranslationPatch |
| Journal entry display | Statically-provable | Postfix, `ref __result` | JournalEntryDisplayTextPatch, JournalMapNoteDisplayTextPatch |
| Popup message | Statically-provable | Prefix, `__args` rewrite | PopupMessageTranslationPatch |
| Inventory/equipment | Narrowable | Postfix, field update | InventoryAndEquipmentStatusScreenTranslationPatch |
| Character status | Narrowable | Postfix, field update | CharacterStatusScreenTranslationPatch, CharacterStatusScreenMutationDetailsPatch |
| Skills/powers | Narrowable | Postfix, field/text update | SkillsAndPowersStatusScreenTranslationPatch, SkillsAndPowersStatusScreenDetailsPatch |
| Factions | Narrowable | Postfix, field update | FactionsLineDataTranslationPatch, FactionsLineTranslationPatch, FactionsStatusScreenTranslationPatch |
| Options screen | Narrowable | Postfix, field update | OptionsLocalizationPatch |
| Pick game object screen | Narrowable | Postfix, field update | PickGameObjectScreenTranslationPatch |
| Menu bottom context | Narrowable | Prefix, field update | QudMenuBottomContextTranslationPatch |
| Player status bar / ability bar | Narrowable | Postfix, dictionary/text update | PlayerStatusBarProducerTranslationPatch, AbilityBarAfterRenderTranslationPatch |
| Popup conversation | Narrowable | Prefix, `__args` rewrite | PopupTranslationPatch, ConversationDisplayTextPatch |
| Popup (ShowBlock/ShowOptionList) | Runtime-required | Prefix, `__args` rewrite | PopupTranslationPatch |
| Message log | Runtime-required | Observation-only | MessageLogPatch |
| UITextSkin | Runtime-required | Observation-only sink | UITextSkinTranslationPatch |
| SinkPrereq | Runtime-required | Observation-only near-sink | SinkPrereqSetDataTranslationPatch, SinkPrereqUiMethodTranslationPatch |

### Disabled (not subject to L2/L3 checklists until re-enabled)

| Route family | Mechanism | Owner patch |
|-------------|-----------|-------------|
| Historic string expansion | Postfix, `ref __result` (TargetMethods yields nothing) | HistoricStringExpanderPatch |

## Layer Proof Rules

| Layer | What it proves | What it does not prove |
|-------|---------------|----------------------|
| **L1** | Translation logic, color code preservation, pattern matching | Patch target, game type compatibility |
| **L2** | Owner translates, sink observation suppressed, return value/field is Japanese | UI rendering, font/glyph display |
| **L2G** | TargetMethod resolves on real DLL, signature matches | Patch body result, display |
| **L3** | Final screen shows Japanese text, no log errors | (Terminal proof layer) |

## L2 Ownership Assertion Pattern

For any route claiming display-path ownership, L2 tests must verify three points:

1. **Translation** — Patched return value or field contains translated Japanese text.
2. **Route recording** — `DynamicTextObservability.GetRouteFamilyHitCountForTests(route, family)` is greater than 0.
3. **Sink suppression** — `SinkObservation.GetHitCountForTests(sink, route, ObservationOnlyDetail, source, stripped)` is 0.

For observation-only routes, the contract test verifies:

1. **Pass-through** — Arguments or return value are NOT modified.
2. **Observation logged** — `SinkObservation.GetHitCountForTests(sink, route, ObservationOnlyDetail, source, stripped)` is greater than 0.

## When Is a Route "Done"?

Use this checklist before marking a route family as complete:

### Statically-provable families

- [ ] L2 test with 3-point ownership assertion exists and passes
- [ ] L1 test covers the translation logic (if non-trivial)
- [ ] L2G confirms TargetMethod resolution (if not already covered)

### Narrowable families

- [ ] L2 test with 3-point ownership assertion exists and passes
- [ ] L3 manual verification has been performed at least once
- [ ] L3 verification steps are documented below

### Runtime-required families

Runtime-required rows split into two subcases. Follow the contract declared in the matrix row for that family.

#### Producer-owned runtime-required families

- [ ] L2 test with 3-point ownership assertion exists and passes
- [ ] L3 reproduction steps are documented below
- [ ] L3 verification has been performed for known scenarios

#### Observation-only runtime-required families

- [ ] L2 test verifies observation-only contract (pass-through + observation logged)
- [ ] L3 reproduction steps are documented below
- [ ] L3 verification has been performed for known scenarios

## L3 Reproduction Procedures

### Inventory/equipment (narrowable)

1. `dotnet build && python3 scripts/sync_mod.py`
2. Launch via `scripts/launch_rosetta.sh` (Apple Silicon) or direct launch
3. Load a save with items in inventory
4. Open inventory (`i`) — verify menu options (Display Options, Quick Drop, etc.) are Japanese
5. Open equipment (`e`) — verify same menu options
6. Switch tabs and verify each tab header
7. Check `Player.log` for `MODWARN` or `[QudJP]` errors
8. Automated pre-check: `python3 scripts/verify_inventory.py`

### Popup ShowBlock/ShowOptionList (runtime-required)

1. `dotnet build && python3 scripts/sync_mod.py`
2. Launch via `scripts/launch_rosetta.sh`
3. **Quit popup**: Press Esc from the main game screen — verify popup body and button text
4. **Save delete popup**: Go to load screen, attempt to delete a save — verify prompt text
5. **Attack prompt**: Attempt to attack a friendly NPC — verify "Do you really want to attack..." text
6. **Skill point popup**: Level up and attempt to buy a skill without enough points — verify message
7. **Death popup**: Die in combat — verify death message and menu options
8. Check `Player.log` for unclaimed sink observations from `PopupTranslationPatch`

### Message log (runtime-required)

1. `dotnet build && python3 scripts/sync_mod.py`
2. Launch via `scripts/launch_rosetta.sh`
3. **Combat messages**: Enter combat with any creature — verify hit/miss/damage messages in the message log
4. **Status messages**: Pick up, drop, eat, or drink an item — verify feedback messages
5. **Environmental messages**: Walk into a zone with temperature or radiation — verify notifications
6. **Zone transition**: Move between zones — verify zone entry messages
7. Check `Player.log` — verify `[QudJP] MessagePatternTranslator: no pattern for` entries do NOT appear (their presence indicates unclaimed messages that lack a producer-side translation)
8. Check `Player.log` for `[QudJP] SinkObserve/v1: sink='MessageLogPatch'` entries (observation-only sink recording)

### UITextSkin (runtime-required)

1. `dotnet build && python3 scripts/sync_mod.py`
2. Launch via `scripts/launch_rosetta.sh`
3. **Character creation**: Start a new game — verify all module titles, descriptions, and button labels through each CharGen step
4. **Main menu**: Verify menu option labels and descriptions
5. **Status screens**: Open character status (`x`), skills (`s`), factions — verify screen headers and section titles
6. **Conversation UI**: Talk to an NPC — verify conversation text renders correctly
7. **Options screen**: Open options — verify setting labels and help text
8. Check `Player.log` for `[QudJP] SinkObserve/v1: sink='UITextSkinTranslationPatch'` entries with route context (e.g., `route='CharGenLocalizationPatch'`, `route='InventoryLocalizationPatch'`)

### SinkPrereq (runtime-required)

1. `dotnet build && python3 scripts/sync_mod.py`
2. Launch via `scripts/launch_rosetta.sh`
3. **setData route** (SinkPrereqSetDataTranslationPatch):
   - Open inventory and hover over items — verify attribute text, description text, and mod descriptions
   - Open tinkering screen — verify tinker detail lines (bit costs, requirements)
   - Open character effects list — verify effect line descriptions
   - Open category scrollers (abilities, mutations) — verify category titles
4. **UI method route** (SinkPrereqUiMethodTranslationPatch):
   - Trade with an NPC — verify trade screen highlight descriptions
   - Open ability manager — verify ability selection detail text
   - Open cybernetics terminal (if available) — verify terminal row descriptions
   - Check player status bar for zone text and detail text
5. Check `Player.log` for `[QudJP] SinkObserve/v1: sink='UITextSkinTranslationPatch'` entries where the route context is `SinkPrereqSetDataTranslationPatch` or `SinkPrereqUiMethodTranslationPatch` (these patches delegate to UITextSkinTranslationPatch via `TranslatePreservingColors`)

### General L3 verification notes

- Always use Rosetta launch on Apple Silicon — native ARM64 Harmony results are not accepted as evidence.
- Check `Player.log` for `[QudJP]` errors, `Missing glyph`, and `MODWARN` entries.
- Screenshot evidence is recommended for first-time verification of narrowable families.
