# 2026-05-05 issue #497 dynamic dictionary audit

## Goal

Inventory the dynamic-looking localization dictionary entries called out in
GitHub issue #497 and decide whether each entry is safe fixed-leaf coverage,
owner-routed template coverage, a stale dictionary row, or a follow-up owner
patch candidate.

## Non-goals

- Do not remove dictionary coverage in this slice unless an owner route and
  focused regression tests are already proven.
- Do not promote sink-observed `AddPlayerMessage`, generic popup, HistorySpice,
  ConversationTemplate, or placeholder-bearing rows into fixed-leaf coverage.
- Do not treat issue #493's trade/PetEitherOr owner work as part of this debt
  audit. That branch only exposed these pre-existing rows.

## Evidence Used

- `docs/RULES.md` proven fixed-leaf and route-ownership policy.
- `Mods/QudJP/Localization/AGENTS.md` dictionary acceptance rules.
- Current dictionary rows under `Mods/QudJP/Localization/Dictionaries/`.
- Static producer evidence in `docs/static-producer-inventory.json`.
- Decompiled Caves of Qud 1.0.4 source under
  `~/dev/coq-decompiled_stable/`.
- Structural searches with `just sg-cs` for `AddPlayerMessage`, `EmitMessage`,
  `StartReplace`, `JournalAPI.AddAccomplishment`, and conversation APIs.
- Existing QudJP owner translators and L1/L2 tests.

## Result Summary

No issue #497 candidate should be counted as proven fixed-leaf dictionary
coverage.

Some entries are still valid route-scoped template assets because a current
owner translator consumes them deliberately. Others are stale or ineffective
flat dictionary debt and should be removed only after the matching owner route
or route-specific pattern is tested.

## High-risk Inventory

| Candidate | Producer evidence | Ownership class | Confidence | Decision |
| --- | --- | --- | --- | --- |
| `messages.ja.json:62` vital-area non-player hit pattern | `XRL.World.Parts/MissileWeapon.cs` emits dynamic hit messages; current QudJP surface is `GameObjectEmitMessageTranslationPatch` plus message patterns. | producer-owned combat/message pattern, not fixed leaf | high | Keep/reclassify as owner-routed pattern coverage. Added focused `EmitMessage` L2 coverage for both player and non-player vital-area missile hits in this slice; the `message-frame` label is metadata, not a fixed-leaf ownership claim. |
| `messages.ja.json:1827` `OUCH! You collide with ...` | `XRL.World.Parts/Physics.cs` composes collision text with object names; QudJP has `PhysicsObjectEnteringCellTranslationPatch` owner surface. | producer-owned dynamic object-name message | high | Keep/reclassify as owner-routed pattern coverage. Added focused `ObjectEnteringCell` collision L2 coverage in this slice; the `needs-harmony-patch` label is stale. |
| `messages.ja.json:1832` `The way is blocked by ...` | `XRL.World.Parts/Physics.cs` composes blocked-way text; existing L2 coverage exists in `CombatAndLogMessageQueuePatchTests`. | producer-owned dynamic object-name message | high | Keep/reclassify as owner-routed pattern coverage, not fixed leaf. |
| `templates-variable.ja.json:14` `=subject.T= =verb:bask= ...` | `XRL.World.Parts.Mutation/PhotosyntheticSkin.cs` calls `.StartReplace().AddObject(...).EmitMessage(...)`; `StartReplaceTranslationPatch` translates before `ReplaceBuilder`. | mid-pipeline-owned VariableTemplate | high | Keep as `templates-variable` template asset. Do not count as fixed leaf. |
| `ui-conversation.ja.json:13` generated village tinker body | `XRL.World.ZoneBuilders/Village.cs` and `VillageCoda.cs` pass the template to `ConversationsAPI.addSimpleConversationToObject`; QudJP owns it via conversation template patches before player variables expand. | producer/API-construction-owned conversation template | high | Keep in `ui-conversation`. Do not count as display-time fixed leaf. |
| `ui-messagelog.ja.json:123` `You stagger␠` fragment and suffix | `XRL.World.Parts/Combat.cs` composes the full shield-block message; `CombatGetDefenderHitDiceTranslationPatch` and a full `messages.ja.json` pattern already cover the real shape. | prefix/suffix fragment, not owner-safe | high | Removed in this slice after focused L2 coverage confirmed the full shield-block message is translated through the combat/message route. Duplicate stale fragments were also removed from `world-parts.ja.json`. |
| `ui-messagelog-world.ja.json:338` `{subject} {direction} breathes ...` | `XRL.World.Parts.Mutation/BreatherBase.cs` uses XDidY-style message construction; QudJP has `XDidYTranslationPatch`. | XDidY message-frame template, current flat key is ineffective | high | Moved in this slice. `MessageFrames/verbs.ja.json` now owns the known `breath` + `a cone of ...` cases from Breather subclasses, with L1 repository and L2 XDidY patch coverage. The stale flat placeholder row was removed. |
| `ui-messagelog-world.ja.json:683` `You are crippled for␠` | `XRL.World.Effects/Cripple.cs` composes `You are crippled for␠` plus `Duration.Things("turn")`. | producer-owned `Cripple.Apply` dynamic duration message | high | Implemented in this slice: `CrippleApplyTranslationPatch` gates queue translation to `Cripple.Apply`, `messages.ja.json` now owns the full-message pattern, and the stale prefix fragment was removed from `ui-messagelog-world.ja.json`. |
| `world-gospels.ja.json:14` `$creaturePossessive $creatureBodyPart` | `HistorySpice.json` cooking ingredient templates use values prepared by `XRL.World.Parts/Campfire.cs` from creature names and body parts. | generated HistorySpice component template | high | Deferred deliberately. `HistoricStringExpanderPatch` is intentionally disabled to avoid world-generation and symbolic-key contamination, and no bounded scoped HistorySpice translator exists in this slice. Do not count as fixed leaf. |
| `world-gospels.ja.json:444` two-ingredient `*dishName*` recipe template | `HistorySpice.json` recipe-name templates are expanded by `XRL.World.Skills.Cooking/CookingRecipe.cs` with ingredient IDs, display names, title-casing, and a second `HistoricStringExpander.ExpandString` pass. | generated recipe-name template | high | Deferred deliberately. Final recipe names need a dedicated `CookingRecipe.GenerateRecipeName` / `Campfire.DescribeMeal` owner translator; exact observed recipe leaves are not acceptable. |
| `world-parts.ja.json:1143` AbsorbablePsyche gospel | `XRL.World.Parts/AbsorbablePsyche.cs` interpolates month, year, faction, and pronoun values. The literal dictionary key with `{Calendar.GetMonth()}` does not match final runtime text. `journal-patterns.ja.json` already owns the real generated shape. | existing journal-pattern owner, stale exact row | high | Removed in this slice. Existing `JournalPatternTranslatorTests` cover the real generated AbsorbablePsyche shape. |
| `GivesRep` AddAccomplishment mural/gospel variants | `XRL.World.Parts/GivesRep.cs` emits water-bonded and normal kill accomplishments with generated reference names, faction names, HistorySpice tokens, and element-domain murder methods. | journal-pattern owner, not fixed leaf | high | Implemented in this slice. Added generated-template patterns to `journal-patterns.ja.json` and L1/L2 owner tests for bonded water-sib and normal single-combat/murder-method variants. |
| `historyspice-common.ja.json:3` `someone` family | Base `HistorySpice.json` component terms are consumed by message and journal pattern capture translation; `Translator` currently loads all `*.ja.json` globally. | stable HistorySpice component leaf, globally exposed | medium | Keep as component/capture leaf for now, but do not count as general fixed leaf. Future work should move this to scoped capture lookup outside global exact translation. |

## Medium-risk Inventory

| Candidate | Producer evidence | Ownership class | Confidence | Decision |
| --- | --- | --- | --- | --- |
| `ui-popup.ja.json:105`, `:109`, `:113` attack/refusal popup templates | `XRL.World.Parts/Physics.cs` and `ConversationScript.cs` compose final popup text directly. Existing `PopupTranslationPatchTests` assert the generic popup route leaves these unchanged even with dictionary entries. | producer-specific popup helper, not generic popup leaf | high | Removed in this slice as ineffective `{0}` template rows, then replaced with narrow `PopupShowTranslationPatch` producer-text helpers for Physics attack confirmation and ConversationScript refusal messages. |
| `ui-reputation.ja.json:43` `The villagers of {0}` | Qud UI builds faction labels and details dynamically; QudJP owns generated shapes in `FactionsStatusScreenTranslationPatch`. | FactionsStatusScreen owner translator template | high | Keep as route-scoped template, not fixed leaf. |
| `ui-game-summary.ja.json:13` `Game summary for {0}` | `XRL.Core/XRLCore.cs` assembles summary text with runtime name/date values; QudJP owns display translation through game-summary translator/patches. | mid-pipeline game-summary translator template | high | Keep as scoped template, not fixed leaf. |
| `world-mods.ja.json:849` mutation grant description template | `XRL.World.Parts/ModImprovedMutationBase.cs` injects mutation name and level; QudJP owns formatting through `WorldModsTextTranslator`. | description owner translator template | high | Keep as `world-mods` scoped template, not fixed leaf. |

## Acceptance Criteria Status

- High-risk candidates are inventoried with producer route, ownership class,
  confidence, and keep/remove/move/defer decision.
- `message-frame`, ConversationTemplate, HistorySpice/Annals, and messagelog
  fragment candidates are not counted as fixed-leaf dictionary coverage.
- Stale dictionary fragments and placeholder rows removed after owner coverage
  or existing route ownership was proven:
  `ui-messagelog-world.ja.json` no longer carries `You are crippled for␠` or
  the ineffective Breather placeholder row; `ui-messagelog.ja.json` and
  `world-parts.ja.json` no longer carry shield-block prefix/suffix fragments;
  `world-parts.ja.json` no longer carries the stale AbsorbablePsyche exact row;
  and `ui-popup.ja.json` no longer carries ineffective `{0}` popup templates.
- New/confirmed owner coverage:
  `CrippleApplyTranslationPatch` owns the Cripple duration message,
  `MessageFrames/verbs.ja.json` owns Breather cone XDidY messages, existing
  combat/message coverage owns shield-block stagger messages, `EmitMessage`
  owner coverage owns vital-area missile hits, existing and new journal patterns
  own AbsorbablePsyche and GivesRep generated journal text, and narrow popup
  producer helpers own attack-confirmation and refusal messages.
- Follow-up tests are listed below for any future route ownership changes.
- This report explicitly treats the rows as pre-existing dictionary debt
  discovered during issue #493, not as part of the trade/PetEitherOr slice.

## Follow-up Task Breakdown

1. Keep `templates-variable`, `ui-conversation`, `ui-reputation`,
   `ui-game-summary`, and `world-mods` as scoped template assets, but avoid
   including them in any fixed-leaf coverage metric.
2. Split `historyspice-common` component/capture lookup from global exact
   `Translator` loading when the HistorySpice scoped-loader work starts.
3. Build a dedicated scoped HistorySpice/CookingRecipe translator before
   translating generated recipe names or meal-description templates.

## Suggested Verification for Future Behavior Changes

- L1 translator tests for message-pattern, journal-pattern, game-summary,
  world-mod, and HistorySpice component reconstruction helpers.
- L2 owner patch tests for Physics collision/blocked-way, Cripple, popup attack
  confirmation, conversation templates, and shield-block message routing.
- L2G target resolution tests when adding or changing Harmony target methods.
- `just localization-check` and `just translation-token-check` after dictionary
  row removals or relocations.
