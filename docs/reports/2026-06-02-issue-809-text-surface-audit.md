# Issue 809 Conversation, Quest, Cooking, and Effect Text Surface Audit

Date: 2026-06-02

## Scope

Issue #809 is a cross-surface audit follow-up for authored and runtime-expanded
text that is not closed by the static producer lane from PR #807 / issue #719.
It reconciles conversation bodies and choice tags, quest body and journal text,
cooking and meal-effect text, and active-effect/status UI text against the
current deterministic queues and route-family inventories.

Quest titles are intentionally excluded from translation targets. Quest body,
quest step, journal, handler, and dynamic reward text remain separate audit
targets.

## Deterministic Evidence

Commands run from the issue #809 worktree:

```bash
just static-producer-owner-queue 5
just text-construction-surface-queue "$HOME/dev/coq-decompiled_stable" /tmp/issue809-text-construction-inventory.json 80
uv run python scripts/text_construction_surface_policy.py \
  --inventory /tmp/issue809-text-construction-inventory.json \
  --format lanes-json \
  --include valuable \
  --limit 0
just localization-coverage-map-check
```

Observed queue totals:

| Queue | Result |
| --- | ---: |
| Static producer owner queue | 0 families / 0 callsites / 0 text arguments |
| Text-construction inventory families | 17,459 |
| Valuable text-construction queue entries | 2,641 |
| `conversation_routes` entries | 36 |
| `journal_quest_routes` entries | 65 |
| `description_effect_detail` entries | 843 |
| `screen_ui_direct_text` entries | 76 |

The static producer result only closes the `EmitMessage`, `Popup.Show*`, and
`AddPlayerMessage` owner-action queue. It does not close conversation body,
choice-tag, quest-log, cooking-effect, active-effect, or status UI routes.

For the Sifrah attribute popup route, decompiled source shows
`RitualSifrahTokenAttributeSacrifice` receives plain attribute names such as
`"Ego"`, `"Intelligence"`, and `"Willpower"` from
`PsychicCombatSifrah`, `RealityDistortionSifrah`, and `ItemNamingSifrah`, then
constructs `Popup.ShowFail("Your " + Attribute + " is too depleted to do that.")`.
There is no observed markup-wrapped attribute capture in this producer, so the
current plain attribute-name translation helper is the correct implementation
surface.

## Surface Ledger

| Surface group | Current evidence | Classification | First candidate work |
| --- | --- | --- | --- |
| Conversation authored XML | `docs/issue-809-authored-text-inventory.json`, `Conversations.jp.xml`, `HiddenConversations.jp.xml`, asset validators | tracked data-source inventory | Use the inventory rows for targeted authored-text fixes; it records text nodes, runtime expansion terms such as `player.formalAddressTerm`, and choice part names. |
| Conversation choice tags and display text | `ConversationDisplayTextPatchTests.cs`, issue #719 conversation report, text-construction `conversation_routes` | owner route | Add narrow emitted-shape tests for any missing `GetChoiceTagEvent` or `DisplayTextEvent` shape before patching. |
| Quest body and journal routes | `QuestLogTranslationPatch`, `QuestsLineTranslationPatch`, issue #747 route inventory, `QuestUiTranslationPatchTests.cs` | owner route plus data-source asset | Add `QuestsLine.bodyText` regression proving translated quest-log body lines reach the UI. |
| Quest step names | `docs/issue-809-authored-text-inventory.json`, `Quests.jp.xml`, `QuestLog.GetLinesForQuest` source evidence | tracked data-source inventory plus QuestLog owner route | Treat step names separately from quest titles; the tracked inventory counts excluded quest titles, metadata text, step names, and step body text separately. |
| Dynamic quest reward options | `DynamicQuestRewardElement_ChoiceFromPopulation.award` source evidence, `PopupPickOptionTranslationPatchTests.cs` | owner route | Reward option emitted-shape tests cover generated display names, comma-separated option parts, and `&WxN` quantity suffix preservation. |
| Cooking effect descriptions | issue #466 cooking report, `CookingEffectTranslationPatch`, `world-effects-cooking.ja.json` | owner route plus scoped fixed leaves | Continue owner-route slices; do not add broad concrete cooking fragments as fallback leaves. |
| Meal and digestion runtime text | `SingleCallsiteOwnerPopupTranslationPatchTests.cs`, `CookingRuntimeTranslationPatch`, `CampfireDescribeMealTranslationPatch`, `Food.cs`, `Stomach.cs` source evidence | owner route | `FoodConsumptionFrame` emitted-shape tests cover `You eat ... You are now ...` plus embedded `FoodStatus` / `WaterStatus` labels. |
| Active-effect descriptions/details | `docs/active-effect-producer-inventory.json`, `EffectDescriptionPatch`, `EffectDetailsPatch`, `world-effects-status.ja.json`, scoped generated templates | owner route plus scoped generated templates | Keep cooking and non-cooking effect routes separate; add emitted-shape tests before promoting UI embedding gaps. |
| Active-effect/status UI rows | `CharacterEffectLineTranslationPatch`, `StatusScreenBindingOwnerPatchTests.cs`, `PlayerStatusBarProducerTranslationPatchTests.cs` | owner route by screen | Focused UI tests cover character-status effect rows and player-status `FoodWater` owner strings; do not rely on final TMP/UIText sinks. |

## Candidate Tests

1. `Mods/QudJP/Assemblies/QudJP.Tests/L2/QuestUiTranslationPatchTests.cs`
   - Add a `QuestsLine.bodyText` emitted-shape test that proves body lines
     produced by `QuestLog.GetLinesForQuest(... includeTitle: false ...)` are
     translated before reaching `bodyText.SetText`.
   - Keep quest titles untranslated in this test.

2. `Mods/QudJP/Assemblies/QudJP.Tests/L2/SingleCallsiteOwnerPopupTranslationPatchTests.cs`
   - `FoodConsumptionFrame` covers the general eat popup shape from
     `Food.HandleEvent`: `You eat ...` plus embedded food/water status labels.
   - The test proves status label translation on the food owner route without
     treating broad popup fallback as closeout evidence.

3. `Mods/QudJP/Assemblies/QudJP.Tests/L2/ConversationDisplayTextPatchTests.cs`
   - Add any missing quest-handler or water-ritual choice tag variants, such as
     the level-based complete quest tag, with markup preserved.

4. `Mods/QudJP/Assemblies/QudJP.Tests/L2/StatusScreenBindingOwnerPatchTests.cs`
   - Character effect-line tests cover owner-specific status UI rows and verify
     route observability without relying on a generic sink.

## Player.log Boundary

Fresh runtime `Player.log` evidence is not required for this static audit
ledger. Fetch a fresh log from the Tailscale-connected `main-mac-mini` only
when deciding whether a candidate is live-visible, whether an authored asset
merge reaches the modern UI route, or whether a route is merely sink-observed.

Useful strings and route markers to search in fresh logs:

- conversation: `[begin trade]`, `[begin water ritual`, `reputation with`,
  `formalAddressTerm`, `factionaddress`, `friend to our village`;
- quest: `QuestLog`, `QuestsLine`, `Travel to Red Rock`, `Choose a Body`,
  `Choose a reward`, `&Wx`;
- cooking/status: `You eat`, `You are now`, `Hungry`, `Thirsty`,
  `Whip up a meal.`, `Cook from a recipe.`, `well fed`;
- active effects: `ActiveEffectsOwner`, `StatusScreenBinding`,
  `world-effects-generated-templates`, `metabolized effect`.

## Remaining Runtime Boundary

The static and owner-route ledger now has emitted-shape tests for each
implemented issue #809 surface group, including dynamic reward options with
generated item display names and `&WxN` quantity suffixes. True untranslated-zero
is still outside this static audit; it requires fresh runtime evidence for the
specific live-visible flows being closed.

## 2026-06-03 Expanded Closeout

After a fresh `Player.log` triage from `main-mac-mini`, the issue #809 scope was
expanded to quest/HSE/dynamic generated text and other live untranslated
corpus. The follow-up pass implemented these additional closures:

- In `Quests.jp.xml`, localized step display `Name` attributes now carry
  explicit English `ID` attributes so `FinishQuestStep` keeps its runtime
  identity. Quest title `Name` attributes generally remain English runtime
  identities; only the small Issue #809 exception set that had already localized
  quest titles carries explicit English `ID` attributes for quest-manager
  identity lookup.
- `ui-quests.ja.json` carries the same authored step-name leaves as a runtime
  safety net for existing saves and generated quest-log lines.
- `QuestLogTranslationPatch` translates authored step names inside color-wrapped
  quest-log lines, including optional step prefixes.
- `QuestsLineTranslationPatch` translates `bodyText` through the shared
  `QuestLogTranslationPatch` line translator, proving modern quest UI body text
  reaches the owner route.
- `ConversationDisplayTextPatch` handles the paired fixed English site-intro
  frames `But they wouldn't reveal the location.` and `We must know.` that are
  composed around HSE output in dynamic quest site introductions.
- `GospelEvent#gospel` in the Annals candidate artifact is classified as `skip`
  rather than `needs_manual`, because the decompiled producer stores an
  already-generated or caller-provided gospel string instead of a fixed template.
  The null fallback `[NO GOSPEL]` is covered as a journal exact leaf.
- Tinkering description lines observed in the fresh log are translated through
  `DescriptionTextTranslator`: `Credits remaining:`, `Creates:`,
  `Deactivated: Currently without power.`, integrated power systems, and fitted
  cleats save-bonus text.

Focused tests added or extended:

- `test_issue809_authored_text_inventory.py`
- `QuestUiTranslationPatchTests`
- `test_quest_identity_contract.py`
- `ConversationDisplayTextPatchTests`
- `PlayerStatusBarProducerTranslationHelpersTests`
- `StatusScreenBindingOwnerPatchTests`
- `JournalEntryDisplayTextPatchTests`
- `AnnalsPatternsCandidateInventoryTests`
- `DescriptionTextTranslatorTests`

Quality audit note: the remaining shipped-text `俊敏` occurrence was not a
Quickness stat label but a `Triple-jointed` Agility-skill description. It was
changed to `敏捷系スキル` to avoid conflicting with the user-requested
Quickness terminology while preserving the source meaning.
