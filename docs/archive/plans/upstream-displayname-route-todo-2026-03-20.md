# Archived Upstream DisplayName Route Notes (2026-03-20)

1. `markup adjective prefix`
source: `GetDisplayNameEvent.AddAdjective(...)` with markup-wrapped ASCII modifier such as `{{graffitied|graffitied}}`
candidate fix: detect leading markup-wrapped modifier before color-span restore and translate the modifier token itself, so `{{graffitied|落書きされた}}塩漬け茎の壁` is emitted without malformed markup reassembly.

2. `bracketed state suffix`
source: effect / part `GetDisplayNameEvent.AddTag("[...]")` family (`sitting`, `flying`, `wading`, `prone`, `empty`, `sealed`, `auto-collecting`)
candidate fix: keep `base + [state]` in `GetDisplayName*` and prefer bracket-specific translations like `[empty] -> [空]` before adjective dictionaries such as `empty -> 空の`.

3. `inventory count / code suffix`
source: inventory display names coming from `GameObject.DisplayName` through `InventoryLineData.displayName`
candidate fix: add display-name suffix families for `xN`, `mk I/II/III`, `<AA1>`, and similar non-lexical codes so already localized JP item names are passed through without missing-key noise.

4. `parenthetical item state`
source: display-name suffixes like `(unburnt)` on stackable items
candidate fix: parse `base (state)` as a display-name family and translate the state token through exact/bracket/state lookup.

5. `liquid / servings suffix`
source: `LiquidVolume` and food stack display names like `[32 drams of fresh water]`, `[1 食分]`
candidate fix: extend suffix parser to translate English inner phrases but suppress already localized JP bracket content.

6. `death popup killer reuse`
source: popup death message reusing display-name output
candidate fix: once `GetDisplayName*` suffix families are stable, keep popup-side processing minimal and rely on upstream display-name translation for killer names.

7. `message / movement residuals`
source: `GameObject` / `Physics` movement-block and stance messages
candidate fix: continue filling shared message families only after the display-name route stops feeding malformed or noisy strings downstream.

8. `message / combat extra-damage residuals`
source: `TakeDamage(..., "from %t freezing weapon!", ...)` and similar combat side-effect families
candidate fix: add message-pattern coverage for elemental/weapon-side-effect damage lines after confirming the upstream format in decompiled game code.

9. `message / narrative and yell residuals`
source: startup intro text from `Base/Text.txt` and runtime `... yells, '...'.` families
candidate fix: add exact narrative patterns for fixed intro text and regex message families for quoted yell lines rather than chasing exact-key variants from logs.

10. `popup / option label residuals`
source: popup hotkey labels and death menu options that are plain exact keys rather than display-name output
candidate fix: fill popup dictionary coverage for action labels (`drop`, `learn`, `mark important`, `add notes`) and death menu choices so popup hotkey normalization can resolve them without route work.

11. `InventoryLocalizationPatch target breadth`
source: current patch scans broad inventory-like UI types and can catch downstream string getters that are not inventory-owned translation leaves.
candidate fix: narrow the patch to known upstream-owned inventory text producers, starting with `Qud.UI.InventoryLineData.get_displayName`, and stop using broad fallback scans.

12. `display-name mixed/title family split`
source: current `TryTranslateMixedDisplayName` conflates adjective-prefix display names and title-prefix naming families.
candidate fix: split the route into upstream-backed families based on `GetDisplayNameEvent.ProcessFor(...)` and `Naming.xml` title styles instead of one generic `ASCII token + JP rest` rule.

13. `display-name of-phrase family narrowing`
source: current `TryTranslateOfPhraseDisplayName` is a generic sink-side `JP head + of + ASCII tail` rule, while the confirmed upstream family is `LiquidVolume` description/tag composition.
candidate fix: replace the broad `of` rule with a liquid-description-specific family backed by `LiquidVolume.GetLiquidDescription(...)` and related slot structure.

14. `logic-required slot/cardinality test debt`
source: `UITextSkin` and `Popup` tests still prove many dynamic routes with only one final-string example.
candidate fix: add slot/cardinality boundary tests for HUD sink segments, display-name slot combinations, popup option counts, death/save prompt slot variation, and hotkey character classes.
