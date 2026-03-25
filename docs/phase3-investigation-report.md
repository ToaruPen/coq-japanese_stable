# Phase 3 Investigation Report: 158 Unresolved Translation Sites

**Date**: 2026-03-25
**Scope**: Static analysis of all 158 `unresolved` sites in `docs/candidate-inventory.json`
**Method**: Decompiled source tracing at `~/Dev/coq-decompiled/`, cross-referenced with existing QudJP patches

## Executive Summary

Of 158 sites originally classified as `runtime_dependent` (Issue #106), **only 43 (27%) are genuinely runtime-dependent**. The remaining 115 sites break down as:

| Classification | Count | % | Action |
|---------------|-------|---|--------|
| STATIC_PARTIAL | 51 | 32% | Translate template, handle runtime slots |
| EXCLUDED | 47 | 30% | No translation needed |
| TRULY_RUNTIME | 43 | 27% | Requires runtime investigation or upstream patch |
| STATIC_RESOLVABLE | 17 | 11% | Direct dictionary entry |
| **Total** | **158** | **100%** | |

**Key finding**: 68 sites (43%) are actionable without any runtime investigation — 51 via template translation and 17 via dictionary entries. 47 sites need no action at all (numeric, upstream-covered, infrastructure).

---

## Classification by Sink Type

### EmitMessage Sites (55 total)

| Classification | Count | Examples |
|---------------|-------|---------|
| STATIC_PARTIAL | 37 | PowerSwitch (7), RemotePowerSwitch (4), ForceProjector (2), Consumer, Explores, LootOnStep (2) |
| TRULY_RUNTIME | 15 | Chat (3), Garbage (2), LiquidVolume, MissileWeapon (2), Stomach, IrritableGenome |
| EXCLUDED | 2 | ReplaceBuilder (2) — infrastructure dispatch, not text-producing |
| STATIC_RESOLVABLE | 1 | Preacher L143 ("You hear inaudible mumbling.") |

**Dominant pattern**: 37/55 EmitMessage sites use blueprint part fields (e.g. `ActivateSuccessMessage`, `SpawnMessage`) expanded via `GameText.VariableReplace`. Templates are either visible as C# field defaults (22 sites) or set from XML blueprints with null C# defaults (15 sites).

### SetText Sites (98 total)

| Classification | Count | Examples |
|---------------|-------|---------|
| EXCLUDED | 41 | Numeric displays (7), DisplayName passthrough (10), upstream-covered (12), user input (1), infrastructure (11) |
| TRULY_RUNTIME | 28 | Framework controls (7), Notification (2), Tutorial (3), LeftSideCategory (4), ObjectFinder (3) |
| STATIC_RESOLVABLE | 16 | Book content (5), Achievements (2), Options titles (3), ButtonBar labels, KeyMenuOption (2) |
| STATIC_PARTIAL | 13 | AbilityBar (4), PlayerStatusBar (4), AskNumberScreen, CyberneticsTerminal, SkillsAndPowers (2) |

**Dominant pattern**: 41/98 SetText sites are EXCLUDED — most are numeric `.ToString()`, upstream `DisplayName` passthrough already covered by `GetDisplayNamePatch`, or message log text already translated by `MessageLogPatch`.

### GetShort/LongDescription Sites (5 total)

| Classification | Count | Site |
|---------------|-------|------|
| EXCLUDED | 4 | Look.cs L259, L532 (covered by `DescriptionLongDescriptionPatch`); TinkerData L129, L173 (covered by `DescriptionShortDescriptionPatch`) |
| STATIC_PARTIAL | 1 | QudCyberneticsModuleWindow L70 (`CyberneticsChoice.GetLongDescription` — not `Description.GetLongDescription`, so not covered by existing patch) |

---

## Detailed Classification: STATIC_PARTIAL (51 sites)

### Blueprint VariableReplace Cluster (37 EmitMessage sites)

These sites read message templates from C# field defaults or XML blueprint fields, then expand via `GameText.VariableReplace` with runtime object names.

#### With visible C# default templates (22 sites)

| File | Line | Field | Default Template |
|------|------|-------|-----------------|
| PowerSwitch.cs | 536 | KeyObjectAccessMessage | `=subject.The==subject.name= =verb:recognize= your =object.name=.` |
| PowerSwitch.cs | 544 | PsychometryAccessMessage | `{{g\|You touch =subject.the==subject.name= and recall =pronouns.possessive= passcode. =pronouns.Subjective= =verb:beep:afterpronoun= warmly.}}` |
| PowerSwitch.cs | 571 | AccessFailureMessage | `{{r\|A loud buzz is emitted. The unauthorized glyph flashes on the display.}}` |
| PowerSwitch.cs | 784 | ActivateSuccessMessage | `=subject.The==subject.name= =verb:start= up with a hum.` |
| PowerSwitch.cs | 789 | ActivateFailureMessage | `Nothing happens.` |
| PowerSwitch.cs | 816 | DeactivateSuccessMessage | `=subject.The==subject.name= =verb:shut= down with a whir.` |
| PowerSwitch.cs | 821 | DeactivateFailureMessage | `Nothing happens.` |
| RemotePowerSwitch.cs | 68 | (delegates to PowerSwitch) | ActivateSuccessMessage |
| RemotePowerSwitch.cs | 73 | (delegates to PowerSwitch) | ActivateFailureMessage |
| RemotePowerSwitch.cs | 94 | (delegates to PowerSwitch) | DeactivateSuccessMessage |
| RemotePowerSwitch.cs | 99 | (delegates to PowerSwitch) | DeactivateFailureMessage |
| ForceProjector.cs | 634 | PsychometryAccessMessage | `You touch =subject.t= and recall =pronouns.possessive= passcode. =pronouns.Subjective= =verb:beep:afterpronoun= warmly.` |
| ForceProjector.cs | 640 | AccessFailureMessage | `A loud buzz is emitted. The unauthorized glyph flashes on the display.` |
| Consumer.cs | 91 | Message | `{{R\|=subject.T= =verb:consume= =object.an==object.directionIfAny=!}}` |
| DesalinationPellet.cs | 213 | Message/DestroyMessage/ConvertMessage | `=subject.T= =verb:fizzle= for several seconds.` (+ 2 variants) |
| Explores.cs | 72 | ExploreMessage | `=subject.T= =verb:float= off.` |
| LootOnStep.cs | 39 | SuccessMessage | `=subject.T==subject.directionIfAny= =verb:flex= and =verb:splinter= apart, revealing =object.an=.` |
| LootOnStep.cs | 46 | FailMessage | `=subject.T==subject.directionIfAny= =verb:flex= and =verb:splinter= apart.` |
| Pettable.cs | 176 | PetResponse (tag) | `=subject.T= =verb:stare= at =object.t= blankly.` (fallback) |
| Preacher.cs | 163 | Prefix/Postfix | Prefix=`=subject.T= =verb:yell= {{W\|'`, Postfix=`'}}` |

#### With null C# defaults (XML blueprint-dependent, 15 sites)

| File | Line | Field |
|------|------|-------|
| BlowAwayGas.cs | 73 | Message |
| CancelRangedAttacks.cs | 100 | Message |
| Interactable.cs | 119 | Message |
| LifeSaver.cs | 186 | LethalMessage / threshold messages |
| LifeSaver.cs | 193 | DestroyWhenUsedUpMessage |
| NephalProperties.cs | 161 | PhaseMessage |
| RandomLongRangeTeleportOnDamage.cs | 126 | Message |
| Reconstitution.cs | 262 | DropMessage |
| SpawnVessel.cs | 66 | SpawnMessage |
| Spawner.cs | 89 | SpawnMessage |
| SplitOnDeath.cs | 66 | Message |
| SwapOnUse.cs | 34 | Message |
| TimeCubeProtection.cs | 21 | Message |
| BaseLiquid.cs | 415 | SlipperyMessage (per liquid XML) |
| BaseLiquid.cs | 440 | SlipperyMessage (per liquid XML) |

Also: Transmutation.cs L86 (caller-supplied template), GameObject.cs L14615 (CustomDeathMessage tag).

### UI Template Sites (13 SetText sites)

| File | Line | Template Pattern |
|------|------|-----------------|
| AbilityBar.cs | 486 | `"ACTIVE EFFECTS: " + effectList` |
| AbilityBar.cs | 501 | `"TARGET: " + name` or `"TARGET: [none]"` |
| AbilityBar.cs | 505 | WoundLevel + FeelingDescription + DifficultyDescription |
| AbilityBar.cs | 609 | ability name + `"[disabled]"` / `"[on]"` / `"[off]"` + hotkey |
| AskNumberScreen.cs | 124 | Caller-supplied message (generally static prompts) |
| CyberneticsTerminalScreen.cs | 256 | FooterText from terminal screen data |
| PlayerStatusBar.cs | 484 | FoodStatus + WaterStatus (finite label set: "Hungry"/"Satiated" etc.) |
| PlayerStatusBar.cs | 491 | Time format `"HH:MM Day of Month"` |
| PlayerStatusBar.cs | 497 | Temperature `"T:Xø"` |
| PlayerStatusBar.cs | 501 | Weight `"X/Y# Z$"` |
| SkillsAndPowersLine.cs | 230 | `ModernUIText()` — template with computed values |
| SkillsAndPowersStatusScreen.cs | 243 | `"Ex: " + prerequisite names` |

### Description Gap (1 site)

| File | Line | Issue |
|------|------|-------|
| QudCyberneticsModuleWindow.cs | 70 | `CyberneticsChoice.GetLongDescription()` — different from `Description.GetLongDescription`, not covered by existing patch. Reads `Description.Short` + `CyberneticsBaseItem.BehaviorDescription` from blueprint XML. |

---

## Detailed Classification: STATIC_RESOLVABLE (17 sites)

| File | Line | Content Type |
|------|------|-------------|
| Preacher.cs | 143 | Hardcoded: `"You hear inaudible mumbling."` |
| AchievementViewRow.cs | 66-67 | Achievement Name + Description (finite set) |
| BookLine.cs | 40 | Book page text (from XML book data) |
| BookScreen.cs | 233 | Book title (from XML book data) |
| WorldGenerationScreen.cs | 251, 260 | Quotes book content + attribution |
| WorldGenerationScreen.cs | 126 | World generation progress messages (finite set) |
| EndGame.cs | 350 | EndCredits book text (from XML) |
| ButtonBarButton.cs | 170 | Button bar labels (static UI definitions) |
| FilterBarCategoryButton.cs | 222 | Filter categories ("Light Sources", "Melee Weapons", etc.) |
| OptionsButtonControl.cs | 69 | Options menu titles |
| OptionsComboBoxControl.cs | 91 | Options combo box titles |
| OptionsSliderControl.cs | 221 | Options slider titles |
| KeyMenuOption.cs | 29, 36 | Menu option descriptions + key prefix |
| TinkeringBitsLine.cs | 96 | Tinkering bit display names (finite set) |

---

## Detailed Classification: EXCLUDED (47 sites)

### Numeric Displays (7 sites)
AskNumberScreen L125/L169, OptionsSliderControl L152/L183/L205, BookScreen L296/L297

### Upstream DisplayName/Description Passthrough (12 sites)
AbilityManagerScreen L471/L495, CharacterEffectLine L88, TinkeringDetailsLine L116/L126/L181/L182, TradeScreen L706/L708/L958, TinkeringStatusScreen L584, PlayerStatusBar L505

### Already-Covered by Existing Patches (6 sites)
Look.cs L259/L532 (DescriptionLongDescriptionPatch), TinkerData L129/L173 (DescriptionShortDescriptionPatch), MessageLogLine L88 (MessageLogPatch), JournalLine L207 (JournalEntryDisplayTextPatch)

### Infrastructure / Non-Translatable (22 sites)
ReplaceBuilder L281/L287, ModCellView L14 (mod ID), PickGameObjectLine L162 (whitespace), FrameworkSearchInput L80 (user input), HorizontalScroller L78 (height calc), LoadingStatusWindow L45, CyberneticsTerminalRow L83/L101/L115 (upstream passthrough), HighScoresRow L79, CharacterAttributeLine L95/L123/L139, PlayerStatusBar L510/L514, TradeScreen L957, GameSummaryScreen L129, ModManagerUI L225, SkillsAndPowersLine L192, JournalStatusScreen L298, CharacterAttributeLine L111

---

## Detailed Classification: TRULY_RUNTIME (43 sites)

### Dynamic Text Composition (15 EmitMessage sites)

| File | Line | Reason |
|------|------|--------|
| Chat.cs | 182, 191, 206 | Per-NPC Says field from blueprint — arbitrary text data |
| Garbage.cs | 148, 168 | Dynamic StringBuilder: object names + direction + journal notes |
| LiquidVolume.cs | 3538 | Actor.Does("collect") + dram count + liquid name + container list |
| MissileWeapon.cs | 2472, 2490 | Message from event handler chain output |
| Physics.cs | 3780 | Damage message with %o/%S/%t placeholder replacements |
| Stomach.cs | 538 | StringBuilder from drinking logic |
| IrritableGenome.cs | 75 | Poss() + dynamic mutation point text |
| Ill.cs | 71 | Message field overridable by constructor caller |
| DesalinationPellet.cs | 217 | MakeUnderstood() runtime output |
| ShevaStarshipControl.cs | 199 | Pass-through from caller |
| GameObject.cs | 14597 | Die() Message parameter from caller |

### UI Data from Multiple Upstream Sources (28 SetText sites)

| Cluster | Sites | Reason |
|---------|-------|--------|
| UI Framework controls | CategoryIconScroller L16, CategoryMenuController L20, CategoryMenusScroller L53/L54, FrameworkHeader L20, FrameworkScroller L333, SummaryBlockControl L30 | FrameworkDataElement populated by many different screens |
| LeftSideCategory | L20, L24, L28, L32 | Category descriptions from keybind/menu/help/options systems |
| Notification | L118, L120 | ConcurrentQueue populated by various game systems |
| Tutorial | L261, L531, L737 | Tutorial text with hotkeyReplace substitution |
| ObjectFinder | L63, L72, L74 | Dynamic direction/distance/description data |
| GameSummary | L130, L131 | Death cause + details from EndGame state |
| BookScreen | L202 | MarkovBook title — procedurally generated |
| MapScroller | L137, L12, L13 | Journal accomplishment + quest pin data |
| WorldGenScreen | L111 | Progress bar text with dynamic insertion |
| JournalStatusScreen | L317 | Tab names dynamically renamed |
| PickTargetWindow | L30 | Text from multiple caller contexts |
| TitledIconButton | L23 | Title set by various callers |
| SteamScoresRow | L33 | Network leaderboard data |

---

## Architecture Proposals

### Proposal 1: Blueprint XML Template Translation Layer (Priority 1)

**Mechanism**: Hook into `GameObjectFactory` blueprint loading to translate part field values (e.g. `ActivateSuccessMessage`, `SpawnMessage`) in-place after XML parsing. Translated templates retain `=subject=`/`=verb:X=` slots so `VariableReplace` continues to work.

**Coverage**: 37 STATIC_PARTIAL EmitMessage sites + any downstream UI passthrough reading these fields.

**Effort**: Medium — Single hook point, but requires enumerating all translatable blueprint fields and building a translation dictionary keyed by `BlueprintID:PartName:FieldName`.

**Risk**:
- `=verb:X=` slots must survive translation (Japanese sentence structure may need slot reordering)
- Blueprint field overrides per-object add dictionary complexity
- Game version updates may change blueprint IDs

**Why first**: Highest ROI — one hook covers the single largest cluster of actionable sites.

### Proposal 2: CharGen Screen Patch Cluster (Priority 2)

**Mechanism**: Individual Harmony patches for `QudCyberneticsModuleWindow`, extending existing `CharGenLocalizationPatch` pattern.

**Coverage**: 1 STATIC_PARTIAL description site + potential expansion to CharGen framework controls.

**Effort**: Low — follows existing pattern in `CharGenLocalizationPatch`.

**Risk**: Minimal — isolated to character creation screens.

**Why second**: Quick win, fills gap in existing patch coverage with proven pattern.

### Proposal 3: Static Dictionary Entries for STATIC_RESOLVABLE (Priority 3)

**Mechanism**: Add XML dictionary entries for the 17 STATIC_RESOLVABLE sites — book content, achievement strings, options titles, button labels, filter categories, menu options.

**Coverage**: 17 STATIC_RESOLVABLE sites.

**Effort**: Low — straightforward dictionary work.

**Risk**:
- Book content (Quotes, EndCredits) is loaded via `BookUI.Books` — need to verify translation hook point
- Filter category names must match game's internal category strings exactly

**Why third**: Easy, predictable work that can be parallelized.

### Proposal 4: UI Template Patch Cluster (Priority 4)

**Mechanism**: Individual Harmony patches for `AbilityBar`, `PlayerStatusBar`, and `SkillsAndPowersStatusScreen` to translate static template fragments ("ACTIVE EFFECTS:", "TARGET:", "T:", food/water labels, time format).

**Coverage**: 13 STATIC_PARTIAL SetText sites.

**Effort**: Medium — each screen needs a dedicated patch to intercept the text assembly.

**Risk**:
- PlayerStatusBar patches touch frequently-updated UI — performance sensitive
- Food/water labels come from `Stomach.FoodStatus()` / `WaterStatus()` — may need upstream hook instead

### Proposal 5: GameText.VariableReplace Post-process Hook (Priority 5, Fallback)

**Mechanism**: Harmony Postfix on `GameText.VariableReplace` to translate expanded template output.

**Coverage**: Catch-all for STATIC_PARTIAL sites not covered by Proposal 1 (blueprint-level translation).

**Effort**: Low — single Postfix.

**Risk**:
- High call frequency — performance impact
- Post-expansion text contains runtime object names mixed with template text, making pattern matching complex
- May conflict with existing `DescriptionShortDescriptionPatch` and `DescriptionLongDescriptionPatch`

**Why last**: Only needed if Proposal 1 leaves gaps. Use as safety net, not primary strategy.

---

## Recommended Phase 3 Execution Plan

### Phase 3a: Blueprint Template Translation (32 of 37 sites)
- Build `BlueprintID:PartName:FieldName` → Japanese template dictionary
- Enumerate all blueprint XML overrides for the 15 null-default fields
- Hook `GameObjectFactory` post-load to apply translations
- **Covered**: 32 sites via part parameter translation (28 direct + 4 RemotePowerSwitch auto-covered)
- **Remaining**: 5 sites need separate handling (Pettable tag, BaseLiquid liquid field, Transmutation caller param, GameObject CustomDeathMessage tag)
- **Excluded from scope**: 5 null-default fields confirmed unused in game v2.0.4 (Reconstitution.DropMessage, Spawner.SpawnMessage, SwapOnUse.Message, LifeSaver.CurrentHitpointsThresholdMessage/DefaultActivationMessage/DestroyWhenUsedUpMessage)

### Phase 3b: Static Dictionary Entries (17 sites)
- Book content (Quotes, EndCredits, page text)
- Achievement names and descriptions
- Options/settings UI titles
- Button labels and menu options
- Tinkering bit names and filter categories
- **Expected result**: 17 STATIC_RESOLVABLE sites resolved

### Phase 3c: UI Template Patches (14 sites)
- AbilityBar (4 sites): "ACTIVE EFFECTS:", "TARGET:", ability state labels
- PlayerStatusBar (4 sites): food/water, time, temperature, weight formats
- SkillsAndPowersStatusScreen (2 sites): requirement prefix, ModernUIText
- QudCyberneticsModuleWindow (1 site): CyberneticsChoice.GetLongDescription
- AskNumberScreen, CyberneticsTerminalScreen (2 sites)
- **Expected result**: 14 STATIC_PARTIAL sites resolved

### Phase 3d: Runtime Investigation (43 sites, deferred)
- Chat/sermon system (3 sites): Requires L3 Rosetta to enumerate Says values per blueprint
- UI Framework infrastructure (7 sites): Requires runtime tracing of FrameworkDataElement sources
- Remaining TRULY_RUNTIME sites: Evaluate cost/benefit per site
- **Expected result**: Triage into "worth patching" vs "accept English" buckets

---

## Impact Summary

| Phase | Sites Resolved | Cumulative Coverage |
|-------|---------------|-------------------|
| Existing patches | 4,506 / 4,664 (96.6%) | 96.6% |
| Phase 3a (Blueprint templates) | +32 | 97.3% |
| Phase 3b (Static dictionary) | +17 | 97.8% |
| Phase 3c (UI template patches) | +14 | 98.1% |
| Phase 3d (Runtime, if pursued) | up to +43 | 99.0% |
| EXCLUDED (no action) | 47 already correct | — |

**With Phases 3a-3c alone, translation coverage reaches 98.1% — up from 96.6% — without any runtime investigation.**
