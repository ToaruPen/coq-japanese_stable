# Runtime-Dependent Translation Sites Strategy (2026-03-23)

## Scope and target set

This document scopes the work to the **179 sites** in `docs/candidate-inventory.json` where:

```json
static_classification == "runtime_dependent"
```

That subset is narrower than the broader `needs_runtime == true` population:

- `needs_runtime == true`: **355** sites
- `static_classification == "runtime_dependent"`: **179** sites

The broader 355-site bucket mixes translated, excluded, and other runtime-observed cases. Issue #84 and this strategy are specifically about the 179 sites already classified as runtime-dependent after source reading.

## Headline findings

1. **Most of the 179 sites already surface through existing sinks**, especially `UITextSkin.SetText`, `MessageQueue.AddPlayerMessage`, and popup routes.

2. **Sink reachability is not the same as "solved".** Existing sink patches are good for observability and low-risk template work, but they lose semantic structure. High-density clusters still need producer-aware or screen-aware handling.

3. The inventory's `existing_patch` field is **empty for all 179 sites**. In this document, "already intercepted" means **runtime reachability through existing sinks or producers**, not that the inventory already credits a finished patch.

4. The problem breaks into **11 implementation clusters**:
   - 4 UI `SetText` sub-clusters (98 sites total)
   - 7 non-`SetText` clusters across `EmitMessage`, `Does()`, and description producers (81 sites total)

5. The best overall strategy is **hybrid**:
   - Use **producer-level Harmony patches** for dense, semantically structured families.
   - Use **existing sink patches** as audit/fallback layers and for low-risk data/template work.
   - Use **runtime capture + targeted follow-up** for payload-driven, persisted, or procedural text.

## Existing infrastructure and what it already buys us

The current mod already has the key interception points needed for this work:

| Infrastructure | Target | Current role | Why it matters here |
|---|---|---|---|
| `UITextSkinTranslationPatch` | `XRL.UI.UITextSkin.SetText(string)` | Catch-all UI sink | Most `SetText` sites already pass here |
| `MessageLogPatch` | `XRL.Messages.MessageQueue.AddPlayerMessage(...)` | Message-log sink | Most `EmitMessage`/`Does()` routes eventually land here |
| `PopupTranslationPatch` | `XRL.UI.Popup.ShowBlock/ShowOptionList/ShowConversation` | Popup sink | Covers player-facing dialog/popup text that bypasses message log |
| `PopupMessageTranslationPatch` | `Qud.UI.PopupMessage.ShowPopup` | Popup message relay | Covers another popup path outside `XRL.UI.Popup` |
| `XDidYTranslationPatch` | `Messaging.XDidY/XDidYToZ/WDidXToYWithZ` | Structured message-frame rewrite | Best existing semantic seam for message-family text |
| `StartReplaceTranslationPatch` | `GameTextExtensions.StartReplace(string)` | Variable-template interception | Useful for `StartReplace` / `ReplaceBuilder` families, but **not** a blanket hook for every `GameText.VariableReplace(...)` call |
| `DescriptionShortDescriptionPatch` | `Description.GetShortDescription(...)` | Description producer hook | Central seam for short descriptions |
| `DescriptionLongDescriptionPatch` | `Description.GetLongDescription(StringBuilder)` | Description producer hook | Central seam for long descriptions |
| `LookTooltipContentPatch` | Look tooltip assembly | Tooltip producer hook | Already centralizes `Look.cs` description output |
| `MessagePatternTranslator` | Regex/template layer | Sink-side fallback | Useful for bounded families, risky as a dumping ground |
| `DynamicTextObservability` | Runtime logging/probes | Discovery and audit | Best way to tighten ambiguous clusters safely |

Relevant architecture guidance already in-repo:

- `docs/source-first-design.md:356-360` explicitly treats `UITextSkinTranslationPatch`, `MessagePatternTranslator`, `PopupTranslationPatch`, and `MessageLogPatch` as **Tier 2 rewrite candidates** once scanner knowledge is available.
- `docs/procedural-text-status.md:3-29` confirms `HistoricStringExpander` remains intentionally disabled because it can corrupt symbolic world-generation keys.
- `Mods/QudJP/Assemblies/src/QudJPMod.cs:101-151` shows failed Harmony targets are logged and skipped rather than crashing the game. Good for game stability, bad for silent coverage drift.
- `Mods/QudJP/Assemblies/src/Patches/GetDisplayNameProcessPatch.cs:100-123` shows some current producer-side work depends on private field names, so version drift is a real maintenance risk.

## Interception decision rules

These rules should drive cluster-level choices:

1. **Prefer Postfix on producers** when the method still has semantic structure (`GetDescription`, screen-specific view update methods, centralized builders).

2. **Use Prefix on sinks** when the goal is to rewrite text before rendering/logging (`SetText`, `AddPlayerMessage`, popup methods), but do not assume sink-only translation is the best long-term fix.

3. **Reserve transpilers** for embedded literals only. They are the highest-risk option and should not be the primary answer for runtime-dependent families.

4. **Treat event interception as targeted, not generic.** Canonical seams like `GetDisplayNameEvent` or the `XDidY` family are worth intercepting. Broad "patch HandleEvent everywhere" style approaches are not.

5. **Use runtime capture first** when text is payload-driven, persisted, procedural, or externally sourced and the upstream producer is ambiguous.

## Cluster inventory

The 179 sites split cleanly into the following implementation clusters.

### Summary table

| Cluster | Sites | Representative producers/files | Existing interception | Recommended primary strategy | Effort | Risk |
|---|---:|---|---|---|---|---|
| A1. UI HUD / live gameplay UI | 25 | `Qud.UI/PlayerStatusBar.cs`, `Qud.UI/AbilityBar.cs`, `Qud.UI/CharacterAttributeLine.cs`, `Qud.UI/SkillsAndPowersLine.cs` | Mostly `UITextSkinTranslationPatch` | **Producer-level screen patches** plus scoped dictionaries; keep `UITextSkin` as audit/fallback | M | M |
| A2. UI detail / inspection screens | 26 | `TradeScreen`, `TinkeringDetailsLine`, `BookScreen`, `JournalStatusScreen`, `CyberneticsTerminalRow` | `UITextSkinTranslationPatch`, some description hooks | **Producer-level or screen-level Harmony patches** for dense screens; reuse description infrastructure | M | M |
| A3. UI framework / menu rows | 24 | `LeftSideCategory`, `OptionsSliderControl`, `CategoryMenusScroller`, `KeyMenuOption` | `UITextSkinTranslationPatch` | **Runtime capture + static/scoped dictionary**, with minimal new Harmony | S-M | L-M |
| A4. UI payload / persisted / external content | 23 | `WorldGenerationScreen`, `TutorialManager`, `Notification`, `HighScoresRow`, `MapScroller*`, mod rows | Mixed; many via `UITextSkin`, some need verification | **Runtime capture first**, then route-specific dictionary or tiny screen patches | M-L | H |
| B1. Device access / activation messages | 13 | `PowerSwitch`, `RemotePowerSwitch`, `ForceProjector` | `MessageLogPatch`, `PopupTranslationPatch`, partial `XDidY` | **Producer-level template handling** around `VariableReplace` fields; sink fallback for final strings | S-M | L-M |
| B2. Blueprint message fields / ReplaceBuilder families | 25 | `LifeSaver`, `LootOnStep`, `Consumer`, `Interactable`, `Pettable`, `Reconstitution`, `SpawnVessel`, `SplitOnDeath`, `TimeCubeProtection`, etc. | `StartReplaceTranslationPatch` + message sinks | **Template dictionary work first**, then producer patches only where Japanese word order demands it | M | M |
| B3. Missile combat outcome messages | 15 | `XRL.World.Parts/MissileWeapon.cs` | Message sinks; some neighboring `XDidY` infra but not enough by itself | **Dedicated producer patch or dedicated family translator**; sink regex only as audit | L | H |
| B4. Chat speech / emote family | 5 | `XRL.World.Parts/Chat.cs` | Message sinks | **Translate the data source first** (`Says`/chat content), keep sink patterns as fallback | M | M |
| B5. Liquid / consumable composition | 9 | `BaseLiquid`, `LiquidVolume`, `TattooGun`, `DesalinationPellet`, `Stomach` | Message sinks, some template infra | **Producer-level patches** around liquid-name insertion and collection/drinking builders | M-L | H |
| B6. Procedural / event-scattered misc | 9 | `Garbage`, `WaterRitualBuySecret`, `PetGloaming`, `Ill`, `IrritableGenome`, `Physics`, `ShevaStarshipControl` | Usually message sinks only | **Runtime capture + targeted follow-up**; do not overgeneralize | L | H |
| B7. Description family | 5 | `Look.cs`, `TinkerData.cs`, `QudCyberneticsModuleWindow.cs` | `DescriptionShortDescriptionPatch`, `DescriptionLongDescriptionPatch`, `LookTooltipContentPatch` | **Extend existing description producers**, not new sinks | S | L |

### Cluster details

#### A1. UI HUD / live gameplay UI (25)

Representative sites:

- `Qud.UI/PlayerStatusBar.cs` lines 482-514: food/water, time, temperature, weight, zone, HP bar text, player name.
- `Qud.UI/AbilityBar.cs` lines 475-609: effect text, target text, target health, per-ability display text with hotkeys/cooldowns.
- `Qud.UI/CharacterAttributeLine.cs`: stat labels and colored numeric values.

Why this is a cluster:

- These strings are rebuilt from **live game state** on update or view-refresh paths.
- The text is high-visibility and changes continuously during normal play.
- The generic `UITextSkin` sink sees the final string, but the producers still know which fields are stats, labels, hotkeys, names, and values.

Recommended strategy:

- Use **screen-aware Harmony patches** for the densest widgets (`PlayerStatusBar`, `AbilityBar`) so translation logic can act on structured inputs rather than post-assembled strings.
- Keep `UITextSkinTranslationPatch` as a safety net and observability point.
- Reuse scoped dictionaries and existing helpers like `StatusLineTranslationHelpers` where applicable.

Best interception type:

- Primary: **producer-level Harmony patch**
- Secondary: **UI sink fallback**

#### A2. UI detail / inspection screens (26)

Representative sites:

- `Qud.UI/TradeScreen.cs` lines 706-708 and 957-958: trader names, item names, details pane.
- `Qud.UI/TinkeringDetailsLine.cs`: display name, unclipped description, `GetShortDescription`, modification description.
- `Qud.UI/BookScreen.cs` lines 202/233/296/297: book titles and page counters.
- `Qud.UI/CyberneticsTerminalRow.cs`: terminal text and cursor animation.

Why this is a cluster:

- These screens already have **cohesive update/render methods** that gather all needed data for one panel.
- A single per-screen patch can often cover multiple inventory sites with less risk than enriching the generic `SetText` sink.

Recommended strategy:

- Patch the **screen/controller update method** or the row binding method, not `UITextSkin` itself.
- For `TinkeringDetailsLine`, explicitly route through existing description infrastructure where possible.
- Treat `BookScreen` and `TradeScreen` as screen-local producers with scoped dictionary/template work.

Best interception type:

- Primary: **producer-level Harmony patch**
- Secondary: **UI framework hook** only for audit/fallback

#### A3. UI framework / menu rows (24)

Representative sites:

- `Qud.UI/LeftSideCategory.cs`: category/menu/help labels.
- `Qud.UI/OptionsSliderControl.cs`: option title plus numeric slider values.
- `XRL.UI.Framework/CategoryMenusScroller.cs` and `KeyMenuOption.cs`: menu descriptions and prefixes.

Why this is a cluster:

- Most text comes from **data objects already intended for display**.
- Many entries are stable labels or low-variance templates rather than deep gameplay composition.

Recommended strategy:

- Use the existing `UITextSkin` sink and add **scoped dictionary entries** for the relevant data families.
- Avoid creating new Harmony hooks unless a specific row class proves too dynamic or bypasses `UITextSkin`.

Best interception type:

- Primary: **runtime capture + static/scoped dictionary**
- Secondary: **small UI framework hook** only if a row class bypasses the standard sink

#### A4. UI payload / persisted / external content (23)

Representative sites:

- `Qud.UI/WorldGenerationScreen.cs`: progress text, quotes, attributions.
- `TutorialManager.cs`: runtime-highlighted tutorial strings with hotkey replacement.
- `Qud.UI/Notification.cs`: title/text from runtime payloads.
- `HighScoresRow.cs`, `SteamScoresRow.cs`, `MapScroller*`, `ModCellView`, `ModManagerUI`.

Why this is a cluster:

- These strings often come from **saved data, callback payloads, external content, or mod metadata**.
- Sink interception can show the string, but often cannot tell whether the right fix is a dictionary entry, a formatter patch, or a deliberate exclusion.

Recommended strategy:

- Start with **runtime capture + provenance tracing**.
- Promote to route-specific dictionary entries only after verifying that the text family is genuinely stable.
- Use tiny screen-specific patches only where repeated formatting logic is clearly local and stable.

Best interception type:

- Primary: **runtime capture + static dictionary**
- Secondary: **route-specific UI patch**

#### B1. Device access / activation messages (13)

Representative sites:

- `XRL.World.Parts/PowerSwitch.cs` lines 536/544/571/784/789/816/821
- `XRL.World.Parts/RemotePowerSwitch.cs` lines 68/73/94/99
- `XRL.World.Parts/ForceProjector.cs` access messages

Why this is a cluster:

- The messages are driven by **blueprint-configured fields** such as `AccessFailureMessage`, `ActivateSuccessMessage`, and `PsychometryAccessMessage`.
- The surrounding logic is stable and screen-independent.
- Many of these sites call `GameText.VariableReplace(...)` directly, which is adjacent to but **not the same interception seam** as `StartReplace()`.

Recommended strategy:

- Treat these as **template/data problems first**, not generic sink problems.
- Prefer one of two routes:
  - add a **narrow producer-side hook** for the relevant `GameText.VariableReplace(...)` family, or
  - cover the fully expanded output at the existing message/popup sinks when the family is small and stable.
- Keep the existing `XDidY` handling for activate/deactivate verb frames, but do not rely on it for the access/success/failure text itself.

Best interception type:

- Primary: **producer-level Harmony patch** or template-layer handling near `VariableReplace`
- Secondary: **sink-level pattern matching**

#### B2. Blueprint message fields / ReplaceBuilder families (25)

Representative sites:

- `LifeSaver`, `LootOnStep`, `CancelRangedAttacks`, `Consumer`, `Explores`, `Interactable`, `Pettable`, `RandomLongRangeTeleportOnDamage`, `Reconstitution`, `SpawnVessel`, `Spawner`, `SplitOnDeath`, `SwapOnUse`, `TimeCubeProtection`
- `BlowAwayGas` and other `StartReplace()`/`ReplaceBuilder`-driven cases

Why this is a cluster:

- The text is predominantly **data-driven** and lives in part fields or variable templates.
- Some sites genuinely use `StartReplace()` / `ReplaceBuilder`, but others only become visible after direct `GameText.VariableReplace(...)` expansion.

Recommended strategy:

- Use **template dictionary work first** for true `StartReplace` / `ReplaceBuilder` routes.
- For direct `GameText.VariableReplace(...)` routes, prefer either a narrow producer hook or bounded sink-side pattern coverage.
- Only add new producer patches where Japanese word order or slot placement cannot be expressed safely with the existing template layer.
- Keep the message sinks as safety nets, not the primary translation mechanism.

Best interception type:

- Primary: **runtime capture + static/template dictionary**
- Secondary: **producer-level Harmony patch**

#### B3. Missile combat outcome messages (15)

Representative sites:

- `XRL.World.Parts/MissileWeapon.cs` lines 2061-2271 (`Does("hit")` branches) plus load-ammo failure messages at lines 2472 and 2490.

Why this is a cluster:

- This is the densest non-UI cluster.
- The text varies by visibility, source, target, direction, hit multiplier, and `OutcomeMessageFragment`.
- Sink-level regexes can observe the final English, but the semantic structure still exists in the producer.

Recommended strategy:

- Handle this as a **dedicated family**, not a long tail of regexes.
- Prefer either:
  - a **producer-level Harmony patch** in `MissileWeapon`, or
  - a dedicated translator/family helper wired near the producer.
- Use sink patterns only for audit or as a short-term bridge.

Best interception type:

- Primary: **producer-level Harmony patch**
- Secondary: **sink-level pattern matching**

#### B4. Chat speech / emote family (5)

Representative sites:

- `XRL.World.Parts/Chat.cs` lines 182/191/195/206/210

Why this is a cluster:

- The text comes from the `Says`/chat content field and is split into multiple modes:
  - direct message (`*`)
  - bracketed emote-like text (`[...]`)
  - `"X says, '...'"` composition via `Does("say")`

Recommended strategy:

- Translate the **data source** first where possible (chat content/blueprint side).
- Keep message-sink patterns as fallback for residual formatting branches.

Best interception type:

- Primary: **runtime capture + static dictionary**
- Secondary: **producer-level Harmony patch**

#### B5. Liquid / consumable composition (9)

Representative sites:

- `XRL.Liquids/BaseLiquid.cs`: slippery/sticky messages from liquid definitions.
- `XRL.World.Parts/LiquidVolume.cs` lines 3474-3538: collection messages with amount, liquid name, direction, and container list.
- `TattooGun`, `DesalinationPellet`, `Stomach`

Why this is a cluster:

- These messages combine **liquid identity**, **quantity grammar**, and **container/body context**.
- The final strings are compositional enough that sink-only handling becomes brittle quickly.

Recommended strategy:

- Use **producer-level patches** for the high-composition builders (`LiquidVolume`, `Stomach`, `TattooGun`).
- Keep data-driven liquid field messages (`BaseLiquid`) in the template/dictionary lane.

Best interception type:

- Primary: **producer-level Harmony patch**
- Secondary: **runtime capture + template dictionary**

#### B6. Procedural / event-scattered misc (9)

Representative sites:

- `Garbage.cs` (random note text plus spatial wording)
- `WaterRitualBuySecret.cs` (historic event gospel)
- `PetGloaming.cs`
- `IrritableGenome.cs`, `Ill.cs`, `Physics.cs`, `ShevaStarshipControl.cs`

Why this is a cluster:

- These are sparse, heterogeneous, and often fed by event payloads or procedural text fragments.
- The main danger is pretending they form one family when they do not.

Recommended strategy:

- Use **runtime capture + targeted follow-up**.
- Do not merge these into a generic catch-all implementation.
- Explicitly keep `HistoricStringExpander` and related unsafe procedural coverage out of the main denominator until the safeguards in `docs/procedural-text-status.md` are met.

Best interception type:

- Primary: **runtime capture + static dictionary**
- Secondary: **event interception** only when a canonical event-like seam is proven

#### B7. Description family (5)

Representative sites:

- `XRL.UI/Look.cs`
- `XRL.World.Tinkering/TinkerData.cs`
- `XRL.CharacterBuilds.Qud.UI/QudCyberneticsModuleWindow.cs`

Why this is a cluster:

- These are already close to centralized description producers.
- The codebase already has `DescriptionShortDescriptionPatch`, `DescriptionLongDescriptionPatch`, and `LookTooltipContentPatch`.

Recommended strategy:

- Extend the **existing description producers and scoped dictionaries**.
- Do not introduce new generic sinks for this family.

Best interception type:

- Primary: **producer-level Harmony patch**

## Which clusters already flow through existing sinks?

This matters for effort, but it should not be mistaken for "coverage is complete."

### Clearly sink-reachable today

- Most `SetText` cases through `UITextSkinTranslationPatch`
- Most `EmitMessage` cases through `MessageLogPatch`
- Popup/dialog variants through `PopupTranslationPatch` / `PopupMessageTranslationPatch`
- Some structured message-frame routes through `XDidYTranslationPatch`
- Description routes through `Description*Patch` and `LookTooltipContentPatch`

### Important caveats

1. **Shared sink != good seam.** `UITextSkin.SetText` is too generic to be the preferred home for all translation logic.

2. **Pattern ordering matters.** `MessagePatternTranslator` is first-match-wins, so every new rule increases shadowing risk.

3. **Some direct text fields may bypass the usual UI path.** These are low-count but must be verified before relying on `UITextSkin` coverage.

4. **Procedural/payload routes remain special.** Observability can see them, but that does not mean a stable static fix exists.

## Recommended phased implementation plan

## Phase 1: Quick wins on top of existing infrastructure

Goal: convert the low-risk, high-confidence families without adding broad new Harmony surfaces.

Target clusters:

- A3. UI framework / menu rows (24)
- B1. Device access / activation messages (13)
- B2. Blueprint message fields / ReplaceBuilder families (25)
- B7. Description family (5)
- Safe, stable slices of A1/A2 where the text is clearly a leaf or simple template

Primary tactics:

- Add scoped dictionary/template entries
- Reuse `StartReplaceTranslationPatch`
- Reuse `DescriptionShortDescriptionPatch`, `DescriptionLongDescriptionPatch`, and `LookTooltipContentPatch`
- Use runtime observability to confirm misses rather than guessing

Why Phase 1 first:

- Lowest side-effect risk
- Strongest leverage from existing infrastructure
- Creates reusable template data needed by later producer patches

Estimated effort:

- **1-2 implementation batches**
- Roughly **67-80 sites** depending on how aggressively safe A1/A2 rows are included

## Phase 2: High-density producer patches

Goal: solve the families where sink-only translation is visibly too lossy.

Target clusters:

- A1. UI HUD / live gameplay UI (25)
- A2. UI detail / inspection screens (26)
- B3. Missile combat outcome messages (15)
- B5. Liquid / consumable composition (9)

Primary tactics:

- Screen-aware Harmony patches for `PlayerStatusBar`, `AbilityBar`, `TradeScreen`, `TinkeringDetailsLine`, and similar high-density UI producers
- Dedicated producer/family patch for `MissileWeapon`
- Producer-side liquid/consumable helpers for `LiquidVolume`, `TattooGun`, `Stomach`, and related builders

Why this is Phase 2:

- These clusters are dense enough that a good producer patch pays off quickly
- They are also the clusters most likely to become regex debt if left to sinks

Estimated effort:

- **2-3 implementation batches**
- Roughly **49-75 sites** depending on how many A1/A2 cases survive Phase 1 as pure data work

## Phase 3: Scattered residuals and runtime-proven fixes

Goal: mop up the families that need proof before implementation.

Target clusters:

- A4. UI payload / persisted / external content (23)
- B4. Chat speech / emote family (5)
- B6. Procedural / event-scattered misc (9)
- Any leftovers from Phase 2 where producer hooks prove too fragile

Primary tactics:

- Runtime capture under Rosetta
- Targeted provenance tracing
- Small route-specific patches only after confirming repeatability
- Explicit exclusions for unsafe procedural families until safeguards are satisfied

Why this is Phase 3:

- Highest ambiguity
- Highest chance of false confidence from sink-only evidence
- Best handled after the dense/core families are removed from the queue

Estimated effort:

- **1-2 focused cleanup batches**
- Roughly **32-41 sites**, but with the highest variability per site

## Risk assessment

### 1. Overestimating sink coverage

Risk:

- "It already reaches `UITextSkin` / `AddPlayerMessage`, so we just need patterns."

Why this is dangerous:

- Shared sinks lose semantics.
- The final string may already be conjugated, color-wrapped, or mixed with dynamic payloads.

Mitigation:

- Treat sink coverage as **observability + fallback**, not proof of the optimal fix.
- Promote high-density families to producer patches.

### 2. Harmony signature and reflection drift

Risk:

- Game updates move signatures or private field names.

Evidence:

- `QudJPMod.cs` explicitly logs and skips patch failures.
- `GetDisplayNameProcessPatch` depends on private builder fields `PrimaryBase` and `LastAdded`.
- Popup handling already hardcodes a subset of popup surfaces.

Mitigation:

- Prefer stable producer methods over fragile IL surgery.
- Keep L2/L2G signature validation for new targets.
- Document the exact 2.0.4 assumptions per new producer patch.

### 3. Pattern-order regressions

Risk:

- `MessagePatternTranslator` uses ordered first-match wins.

Mitigation:

- Add bounded family patterns only where there is a clear family.
- Pair new families with regression tests.
- Do not turn `MessagePatternTranslator` into the primary long-term solution for all runtime text.

### 4. Procedural text safety

Risk:

- Accidentally translating symbolic or machine-consumed procedural text.

Evidence:

- `HistoricStringExpanderPatch` is intentionally disabled because it broke world-generation/playability behavior.

Mitigation:

- Keep procedural families out of the main denominator unless the safeguards in `docs/procedural-text-status.md` are met.
- Require Rosetta runtime validation for any reintroduction.

### 5. Denominator drift

Risk:

- Reviewers may conflate the 179-site scoped subset with the broader 355-site `needs_runtime` population.

Mitigation:

- Keep the selection rule explicit in every planning artifact.
- When reporting progress, always say whether the denominator is **179** or **355**.

## Dependencies on existing infrastructure

This plan depends on the following infrastructure staying healthy:

- `UITextSkinTranslationPatch`
- `MessageLogPatch`
- `PopupTranslationPatch`
- `PopupMessageTranslationPatch`
- `XDidYTranslationPatch`
- `StartReplaceTranslationPatch`
- `DescriptionShortDescriptionPatch`
- `DescriptionLongDescriptionPatch`
- `LookTooltipContentPatch`
- `DynamicTextObservability`
- `ColorAwareTranslationComposer`

In practice, that means:

1. Keep existing sink patches working as observability/fallback layers.
2. Add new producer patches incrementally rather than replacing the sink safety net in one step.
3. Validate new producer patches under the current game version (`2.0.4`) and Rosetta runtime flow before calling a cluster "done."

## Validation plan

For each phase:

1. Capture runtime evidence with the existing observability stack.
2. Verify the target producer method actually resolves and patches cleanly.
3. Smoke-test the relevant in-game surfaces:
   - HUD and status screens
   - trade/tinkering/book/cybernetics screens
   - power switches and similar device interactions
   - missile combat
   - liquid collection / use
   - NPC chat / dialog
4. Check logs for:
   - `[QudJP] Build marker`
   - patch application warnings
   - `missing key`
   - `MessagePatternTranslator: no pattern`
   - other cluster-specific probes

## Final recommendation

Use the current sinks to **observe, classify, and mop up**, but do not make them the primary implementation surface for the hardest families.

The best sequence is:

1. **Phase 1:** mine the existing infrastructure for safe data/template wins.
2. **Phase 2:** invest in producer/screen patches for HUD, detail screens, missile combat, and liquid builders.
3. **Phase 3:** finish payload-driven, persisted, and procedural/scattered residuals with runtime proof first.

If executed this way, the work stays aligned with the repo's source-first direction: producers first where semantics matter, sinks second where they are genuinely enough.
