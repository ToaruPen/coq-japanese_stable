# Popup translation architecture

Research report for popup translation coverage in QudJP.

Scope: architecture only. No production code changes were made as part of this investigation.

## Executive summary

Popup translation is already partially implemented, but the current hook set only covers part of the game's real popup surface.

The current mod actively translates these routes:

- `Qud.UI.PopupMessage.ShowPopup` via `PopupMessageTranslationPatch` (`Mods/QudJP/Assemblies/src/Patches/PopupMessageTranslationPatch.cs:9-114`)
- `XRL.UI.Popup.Show`, `ShowYesNo`, and `ShowYesNoCancel` via `PopupShowTranslationPatch` (`Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs:13-101`)
- `XRL.UI.Popup.ShowBlock`, `ShowOptionList`, and `ShowConversation` via `PopupTranslationPatch` (`Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs:11-513`)
- bottom-context popup/menu items via `QudMenuBottomContextTranslationPatch` (`Mods/QudJP/Assemblies/src/Patches/QudMenuBottomContextTranslationPatch.cs:9-113`)
- one special `Popup.AskNumberAsync` trade route via `TradeScreenUiTranslationPatch` (`Mods/QudJP/Assemblies/src/Patches/TradeScreenUiTranslationPatch.cs:12-237`)

The biggest architectural gap is that the game now uses `Popup.PickOption(...)` heavily, but QudJP still hooks the obsolete `ShowOptionList(...)` route instead of `PickOption(...)`. In the decompiled game, `ShowOptionList` is just an obsolete wrapper that forwards to `PickOption` (`~/Dev/coq-decompiled/XRL.UI/Popup.cs:1637-1643`), while real producers in `XRLCore` and `SifrahGame` call `PickOption` directly (`~/Dev/coq-decompiled/XRL.Core/XRLCore.cs:903-974,1259,1364`; `~/Dev/coq-decompiled/XRL/SifrahGame.cs:597`). That is the most likely reason popup coverage stalls around "some exact-message popups work, but many option-list popups do not."

The recommended direction is a hybrid:

1. Expand dictionary coverage for true static popup leaf strings that already flow through existing producer hooks.
2. Add a small number of new Harmony hooks for uncovered generic popup entry points, especially `PickOption` and the generic input prompts.
3. Handle the remaining concatenated/computed routes producer-side, reusing the existing popup producer helpers rather than adding sink-side dictionary translation.

---

## 1. Current popup translation route in QudJP

### 1.1 `PopupTranslationPatch` is an active producer-side translator, not just an observer

`PopupTranslationPatch` currently targets three `XRL.UI.Popup` methods:

- `ShowBlock`
- `ShowOptionList`
- `ShowConversation`

This is implemented through `HarmonyTargetMethods()` plus a single `Prefix` dispatcher (`Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs:47-100`).

For those methods, the patch rewrites:

- `ShowBlock`: message and title (`PopupTranslationPatch.cs:102-106`)
- `ShowOptionList`: title, options, intro, spacing text, and button/item text (`PopupTranslationPatch.cs:108-119`)
- `ShowConversation`: title, intro, and options (`PopupTranslationPatch.cs:121-126`)

The actual translation path is producer-side:

- direct marker stripping: `MessageFrameTranslator.TryStripDirectTranslationMarker(...)` (`PopupTranslationPatch.cs:183-188`, `231-234`, `257-260`)
- color-safe exact lookup: `StringHelpers.TryGetTranslationExactOrLowerAscii(...)` (`PopupTranslationPatch.cs:280-286`)
- a small set of regex template rewrites, e.g. delete-save prompt and delete title (`PopupTranslationPatch.cs:288-338`, `360-397`)
- death-popup-specific producer translation through `DeathWrapperFamilyTranslator.TryTranslatePopup(...)` (`PopupTranslationPatch.cs:274-278`)
- `MessagePatternTranslator` fallback, but only for `PopupShowTranslationPatch` route names (`PopupTranslationPatch.cs:340-358`)

So the main popup path is already "producer-side translate if possible; otherwise leave English unchanged."

### 1.2 `TranslatePopupTextForRoute` is observation-only

There is a second helper, `TranslatePopupTextForRoute`, that looks superficially similar but is intentionally observation-only:

- it strips the direct-translation marker
- it logs an unclaimed sink observation when the text is still English
- it returns the source unchanged

See `PopupTranslationPatch.cs:183-202`.

The tests explicitly lock that behavior in:

- `TranslatePopupTextForRoute_ObservationOnly_ReturnsSourceUnchanged`
- `TranslatePopupTextForRoute_ObservationOnly_LogsUnclaimed`
- `TranslatePopupTextForRoute_DirectMarker_StillStripped`

(`Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupTranslationPatchTests.cs:1045-1068`)

This matters because the architecture intentionally avoids reintroducing broad sink-side popup dictionary translation.

### 1.3 `PopupShowTranslationPatch` actively translates `Show` / `ShowYesNo` / `ShowYesNoCancel`

`PopupShowTranslationPatch` targets:

- `Popup.Show`
- `Popup.ShowYesNo`
- `Popup.ShowYesNoCancel`

(`Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs:18-83`)

Its `Prefix` rewrites argument 0 through `PopupTranslationPatch.TranslatePopupTextForProducerRoute(...)` (`PopupShowTranslationPatch.cs:85-100`).

Despite the stale summary comment saying "observes Message parameter" (`PopupShowTranslationPatch.cs:9-12`), this patch is active translation, not observation-only.

This is also the only popup route that enables the `MessagePatternTranslator` fallback, because `PopupTranslationPatch.ShouldTryMessagePatternFallback(...)` returns true only for `nameof(PopupShowTranslationPatch)` (`Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs:340-358`).

One subtlety: `Popup.Show(...)` in the game forwards into `ShowBlock(...)` (`~/Dev/coq-decompiled/XRL.UI/Popup.cs:658-671,1118-1141`). So a `Popup.Show(...)` message can be translated once by `PopupShowTranslationPatch` and then still pass through the `ShowBlock`-side popup patch. That matches current runtime evidence, where `Game saved!` produced probes for both routes (`~/Library/Logs/Freehold Games/CavesOfQud/Player.log:1302-1303`).

Runtime evidence confirms that it is live: `Player.log` currently contains:

- `route='PopupShowTranslationPatch' ... source='Game saved!' translated='ゲームをセーブしました！'`

(`~/Library/Logs/Freehold Games/CavesOfQud/Player.log:1302`)

### 1.4 `PopupMessageTranslationPatch` is the modern UI convergence hook

Modern popup UI is rendered through `Qud.UI.PopupMessage.ShowPopup(...)`, and QudJP patches that directly:

- target resolution: `PopupMessageTranslationPatch.cs:22-39`
- translated arguments: message, buttons, items, title, context title (`PopupMessageTranslationPatch.cs:41-114`)

This patch delegates all string translation back to `PopupTranslationPatch.TranslatePopupTextForProducerRoute(...)`, so the dictionary/template/death-wrapper behavior is shared (`PopupMessageTranslationPatch.cs:109-113`).

Tests show it already handles:

- message translation
- delete-save template/title placeholder reordering
- button text
- item text
- context title
- direct-marker stripping

(`Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupMessageTranslationPatchTests.cs:51-294`)

Runtime evidence also confirms producer-side hits on this route for popup button text:

- `route='PopupMessageTranslationPatch' ... source='{{W|[L]}} {{y|Look}}' translated='{{W|[L]}} {{y|調べる}}'`
- `route='PopupMessageTranslationPatch' ... source='[{{W|Tab}}] {{y|Trade}}' translated='[{{W|Tab}}] {{y|取引}}'`

(`~/Library/Logs/Freehold Games/CavesOfQud/Player.log:372-373,466-510`)

### 1.5 `QudMenuBottomContextTranslationPatch` covers bottom button/menu-item text

`QudMenuBottomContextTranslationPatch` intercepts `Qud.UI.QudMenuBottomContext.RefreshButtons` and normalizes each item text through `PopupTranslationPatch.TranslatePopupMenuItemTextForProducerRoute(...)` (`Mods/QudJP/Assemblies/src/Patches/QudMenuBottomContextTranslationPatch.cs:12-102`).

That route is already tested together with popup menu-item normalization:

- preserves already localized hotkey labels
- skips bottom-context items that should remain unchanged

(`Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupTranslationPatchTests.cs:733-808`)

### 1.6 `TradeScreenUiTranslationPatch` is a special-case popup prompt patch

Trade UI separately patches `Popup.AskNumberAsync(...)` and translates the specific prompt pattern:

- regex: `^Add how many (?<name>.+) to trade\.$`
- template key: `Add how many {0} to trade.`

(`Mods/QudJP/Assemblies/src/Patches/TradeScreenUiTranslationPatch.cs:17-20,93-113,203-237`)

This shows the current codebase already accepts "generic popup hook + producer-specific escape hatch" as a pattern.

### 1.7 `StartReplaceTranslationPatch` is not the popup route

`StartReplaceTranslationPatch` patches `GameTextExtensions.StartReplace(string)` and loads `Dictionaries/templates-variable.ja.json` (`Mods/QudJP/Assemblies/src/Patches/StartReplaceTranslationPatch.cs:14-17,36-45,79-100,148-156`).

That is a generic `=variable=` template hook, not a popup-specific interception layer. Nothing in the popup patches routes popup text through it, and popup coverage should not assume it will catch `Popup.Show*` / `PopupMessage.ShowPopup(...)` traffic.

### 1.8 Current popup dictionary assets

`ui-popup.ja.json` contains 126 entries in the current repo. It includes:

- popup buttons and labels
- bottom menu actions
- exact popup messages
- `Popup.AskString.Prompt` strings
- a few `Qud.UI.PopupMessage.ShowPopup.Message` and `XRL.UI.Popup.ShowBlock.Message` entries

(`Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json:1-599`)

Important detail: this dictionary is useful only where text already reaches a translation-aware producer hook. The dictionary alone does not create popup coverage.

Also note that `Translator` loads all `*.ja.json` files in the dictionaries directory into one flat translation map, so popup strings are not isolated to `ui-popup.ja.json` only (`Mods/QudJP/Assemblies/src/Translator.cs:206` and surrounding loader logic).

---

## 2. Game popup architecture

### 2.1 `XRL.UI.Popup` is a static facade

The decompiled game implements popup behavior as a static facade on `XRL.UI.Popup` (`~/Dev/coq-decompiled/XRL.UI/Popup.cs:20-21`).

Relevant static entry points include:

- one-message popups: `Show`, `ShowFail`, `ShowSpace`, `ShowBlock`, `ShowBlockPrompt`, `ShowBlockSpace`, `ShowBlockWithCopy` (`Popup.cs:624-739,923-1189`)
- confirmations: `ShowYesNo`, `ShowYesNoCancel`, async variants (`Popup.cs:2244-2385`)
- option pickers: `PickOption`, `PickSeveral`, `ShowOptionList` obsolete wrapper, `ShowConversation` (`Popup.cs:1559-2002,2039-2131`)
- inputs: `AskNumber`, `AskNumberAsync`, `AskString`, `AskStringAsync` (`Popup.cs:1191-1511`)
- modern popup bridge: `NewPopupMessageAsync` and `WaitNewPopupMessage` (`Popup.cs:751-910`)

### 2.2 There is no built-in localization hook in `Popup`

Inside `Popup.cs`, popup strings are not looked up in any string table.

Instead:

- the caller passes already-built English strings
- `Markup.Transform(...)` only processes color/markup (`Popup.cs:1129,1207,1429,2247,2360`)
- `MessageQueue.AddPlayerMessage(...)` logs the already-built message (`Popup.cs:1123-1137`)

The game does not call a localization system from `Popup` itself.

The popup code therefore does not own translation. Translation must happen:

- before the string reaches `Popup`
- or by intercepting `Popup` method parameters with Harmony

### 2.3 Modern UI path converges on `PopupMessage.ShowPopup(...)`

When `UIManager.UseNewPopups` is true, the sync/static popup APIs eventually reach:

- `Popup.NewPopupMessageAsync(...)`
- `Popup.WaitNewPopupMessage(...)`
- `Qud.UI.PopupMessage.ShowPopup(...)`

(`~/Dev/coq-decompiled/XRL.UI/Popup.cs:751-910`; `~/Dev/coq-decompiled/Qud.UI/PopupMessage.cs:531-560`)

That makes `PopupMessage.ShowPopup(...)` the modern UI convergence point, but not a full popup solution by itself because the legacy TUI path still exists.

### 2.4 `PopupMessage` owns the English button lists

`Qud.UI.PopupMessage` defines the static/default modern-UI button text:

- `CancelButton`, `CopyButton`, `LookButton`, `SingleButton`
- `YesNoButton`, `YesNoCancelButton`
- `AcceptCancelButton`, `SubmitCancelButton`, `SubmitCancelHoldButton`
- `AcceptCancelTradeButton`

(`~/Dev/coq-decompiled/Qud.UI/PopupMessage.cs:44-217,255-523`)

These are plain English `QudMenuItem.text` values such as:

- `{{W|[Esc]}} {{y|Cancel}}`
- `{{W|[space]}} {{y|Continue}}`
- `{{W|[y]}} {{y|Yes}}`
- `{{y|Submit}}`

(`PopupMessage.cs:46-217`)

So button translation needs either:

- direct patching of `PopupMessage.ShowPopup(...)` item/button text, which QudJP already does
- or direct patching of the static button factories

### 2.5 Representative producer examples

#### `XRLCore`

Static strings:

- `Popup.Show("You can only set your checkpoint in settlements.")` (`~/Dev/coq-decompiled/XRL.Core/XRLCore.cs:1034`)
- `Popup.Show("Game saved!")` (`XRLCore.cs:1062`)
- `Popup.ShowYesNoCancel("Are you sure you want to restore your checkpoint?")` (`XRLCore.cs:1043`)

Concatenated strings:

- health warning: `Popup.ShowSpace("{{R|Your health has dropped below {{C|" + Globals.HPWarningThreshold + "%}}!}}")` (`XRLCore.cs:722`)
- world-seed block: `Popup.ShowBlockWithCopy(...)` built by string concatenation (`XRLCore.cs:1057`)
- item/target state messages such as missile reload / hostility / pathing failures (`XRLCore.cs:1775-1999`)

Input prompts:

- quit-confirmation `AskString(...)` with embedded typed keyword (`XRLCore.cs:996,1661`)
- wait-count `AskNumber(...)` (`XRLCore.cs:1471`)

Option menus:

- wait style / move style menus use `PickOption(...)` (`XRLCore.cs:903,939`)
- checkpoint/game menu uses `PickOption(...)` with multiple string arrays (`XRLCore.cs:970-974`)
- destination/POI menus use `PickOption(...)` (`XRLCore.cs:1259,1364`)

#### `SifrahGame`

Concatenated or computed text:

- `Popup.ShowFail("You have already chosen the correct option for " + sifrahSlot.Description + ".")` (`~/Dev/coq-decompiled/XRL/SifrahGame.cs:536`)
- `Popup.PickOption("Use which option for " + sifrahSlot.Description + "?", ...)` (`SifrahGame.cs:597`)
- `Popup.ShowFail("You have already eliminated " + sifrahToken2.Description + " as a possibility.")` (`SifrahGame.cs:605`)
- `Popup.ShowFail("Choosing " + sifrahToken2.Description + " is disabled for this turn.")` (`SifrahGame.cs:616`)

Static confirmations:

- `Popup.ShowYesNo("You haven't selected an option for every slot...")` (`SifrahGame.cs:629`)
- `Popup.ShowYesNo("You aren't finished! Are you sure you want to abort the process?")` (`SifrahGame.cs:634`)

StringBuilder/computed:

- explanation popups built by `stringBuilder.ToString()` (`SifrahGame.cs:849-856,875-879`)

---

## 3. Dynamic vs static popup classification

### 3.1 Methodology

I ran a mechanized scan across the canonical decompiled `.cs` files under `~/Dev/coq-decompiled/`, excluding the duplicate flat `XRL.*.cs` mirror files, and extracted popup-facing text slots from:

- `Show*`
- `Ask*`
- `ShowYesNo*`
- `ShowConversation`
- `PickOption`
- `PickSeveral`
- `ShowColorPicker`

I then compared string literals inside those expressions to the current QudJP dictionary keys under `Mods/QudJP/Localization/Dictionaries/*.json`.

This is an architecture estimate, not a merge-blocking inventory. The purpose is to size the backlog and separate leaf strings from producer-owned dynamic routes.

### 3.2 Results

After filtering out `null` / empty scaffolding:

- unresolved popup-facing text slots: **1,374**
- unmatched by kind:
  - **556 static**
  - **505 concatenated**
  - **313 computed**
  - **template:** effectively negligible in the unresolved set

For static leaf strings specifically, the scan found **477 unique unmatched English literals**. That is close to the user's "~458 untranslated popups" estimate and suggests that the "~458" backlog is mostly describing the static-literal subset rather than the full producer-surface problem.

### 3.3 Category breakdown

#### (a) Static strings

Estimated size:

- **~477 unique untranslated static popup literals**
- **556 unresolved static slots** before deduping duplicates

Examples:

- `Are you sure you want to save and quit?`
- `Are you sure you want to delete this saved game?`
- `All options are correct responses to some requirement.`
- `Choose a body part to engrave.`
- `Are you sure you want to ascend the Spindle?...`

These are the best dictionary-only targets.

Primary approach:

- add exact entries to `ui-popup.ja.json` or another popup-specific dictionary
- rely on existing producer-side popup hooks where they already fire

#### (b) Template strings

Estimated size:

- **very small as direct unresolved popup literals**

Why so small:

- much of the current popup producer code uses concatenation rather than stable `{0}` / `string.Format(...)` templates
- QudJP already handles a few high-value popup templates directly:
  - delete-save prompt
  - delete title
  - duplicate build code
  - manage build title

(`Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs:288-338`)

Primary approach:

- where a producer uses a truly stable pattern, add a reusable regex/template translator
- keep this limited to obvious high-value reusable families

#### (c) Concatenated strings

Estimated size:

- **~505 unresolved concatenated popup text slots**

Examples:

- `"Use which option for " + sifrahSlot.Description + "?"`
- `"You do not autoattack " + gameObject7.t() + " because " + gameObject7.itis + " not hostile to you."`
- `"{{R|Your health has dropped below {{C|" + Globals.HPWarningThreshold + "%}}!}}"`

These cannot be solved well by sink-side dictionary lookup because the English surface is assembled at runtime.

Primary approach:

- producer-side Harmony patch or helper
- normalize the route to a reusable template
- pass the final English through the existing popup producer translation helpers if the expression can be reduced to a stable visible template

#### (d) Computed strings

Estimated size:

- **~313 unresolved computed popup text slots**

Examples:

- `stringBuilder.ToString()`
- `SuccessMessage`
- `message`
- arrays/lists built elsewhere and passed into `PickOption`

These are the most upstream-owned routes.

Primary approach:

- inspect the producing function rather than the popup sink
- patch earlier, closer to the domain object or content generator
- use L3/runtime evidence before deciding whether a stable leaf exists

### 3.4 Highest-volume owners

The biggest popup owners in the current decompiled scan are:

- `XRL.World.Capabilities/Wishing.cs`
- `XRL.World.Parts/Campfire.cs`
- `XRL.Core/XRLCore.cs`
- `XRL.World/GameObject.cs`
- `XRL.UI/TradeUI.cs`
- `XRL.World.Parts/SpindleNegotiation.cs`
- `XRL.World.Parts/Inventory.cs`
- `XRL/SifrahGame.cs`

That strongly suggests that "improve popup coverage" is not one problem. It is a combination of:

- generic popup interception gaps
- plus a few very large producer domains

---

## 4. TDD strategy

### 4.1 What belongs in L1

L1 is for pure logic only (`docs/test-architecture.md:19-38`).

Popup-related logic that can live in L1:

- regex/template translators extracted from popup producer helpers
- hotkey-label normalization logic
- exact-lookup / already-localized detection helpers
- observability helpers

The repo already has L1 coverage for popup observability primitives:

- `SinkObservationTests` (`Mods/QudJP/Assemblies/QudJP.Tests/L1/SinkObservationTests.cs:6-131`)
- `DynamicTextObservabilityTests` (`Mods/QudJP/Assemblies/QudJP.Tests/L1/DynamicTextObservabilityTests.cs:6-54`)

If more popup translation logic is extracted into pure helpers, it should be tested at L1 first.

### 4.2 What belongs in L2

L2 is where current popup architecture is already validated using DummyTargets (`docs/test-architecture.md:67-102`).

Existing L2 popup tests already prove:

- `PopupTranslationPatch` rewrites `ShowBlock`, `ShowOptionList`, and `ShowConversation` payloads
- some routes are intentionally observation-only
- death wrappers work
- popup menu item translation preserves markup
- `PopupShowTranslationPatch` rewrites `Popup.Show` and pattern-fallback cases
- `PopupMessageTranslationPatch` rewrites message/buttons/items/title/context title

Key files:

- `Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupTranslationPatchTests.cs`
- `Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs`
- `Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupMessageTranslationPatchTests.cs`

I ran the popup-focused existing test slice during this investigation:

- `56` popup/observability tests passed

Recommended new L2 cases if implementation work happens later:

- `PickOption` title / intro / options / buttons rewrite
- generic `AskString` prompt rewrite
- generic `AskNumber` prompt rewrite
- `ShowSpace` / `ShowBlockWithCopy` prompt rewrite
- gamepad-style `PopupMessage` button text

### 4.3 What belongs in L2G

L2G should verify hook resolution on the actual game DLL (`docs/test-architecture.md:47-66`).

Current target-resolution coverage already includes:

- `PopupTranslationPatch`
- `PopupShowTranslationPatch`

(`Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs:322-333`)

If new popup hooks are added, the first follow-up should be extending L2G with the new signatures:

- `PopupMessageTranslationPatch` target resolution, which is currently missing from L2G
- `Popup.PickOption`
- `Popup.AskString`
- `Popup.AskNumber`
- possibly `Popup.PickSeveral` / `Popup.ShowSpace`

### 4.4 What belongs in L3

L3 is still necessary for:

- modern vs legacy popup path behavior
- controller/gamepad button labels
- actual Unity rendering and line wrap
- copy button / scroll prompts / popup placement
- ensuring no visual regressions in popup containers

(`docs/test-architecture.md:106-152`)

Critical L3 scenarios:

- save / quit / abandon confirm flows
- checkpoint restore flow
- autostair and world-map confirmations
- Sifrah option-selection popup
- trade quantity prompt
- popup button rows on keyboard and gamepad

### 4.5 Most important future test cases

If implementation work starts later, these are the highest-value tests to add first:

1. `PickOption` rewrites checkpoint/game menu options from `XRLCore`.
2. `PickOption` rewrites Sifrah titles/options.
3. `AskString` rewrites quit / abandon prompts.
4. `AskNumber` rewrites generic count prompts outside Trade UI.
5. `ShowSpace` rewrites HP-threshold warning.
6. `ShowBlockWithCopy` rewrites world-seed popup message/prompt/title.
7. `PopupMessage` gamepad button variants translate `Yes/No/Cancel/Submit/Color/Hold to Accept`.

---

## 5. Recommended architecture

### 5.1 Do not rely on dictionary expansion alone

Dictionary-only work will improve the static-literal subset, but it will not solve the core coverage problem because:

- many popup producers are concatenated or computed
- several important popup APIs are not currently hooked
- `PickOption` is the dominant option-list API in the game, and current QudJP coverage is aimed at `ShowOptionList`

### 5.2 Do not move to a pure sink-side `PopupMessage` / `Popup` catch-all

That would conflict with the repo's existing translation direction:

- sink-side observation-only helpers already exist on purpose
- tests explicitly preserve that behavior
- many popup strings are not true stable leaves by the time they hit the sink

The current codebase prefers producer-side or near-producer fixes when the route is dynamic (`AGENTS.md:32-37`; `Mods/QudJP/Assemblies/AGENTS.md:39-47`).

### 5.3 Recommended answer: a hybrid approach

#### Keep and extend the existing generic popup producer hooks

The smallest high-leverage hook expansion is:

- keep `PopupMessageTranslationPatch`
- keep `PopupShowTranslationPatch`
- keep `PopupTranslationPatch`
- extend coverage to the modern generic APIs the game actually uses:
  - `Popup.PickOption`
  - `Popup.AskString`
  - `Popup.AskNumber`
  - probably `Popup.ShowSpace`
  - optionally `Popup.PickSeveral`

#### Reuse the existing popup producer helpers

New hooks should keep reusing:

- `PopupTranslationPatch.TranslatePopupTextForProducerRoute(...)`
- `PopupTranslationPatch.TranslatePopupMenuItemTextForProducerRoute(...)`

That preserves:

- exact dictionary lookup
- color-safe restore
- death-wrapper handling
- dynamic-text observability
- sink-observation discipline

#### Add producer-specific patches only where generic popup hooks still cannot help

Examples:

- Sifrah token/slot descriptions
- Trade UI quantity prompts beyond the current special case
- complicated `GameObject` / `Campfire` / `Wishing` string builders

### 5.4 Minimal patch surface area to maximize coverage

If I had to choose the smallest patch set with the biggest coverage jump, it would be:

1. **Patch `Popup.PickOption`**  
   This is the single biggest missed generic surface.

2. **Patch generic `Popup.AskString` / `AskStringAsync` / `AskNumber` / `AskNumberAsync`**  
   This captures many confirmation and entry prompts that today only translate on some modern-UI paths.

3. **Patch `Popup.ShowSpace`**  
   Covers short notification popups like the HP threshold warning.

4. **Only after that, add producer-specific patches for large dynamic owners**  
   Especially `SifrahGame`, `Campfire`, `Wishing`, and selected `XRLCore` flows.

I would not start by patching every individual producer.

### 5.5 Existing codebase patterns to follow

Future popup work should follow current repo patterns:

- producer-side translation helper reuse (`PopupTranslationPatch.cs:204-267`)
- exact dictionary lookup first, template/pattern second, no silent broad fallback
- observability for transformed vs unclaimed routes (`DynamicTextObservabilityTests.cs`, `SinkObservationTests.cs`)
- L2 dummy-target tests for patch behavior before L3 runtime confirmation

---

## 6. Priority implementation plan

## Phase 1: dictionary-only, no C# changes

Target:

- the **~477 unique static popup literals** that already flow through existing translation-aware hooks

Best candidates:

- `Popup.Show` / `ShowYesNo` / `ShowYesNoCancel` exact strings
- `PopupMessage.ShowPopup` exact strings
- `ShowConversation` exact title/intro/options where already hooked
- bottom-context labels
- current trade quantity prompt template keys

Expected impact:

- high ROI for the static-literal backlog
- low risk
- likely the fastest route to materially improve coverage without changing architecture

Limit:

- does **not** solve `PickOption` call sites that never touch `ShowOptionList`
- does **not** solve concatenated/computed producers

Effort:

- **small to medium**, mostly translation inventory work

## Phase 2: generic Harmony hook expansion

Target:

- generic popup APIs that are currently under-hooked

Recommended order:

1. `Popup.PickOption`
2. `Popup.AskString` / `AskStringAsync`
3. `Popup.AskNumber` / `AskNumberAsync`
4. `Popup.ShowSpace`
5. optionally `Popup.PickSeveral`

Expected impact:

- biggest architectural coverage jump
- unlocks many existing dictionary entries and future exact translations
- should raise popup coverage much more than adding more exact strings alone

Effort:

- **medium**
- requires new L2/L2G tests but still follows existing patch/helper patterns

## Phase 3: producer-side fixes for dynamic owners

Target:

- routes that remain English because they are concatenated or computed upstream

Priority owners:

- `XRL/SifrahGame.cs`
- `XRL.Core/XRLCore.cs`
- `XRL.World.Parts/Campfire.cs`
- `XRL.World.Capabilities/Wishing.cs`
- `XRL.World/GameObject.cs`
- `XRL.UI/TradeUI.cs`

Expected impact:

- closes the hard remainder after generic popup interception is in place
- required for high coverage, especially on option descriptions and grammar-heavy popups

Effort:

- **medium to large**
- should be done incrementally, with route-by-route L2/L3 evidence

---

## Final recommendation

For expanding popup coverage from ~20% to high coverage, the best path is:

1. **Treat the current popup architecture as partially correct, not fundamentally wrong.**
2. **Use dictionary expansion for the static leaf backlog first.**
3. **Add a new generic Harmony patch on `Popup.PickOption` before anything else.**
4. **Then cover generic input prompts (`AskString` / `AskNumber`) and short notifications (`ShowSpace`).**
5. **Only after those generic hooks are in place, chase the remaining dynamic producers one domain at a time.**

In short:

- **not dictionary-only**
- **not sink-only**
- **yes to a hybrid, producer-biased architecture with a small number of well-chosen generic popup hooks**
