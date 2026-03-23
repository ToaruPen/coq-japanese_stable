# No-Fallback Architecture Investigation

Date: 2026-03-23

Scope: `Mods/QudJP/Assemblies/src/` and `Mods/QudJP/Assemblies/src/Patches/`

Goal: identify the architectural changes required to eliminate **silent** sink-time translation fallback while preserving coverage and improving root-cause visibility.

## Executive summary

The current architecture does **not** have a generic claim registry. The active claim mechanism is the `DirectTranslationMarker` (`'\x01'`) in `MessageFrameTranslator`, and today only `XDidYTranslationPatch` writes that claim marker before redispatching a translated message upstream of the sinks (`Mods/QudJP/Assemblies/src/MessageFrameTranslator.cs:23,42-74`, `Mods/QudJP/Assemblies/src/Patches/XDidYTranslationPatch.cs:940-976`).

That means the current system is effectively:

1. Some producers translate and claim ownership.
2. Broad sinks translate any unclaimed text they happen to see.
3. Unhandled text often passes through untranslated, with only missing-key / no-pattern observability.

The biggest problem is not only that sinks translate; it is that they translate **silently and generically**, which hides ownership gaps. `PopupTranslationPatch` falls through to `MessagePatternTranslator` and then to `UITextSkinTranslationPatch`; `UITextSkinTranslationPatch` itself ends in a bare `Translator.Translate(stripped)` catch-all; `MessageLogPatch` routes every unclaimed player message through `MessagePatternTranslator` (`Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs:243-279`, `Mods/QudJP/Assemblies/src/Patches/UITextSkinTranslationPatch.cs:128-156`, `Mods/QudJP/Assemblies/src/Patches/MessageLogPatch.cs:25-37`).

The practical target should therefore be:

- **No silent generic sink fallback.**
- **Producer ownership for dynamic families.**
- **Observation-only sinks for unclaimed text.**
- **Explicit route-scoped leaf handling where the source is already a genuine leaf.**

In other words: remove generic sink mutation, not necessarily every last route-local leaf lookup.

## Current translation architecture

### Core mechanisms

| Mechanism | Role | Evidence |
| --- | --- | --- |
| `Translator` | Global exact-key dictionary lookup with missing-key logs. | `Mods/QudJP/Assemblies/src/Translator.cs:155-171` |
| `MessagePatternTranslator` | Ordered regex/template translator for `messages.ja.json`; logs `no pattern` on misses. | `Mods/QudJP/Assemblies/src/MessagePatternTranslator.cs:67-142,163-228` |
| `MessageFrameTranslator` | Structured verb-frame translator for `XDidY` families; owns the `'\x01'` marker. | `Mods/QudJP/Assemblies/src/MessageFrameTranslator.cs:23,77-245` |
| `DynamicTextObservability` | Route/family transform logging with power-of-2 throttling. | `Mods/QudJP/Assemblies/src/DynamicTextObservability.cs:28-60` |
| `ObservabilityHelpers` | Context composition/extraction and power-of-2 log throttling. | `Mods/QudJP/Assemblies/src/ObservabilityHelpers.cs:13-47` |

### Current sink paths

| Sink | Target | Current behavior | Why it is a fallback sink |
| --- | --- | --- | --- |
| `MessageLogPatch` | `MessageQueue.AddPlayerMessage(...)` | Strip `'\x01'`; otherwise run `MessagePatternTranslator.Translate(Message, nameof(MessageLogPatch))`. | It catches every unclaimed player message regardless of producer. |
| `PopupTranslationPatch` | `Popup.ShowBlock/ShowOptionList/ShowConversation` | Strip marker; translate menu items; try popup templates; exact lookup; `MessagePatternTranslator`; then `UITextSkinTranslationPatch`. | It is a multi-layer fallback chain, not a single-family owner. |
| `PopupMessageTranslationPatch` | `PopupMessage.ShowPopup` | Delegates message/title/buttons/items/context-title through `PopupTranslationPatch.TranslatePopupTextForRoute(...)`. | It inherits the same fallback chain. |
| `UITextSkinTranslationPatch` | `UITextSkin.SetText(string)` | Strip marker; infer route by stack; try route helpers and sink templates; trimmed lookup; final `Translator.Translate(stripped)`. | It is the broadest late-stage UI sink in the mod. |

Evidence: `Mods/QudJP/Assemblies/src/Patches/MessageLogPatch.cs:25-37`, `Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs:243-279`, `Mods/QudJP/Assemblies/src/Patches/PopupMessageTranslationPatch.cs:40-109`, `Mods/QudJP/Assemblies/src/Patches/UITextSkinTranslationPatch.cs:91-156,401-488,679-805`.

### Current producer ownership map

There are three different kinds of "producer" today:

1. **Real upstream owners** that translate before the broad sinks and, ideally, claim ownership.
2. **Route-specific postfix owners** that still mutate late, but on a narrow route.
3. **Producer-looking relays** that mostly just forward text into a sink helper.

#### Real or mostly-real producer owners

| Route family | Current owner | Notes |
| --- | --- | --- |
| Structured message frames | `XDidYTranslationPatch` + `MessageFrameTranslator` | Only route that currently claims output with `'\x01'`. Falls back to original English when translation fails. |
| Grammar helpers | `GrammarPatch` | Fully replaces English grammar helpers with Japanese logic. |
| Display names | `GetDisplayNameRouteTranslator` via `GetDisplayNamePatch`, `GetDisplayNameProcessPatch`, `InventoryLocalizationPatch` | Large structural translator, but still postfix-side rather than builder-side. |
| Factions lines/data | `FactionsStatusScreenTranslationPatch`, `FactionsLineTranslationPatch`, `FactionsLineDataTranslationPatch` | Route-aware translation exists, but `UITextSkinTranslationPatch` still calls faction helpers directly for sink-time recovery. |
| Journal entries | `JournalEntryDisplayTextPatch`, `JournalMapNoteDisplayTextPatch`, `JournalTextTranslator` | This is the one intentional producer-side user of `MessagePatternTranslator`. |
| Effect descriptions/details | `EffectDescriptionPatch`, `EffectDetailsPatch`, `ActiveEffectTextTranslator` | Mostly exact/scoped dictionary handling. |
| Ability bar header | `AbilityBarUpdateAbilitiesTextPatch` | Producer-side header/pagination translation. |
| Active effects popup title/header | `GameObjectShowActiveEffectsPatch` | Inline producer-side translation for a bounded family. |

Evidence: `Mods/QudJP/Assemblies/src/Patches/XDidYTranslationPatch.cs:220-314,317-432,435-596,940-976`, `Mods/QudJP/Assemblies/src/Patches/GrammarPatch.cs:123-402`, `Mods/QudJP/Assemblies/src/Patches/GetDisplayNamePatch.cs:63-72`, `Mods/QudJP/Assemblies/src/Patches/GetDisplayNameProcessPatch.cs:63-90,126-188`, `Mods/QudJP/Assemblies/src/Patches/InventoryLocalizationPatch.cs:47-62`, `Mods/QudJP/Assemblies/src/Patches/FactionsLineTranslationPatch.cs:42-118`, `Mods/QudJP/Assemblies/src/Patches/JournalEntryDisplayTextPatch.cs:30-49`, `Mods/QudJP/Assemblies/src/Patches/JournalMapNoteDisplayTextPatch.cs:30-49`, `Mods/QudJP/Assemblies/src/Patches/JournalTextTranslator.cs:52-127`, `Mods/QudJP/Assemblies/src/Patches/EffectDescriptionPatch.cs:30-42`, `Mods/QudJP/Assemblies/src/Patches/EffectDetailsPatch.cs:30-42`, `Mods/QudJP/Assemblies/src/Patches/AbilityBarUpdateAbilitiesTextPatch.cs:35-124`, `Mods/QudJP/Assemblies/src/Patches/GameObjectShowActiveEffectsPatch.cs:43-90`.

#### Producer-looking relays that still depend on sink logic

| Route family | Relay patch | Why it is still sink-dependent |
| --- | --- | --- |
| Conversation text | `ConversationDisplayTextPatch` | Calls `UITextSkinTranslationPatch.TranslatePreservingColors(...)` directly. |
| Main menu | `MainMenuLocalizationPatch` | Uses `TranslateStringFieldsInCollection(...)`, which routes fields through `UITextSkinTranslationPatch`. |
| Options | `OptionsLocalizationPatch` | Same pattern as main menu. |
| Long descriptions | `DescriptionLongDescriptionPatch` | After compare-status special case, falls back to `UITextSkinTranslationPatch`. |
| Tooltips | `LookTooltipContentPatch` | After compare-status special case, falls back to `UITextSkinTranslationPatch`. |
| Popup bottom-context menu | `QudMenuBottomContextTranslationPatch` | Tries popup menu translation first, then falls through to `UITextSkinTranslationPatch`. |

Evidence: `Mods/QudJP/Assemblies/src/Patches/ConversationDisplayTextPatch.cs:53-64`, `Mods/QudJP/Assemblies/src/Patches/MainMenuLocalizationPatch.cs:42-52`, `Mods/QudJP/Assemblies/src/Patches/OptionsLocalizationPatch.cs:37-44`, `Mods/QudJP/Assemblies/src/Patches/DescriptionLongDescriptionPatch.cs:84-96`, `Mods/QudJP/Assemblies/src/Patches/LookTooltipContentPatch.cs:69-81`, `Mods/QudJP/Assemblies/src/Patches/QudMenuBottomContextTranslationPatch.cs:91-103`.

## 1. Map of current sink translation paths

### `MessageLogPatch`

`MessageLogPatch` is the purest sink fallback: it strips the direct marker and then hands everything else to `MessagePatternTranslator` (`Mods/QudJP/Assemblies/src/Patches/MessageLogPatch.cs:25-37`).

What it translates today:

- Any `messages.ja.json` regex/template family reached through `AddPlayerMessage`.
- Death-wrapper log messages because `MessagePatternTranslator.TranslateStripped(...)` checks `DeathWrapperFamilyTranslator.TryTranslateMessage(...)` before iterating the JSON patterns.

Evidence: `Mods/QudJP/Assemblies/src/MessagePatternTranslator.cs:104-142`.

### `PopupTranslationPatch`

`PopupTranslationPatch.TranslatePopupTextForRoute(...)` is a six-step mutating chain:

1. strip `'\x01'`
2. popup-menu-item hotkey handling
3. popup-specific regex/template handling
4. exact/lower-ascii lookup
5. `MessagePatternTranslator`
6. `UITextSkinTranslationPatch`

Evidence: `Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs:243-279,291-314`.

Inline popup-specific families currently owned only here:

- attack prompt
- refuses-to-speak prompt
- hostility prompt
- delete-save prompt
- delete-title prompt
- hotkey labels
- numbered conversation choices
- death-wrapper popup family
- abandon prompt

Evidence: `Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs:183-241,363-565`.

### `PopupMessageTranslationPatch`

`PopupMessageTranslationPatch` is not an independent translator; it forwards message/title/buttons/items/context-title through `PopupTranslationPatch.TranslatePopupTextForRoute(...)` (`Mods/QudJP/Assemblies/src/Patches/PopupMessageTranslationPatch.cs:50-55,62-109`).

### `UITextSkinTranslationPatch`

`UITextSkinTranslationPatch` is the largest architectural problem because it combines:

- route inference by stack trace,
- dedicated route helpers,
- sink-specific templates,
- generic exact lookup,
- generic status-line handling,
- trimmed lookup,
- final exact-key catch-all.

The route inference alone is a strong sign that ownership is happening too late: `ResolveObservabilityContextFromStack(...)` builds a stack trace, extracts type names, and guesses whether the text belongs to chargen, character status, factions, main menu, options, pick-target, or popup (`Mods/QudJP/Assemblies/src/Patches/UITextSkinTranslationPatch.cs:679-805`).

The mutating stages are:

- dedicated-route helpers: factions, character status, inventory/equipment, pick-target
- popup-template path when popup context is inferred
- sink-template helpers: skills/powers, exact lookup, hotkey labels, compare/status/HP/level lines
- trimmed lookup
- final `Translator.Translate(stripped)`

Evidence: `Mods/QudJP/Assemblies/src/Patches/UITextSkinTranslationPatch.cs:128-156,401-488,567-591`.

## 2. Which translations are only reachable via sink fallback

### Message-log-only families

If a message reaches `AddPlayerMessage` without `'\x01'`, the only remaining translation path is `MessageLogPatch -> MessagePatternTranslator` (`Mods/QudJP/Assemblies/src/Patches/MessageLogPatch.cs:25-37`).

That includes:

- message-frame misses from `XDidYTranslationPatch`, because `XDidYTranslationPatch` returns `true` and lets the original English producer run whenever translation fails (`Mods/QudJP/Assemblies/src/Patches/XDidYTranslationPatch.cs:276-279,309-314,373-376,420-429,517-529,582-596`)
- non-`XDidY` message families still covered only by `messages.ja.json`
- death-wrapper log messages still routed through `MessagePatternTranslator`

This is why holes are masked today: the producer can fail quietly, and the sink may still salvage the result.

### Popup-only or popup-chain-only families

The popup-specific prompt families listed above are only translated in `PopupTranslationPatch` today. There is no producer-side popup claim protocol analogous to `XDidYTranslationPatch` (`Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs:183-241,363-565`).

In addition, any popup text that misses popup templates and misses `MessagePatternTranslator` can still be translated by the generic `UITextSkinTranslationPatch` fallback because popup translation delegates into it (`Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs:272-279`).

### `UITextSkin`-only families

The following routes still depend on `UITextSkinTranslationPatch` as their actual translator rather than just as a sink:

- conversation text (`ConversationDisplayTextPatch`)
- main menu collections (`MainMenuLocalizationPatch`)
- options screen collections (`OptionsLocalizationPatch`)
- long descriptions after compare-status handling (`DescriptionLongDescriptionPatch`)
- tooltips after compare-status handling (`LookTooltipContentPatch`)
- popup bottom-context menu items after popup-item translation misses (`QudMenuBottomContextTranslationPatch`)
- any unclassified `SetText` route not recognized by stack inference

Evidence: `Mods/QudJP/Assemblies/src/Patches/ConversationDisplayTextPatch.cs:53-64`, `Mods/QudJP/Assemblies/src/Patches/MainMenuLocalizationPatch.cs:42-52`, `Mods/QudJP/Assemblies/src/Patches/OptionsLocalizationPatch.cs:37-44`, `Mods/QudJP/Assemblies/src/Patches/DescriptionLongDescriptionPatch.cs:84-96`, `Mods/QudJP/Assemblies/src/Patches/LookTooltipContentPatch.cs:69-81`, `Mods/QudJP/Assemblies/src/Patches/QudMenuBottomContextTranslationPatch.cs:96-103`.

## 3. Load-bearing sink translations: what breaks if each sink becomes observation-only

### `PopupTranslationPatch` / `PopupMessageTranslationPatch`

This is the safest first cut, but it is still load-bearing.

What would be lost immediately:

- attack / hostility / refuses-to-speak / delete / abandon popup families
- numbered conversation choices
- popup death-wrapper text
- popup menu hotkey labels and styled hotkey labels
- popup exact-leaf labels currently handled by case-fallback lookup
- any popup text currently salvaged by `MessagePatternTranslator`
- any popup text currently salvaged only by the final `UITextSkinTranslationPatch` fallback

Evidence: `Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs:243-279,363-565`, `Mods/QudJP/Assemblies/src/Patches/PopupMessageTranslationPatch.cs:50-55,62-109`.

Categorization:

- **Producer exists but has a gap:** conversation text already has `ConversationDisplayTextPatch`, but popup-numbered choice wrapping still lives in the sink.
- **Needs a new producer patch:** most prompt families above.
- **Static leaves that should move upstream:** stable popup/button/menu labels and delete/confirm titles.

### `MessageLogPatch`

This is still heavily load-bearing.

What would be lost immediately:

- every `messages.ja.json` family still reached through `AddPlayerMessage`
- every `XDidYTranslationPatch` miss currently rescued by `MessagePatternTranslator`
- death-wrapper log families still reached via `MessagePatternTranslator`

Evidence: `Mods/QudJP/Assemblies/src/Patches/MessageLogPatch.cs:25-37`, `Mods/QudJP/Assemblies/src/Patches/XDidYTranslationPatch.cs:276-279,309-314,373-376,420-429,517-529,582-596`, `Mods/QudJP/Assemblies/src/MessagePatternTranslator.cs:104-142`.

Categorization:

- **Producer exists but has a gap:** structured message-frame families that belong in `XDidYTranslationPatch` / `MessageFrameTranslator`.
- **Needs a new producer patch:** non-`XDidY` message emitters and death-message producers.
- **Static leaves that should move upstream:** exact log strings that never needed regex in the first place.

### `UITextSkinTranslationPatch`

This is the most load-bearing sink by far.

What would be lost immediately:

- character status sink helpers
- skills/powers sink helpers
- inventory/equipment sink helpers
- pick-target sink helpers
- generic hotkey labels
- exact leaf lookups
- compare/status/HP/level lines still owned at sink time
- trimmed lookup
- the final global `Translator.Translate(stripped)` catch-all
- every wrapper route listed in the previous section

Evidence: `Mods/QudJP/Assemblies/src/Patches/UITextSkinTranslationPatch.cs:128-156,401-488,567-591`, `Mods/QudJP/Assemblies/src/Patches/CharacterStatusScreenTranslationPatch.cs:37-54`, `Mods/QudJP/Assemblies/src/Patches/InventoryAndEquipmentStatusScreenTranslationPatch.cs:30-92`.

Categorization:

- **Producer exists but has a gap:** character status, skills/powers, factions, inventory/equipment all have route patches but still depend on sink-owned helpers.
- **Needs a new producer patch:** conversation text, tooltip content, long descriptions, popup-bottom-context normalization, various unclassified `SetText` routes.
- **Static leaves that should move upstream:** main menu/options labels and other genuine screen-local leaves that can live in route-scoped exact dictionaries.

## 4. `MessagePatternTranslator`: current role and required future treatment

The current `messages.ja.json` file contains **359 pattern entries** (local count on 2026-03-23).

### What is statically knowable today

There are only four direct code call sites:

- sink callers:
  - `MessageLogPatch`
  - `PopupTranslationPatch`
- intentional producer callers:
  - `JournalTextTranslator.TryTranslateDisplayText(...)`
  - `JournalTextTranslator.TryTranslateLines(...)`

Evidence: `Mods/QudJP/Assemblies/src/Patches/MessageLogPatch.cs:36`, `Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs:272`, `Mods/QudJP/Assemblies/src/Patches/JournalTextTranslator.cs:59,109`.

### What is **not** statically knowable today

The current architecture gives patterns **no ownership metadata**. A pattern is not tagged as "message-log only", "popup only", "journal only", or "producer-owned". Matching is global and content-driven. So the question "how many of the 359 patterns are sink-only vs producer-owned" is **not representable in the current code**.

That is itself an architectural defect: the current pattern layer is a global fallback pool rather than an owned route manifest.

### Practical interpretation for migration

Treat the current 359 patterns as:

- **0 explicitly producer-owned patterns**
- **359 globally shared fallback patterns**
- **an unknown journal-used subset**

The required migration step is therefore to classify all 359 patterns into explicit destinations:

1. message-frame producer logic
2. popup producer logic
3. journal/history producer logic
4. route-scoped leaf dictionaries
5. delete

This aligns with the source-first rewrite guidance that already marks `UITextSkinTranslationPatch`, `MessagePatternTranslator`, `PopupTranslationPatch`, and `MessageLogPatch` as Tier 2 rewrite targets once route inventory exists (`docs/source-first-design.md:352-366`).

### What should happen to each pattern class

| Pattern class | Future home | Why |
| --- | --- | --- |
| Combat/action/message-frame patterns | `XDidYTranslationPatch` + `MessageFrameTranslator` | These are semantic producers, not sink regexes. |
| Popup prompt families | Popup call-site producer patches | Popup sink fallback should not own semantic prompts. |
| Journal/history patterns | `JournalTextTranslator` or a future route-scoped history producer | Journal is the only current intentional producer-side pattern user. |
| Exact/no-capture leaves | Route-scoped dictionaries / exact translators | Regex is unnecessary here. |
| Recursive `{t0}` sink tricks and punctuation duplicates | Delete after ownership is moved upstream | They hide composition problems and suppress missing-key visibility. |

## 5. Recommended migration order and dependency graph

### Phase 0: observability first

Do not remove any fallback before measurement is trustworthy.

Needed first:

- route-correct observation for sink fallthroughs
- consistent primary-route extraction for `MessagePatternTranslator`
- duplicate suppression for nested chains like popup -> `UITextSkin`

Why: `DynamicTextObservability` already has a useful route/family format, but current sink chains still blur route attribution, and `MessagePatternTranslator` currently records successful transforms under its own name rather than preserving caller ownership (`Mods/QudJP/Assemblies/src/DynamicTextObservability.cs:28-60`, `Mods/QudJP/Assemblies/src/MessagePatternTranslator.cs:122-127`).

### Phase 1: classify the 359 `MessagePatternTranslator` patterns

Before any sink cutover, produce an explicit manifest for all pattern families.

This is also consistent with existing scanner-era planning: the runtime-sites strategy already treats `MessagePatternTranslator` as useful for bounded families but risky as a dumping ground, and expects scanner-driven ownership decisions rather than perpetual sink regex growth (`docs/superpowers/plans/2026-03-23-runtime-sites-strategy.md:39-57,82-91`).

### Phase 2: cut popup paths first

`PopupTranslationPatch` / `PopupMessageTranslationPatch` should be the first sinks converted to observation-only, but only after popup-specific producer patches exist for the currently inlined template families.

Reasoning:

- bounded surface area
- visible regressions
- no stack-based route guessing
- fewer downstream dependents than `MessageLogPatch` or `UITextSkinTranslationPatch`

### Phase 3: cut message-log fallback next

After popup producers are in place, finish message-log producer ownership:

- expand `XDidYTranslationPatch`
- move death wrappers to a producer seam
- add producers for remaining non-`XDidY` message families

Only then can `MessageLogPatch` become a pure marker-stripper + observer.

### Phase 4: convert `UITextSkin` route by route

Do **not** flip `UITextSkinTranslationPatch` globally.

Convert it per route:

1. character status
2. skills and powers
3. factions
4. inventory/equipment
5. conversation / tooltip / description wrappers
6. main menu / options / other leaf screens

For each route, move logic out of `TryTranslateDedicatedRouteText(...)` and `TryTranslateUITextSinkTemplate(...)` into explicit route patches, then remove that route from sink mutation.

### Phase 5: move display names last

Display-name translation is structurally rich and already has special-case builder-field logic for figurine / warlord / legendary families. It should be migrated last, from postfix decomposition to builder-side ownership (`Mods/QudJP/Assemblies/src/Patches/GetDisplayNameProcessPatch.cs:100-188`, `Mods/QudJP/Assemblies/src/Patches/GetDisplayNameRouteTranslator.cs:187-799`).

## 6. Observation-only sink design

### Recommended behavior

An observation-only sink should:

1. strip `'\x01'` and return silently if claimed
2. skip already-localized text
3. log a structured observation when unclaimed untranslated text reaches the sink
4. never mutate text as a generic fallback

### Recommended log shape

Use a new event family that mirrors existing observability style:

```text
[QudJP] SinkObserve/v1: sink='UITextSkinTranslationPatch' route='CharacterStatusScreenTranslationPatch' detail='field=mutationDetails' reason='producer-missing' hit=4 claimed=false source='You are bleeding' stripped='You are bleeding'.
```

Suggested metadata:

- `sink`
- `route` (primary route via `ExtractPrimaryContext`)
- `detail` (secondary detail via `ComposeContext`)
- `reason` (`unclaimed-text`, `producer-missing`, `delegated-downstream`, `already-localized`)
- `claimed`
- `source`
- `stripped`

Existing infrastructure to reuse:

- `ObservabilityHelpers.ShouldLogMissingHit(...)` for power-of-2 throttling
- `DynamicTextObservability` route/family style
- `Translator.PushLogContext(...)` / `GetCurrentLogContextSuffix()` for contextual suffixes

Evidence: `Mods/QudJP/Assemblies/src/ObservabilityHelpers.cs:13-47`, `Mods/QudJP/Assemblies/src/DynamicTextObservability.cs:28-60`, `Mods/QudJP/Assemblies/src/Translator.cs:138-168`.

### Duplicate suppression rule

The outermost sink should log; downstream delegates should not.

Examples:

- popup -> `UITextSkin`: popup logs, `UITextSkin` stays quiet
- wrapper patch -> `UITextSkin`: wrapper logs, generic sink stays quiet

This avoids noisy duplicate observations when nested fallback chains are removed.

## 7. Concrete architectural changes required

1. **Preserve the direct marker protocol, but expand producer use of it.**

   The marker is already the effective claim system. Do not replace it with a new global registry as a prerequisite.

2. **Remove mutating fallback edges in this order:**

   - `PopupTranslationPatch -> MessagePatternTranslator`
   - `PopupTranslationPatch -> UITextSkinTranslationPatch`
   - `MessageLogPatch -> MessagePatternTranslator`
   - route-specific branches inside `UITextSkinTranslationPatch`
   - final `UITextSkinTranslationPatch -> Translator.Translate(stripped)`

3. **Turn producer-looking relays into real route owners.**

   `ConversationDisplayTextPatch`, `MainMenuLocalizationPatch`, `OptionsLocalizationPatch`, `DescriptionLongDescriptionPatch`, `LookTooltipContentPatch`, and `QudMenuBottomContextTranslationPatch` must stop depending on sink helpers for actual translation.

4. **Make `MessagePatternTranslator` scoped, not global.**

   Its long-term homes are journal/history and perhaps a small number of explicitly approved route-scoped structured families, not global message-log or popup fallback.

5. **Move display-name translation upstream only after simpler sinks are stabilized.**

   The display-name route is too structurally rich to be an early migration target.

## 8. Recommended end-state

The stable end-state should look like this:

- producers fully own dynamic/template families
- route patches own route-local leaf lookup
- sinks only strip claims, skip already-localized text, and emit structured observation
- untranslated text reaching a sink becomes visible immediately as a producer gap

That end-state satisfies the original architectural goal: producer ownership becomes auditable, holes stop being masked by generic regex salvage, and root-cause discovery happens at the route that failed rather than at the last UI sink.
