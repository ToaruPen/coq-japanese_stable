# Issue 719 Conversation Text-Construction Routes

Date: 2026-05-16

## Scope

This report closes the issue-719 conversation slice for decompiled Caves of Qud
`1.0.4` text constructions. Conversation routes are tracked separately from the
static producer closure lane for `EmitMessage`, `Popup.Show*`, and
`AddPlayerMessage`.

The final scanner rule only treats conversation event receivers as
`ConversationTextAppend` / `ConversationTextReplace` when the receiver is
exactly `E.Text`. The earlier `ReclamationSystem.SpawnNephilim` candidate was a
false positive: it mutates a quest step `Text` property, not conversation event
display text.

## Evidence

Commands:

```bash
just static-producer-owner-queue 10
just text-construction-surface-queue /Users/toarupen/dev/coq-decompiled_stable /tmp/issue719-text-construction-inventory-final.json 10
uv run python scripts/text_construction_surface_policy.py \
  --inventory /tmp/issue719-text-construction-inventory-final.json \
  --format lanes-json \
  --include valuable \
  --limit 0
uv run python scripts/text_construction_surface_policy.py \
  --inventory /tmp/issue719-text-construction-inventory-final.json \
  --format json \
  --include valuable \
  --limit 0
just localization-coverage-map-check
```

Observed results:

- `just static-producer-owner-queue 10`: `0 families, 0 callsites, 0 text arguments across 0 source files`.
- `text-construction-surface-queue`: `2,641` valuable entries after adding the
  `conversation_routes` lane.
- `conversation_routes`: `36` entries, `89` text constructions.
- `conversation_routes` closure status: `28 covered_by_owner_route`,
  `6 partial_coverage`, `2 runtime_required`, `0 action_required`.
- `just localization-coverage-map-check`: passed.

## Implemented Owner Route

`ConversationDisplayTextPatch` now owns the conversation display route for this
slice:

- strips plain and colored trailing choice action/cost tags emitted through
  `GetChoiceTagEvent`, including nested color markup and trailing newlines;
- translates water ritual reputation summaries appended by `DisplayTextEvent`;
- translates mound countdown fragments inserted by `MoundContext`;
- translates quest signpost direction/conjunction fragments without touching
  unrelated ordinary `or` text;
- translates the fixed water ritual tinkering `Item mod` label;
- translates the default hermit oath address fallback;
- translates `I seek <skill>.` prompts while leaving generated skill names to
  exact dictionary/display-name routes.

## Conversation Route Closure Queue

| Status | Family | Surface | Count |
| --- | --- | --- | ---: |
| covered_by_owner_route | `WaterRitualLearnSkill.HandleEvent(GetChoiceTagEvent)` | `ConversationChoiceTag` | 10 |
| covered_by_owner_route | `MoundContext.HandleEvent(PrepareTextEvent)` | `ConversationTextReplace` | 8 |
| partial_coverage | `QuestSignpost.HandleEvent(PrepareTextEvent)` | `ConversationTextReplace` | 8 |
| covered_by_owner_route | `WaterRitual.HandleEvent(DisplayTextEvent)` | `ConversationTextAppend` | 7 |
| covered_by_owner_route | `RequireReputation.HandleEvent(GetChoiceTagEvent)` | `ConversationChoiceTag` | 6 |
| covered_by_owner_route | `WaterRitualSellSecret.HandleEvent(GetChoiceTagEvent)` | `ConversationChoiceTag` | 6 |
| covered_by_owner_route | `QuestHandler.HandleEvent(GetChoiceTagEvent)` | `ConversationChoiceTag` | 5 |
| partial_coverage | `WaterRitualTinkeringRecipe.HandleEvent(PrepareTextEvent)` | `ConversationTextReplace` | 4 |
| partial_coverage | `WaterRitualHermitOath.HandleEvent(PrepareTextEvent)` | `ConversationTextReplace` | 3 |
| covered_by_owner_route | `EndGame.HandleEvent(GetChoiceTagEvent)` | `ConversationChoiceTag` | 2 |
| runtime_required | `GlotrotFilter.HandleEvent(PrepareTextEvent)` | `ConversationTextAppend` | 2 |
| runtime_required | `InsertRandomBookLine.HandleEvent(PrepareTextEvent)` | `ConversationTextAppend` | 2 |
| covered_by_owner_route | `WaterRitualBegin.HandleEvent(GetChoiceTagEvent)` | `ConversationChoiceTag` | 2 |
| covered_by_owner_route | `WaterRitualHermitOath.HandleEvent(GetChoiceTagEvent)` | `ConversationChoiceTag` | 2 |
| covered_by_owner_route | `AddSlynthCandidate.HandleEvent(GetChoiceTagEvent)` | `ConversationChoiceTag` | 1 |
| covered_by_owner_route | `BuildGolem.HandleEvent(GetChoiceTagEvent)` | `ConversationChoiceTag` | 1 |
| covered_by_owner_route | `CrossIntoBrightsheol.HandleEvent(GetChoiceTagEvent)` | `ConversationChoiceTag` | 1 |
| covered_by_owner_route | `GiveArtifact.HandleEvent(GetChoiceTagEvent)` | `ConversationChoiceTag` | 1 |
| covered_by_owner_route | `GiveReshephSecret.HandleEvent(GetChoiceTagEvent)` | `ConversationChoiceTag` | 1 |
| partial_coverage | `KithAndKinExclusion.HandleEvent(PrepareTextEvent)` | `ConversationTextReplace` | 1 |
| partial_coverage | `KithAndKinMotive.HandleEvent(PrepareTextEvent)` | `ConversationTextReplace` | 1 |
| covered_by_owner_route | `LibrarianGiveBook.HandleEvent(GetChoiceTagEvent)` | `ConversationChoiceTag` | 1 |
| covered_by_owner_route | `PaxInfectLimb.HandleEvent(GetChoiceTagEvent)` | `ConversationChoiceTag` | 1 |
| covered_by_owner_route | `StartFight.HandleEvent(GetChoiceTagEvent)` | `ConversationChoiceTag` | 1 |
| covered_by_owner_route | `Trade.HandleEvent(GetChoiceTagEvent)` | `ConversationChoiceTag` | 1 |
| covered_by_owner_route | `WaterRitualBuyItem.HandleEvent(GetChoiceTagEvent)` | `ConversationChoiceTag` | 1 |
| covered_by_owner_route | `WaterRitualBuySecret.HandleEvent(GetChoiceTagEvent)` | `ConversationChoiceTag` | 1 |
| covered_by_owner_route | `WaterRitualCookingRecipe.HandleEvent(GetChoiceTagEvent)` | `ConversationChoiceTag` | 1 |
| covered_by_owner_route | `WaterRitualFungusColonize.HandleEvent(GetChoiceTagEvent)` | `ConversationChoiceTag` | 1 |
| covered_by_owner_route | `WaterRitualGainMutation.HandleEvent(GetChoiceTagEvent)` | `ConversationChoiceTag` | 1 |
| covered_by_owner_route | `WaterRitualJoinParty.HandleEvent(GetChoiceTagEvent)` | `ConversationChoiceTag` | 1 |
| partial_coverage | `WaterRitualLearnSkill.HandleEvent(PrepareTextEvent)` | `ConversationTextReplace` | 1 |
| covered_by_owner_route | `WaterRitualNephilimPacify.HandleEvent(GetChoiceTagEvent)` | `ConversationChoiceTag` | 1 |
| covered_by_owner_route | `WaterRitualRandomMutation.HandleEvent(GetChoiceTagEvent)` | `ConversationChoiceTag` | 1 |
| covered_by_owner_route | `WaterRitualSkillPoint.HandleEvent(GetChoiceTagEvent)` | `ConversationChoiceTag` | 1 |
| covered_by_owner_route | `WaterRitualTinkeringRecipe.HandleEvent(GetChoiceTagEvent)` | `ConversationChoiceTag` | 1 |

## Residual Runtime/Data-Source Ownership

The remaining non-covered statuses are intentional and no longer
`action_required` for this issue:

- `QuestSignpost`: direction and conjunction fragments are translated by the
  owner route; generated questgiver names and landmark text remain
  display-name/data-source routes.
- `WaterRitualTinkeringRecipe`: the fixed `Item mod` label is translated; item
  and recipe names remain object/tinkering display-name routes.
- `WaterRitualHermitOath`: the default `hermit` fallback is translated;
  speaker-specific `HermitOathAddressAs` values remain data-source/runtime
  owned.
- `WaterRitualLearnSkill`: the fixed prompt shape is translated; generated
  skill names remain exact dictionary/display-name routes.
- `KithAndKinExclusion` / `KithAndKinMotive`: replacements come from
  Kith-and-Kin game-state or journal clue data.
- `GlotrotFilter`: intentionally rewrites conversation text into disease
  speech at runtime.
- `InsertRandomBookLine`: inserted text must be verified through book/data
  localization runtime evidence.

## Tests Added

- `ConversationDisplayTextPatchTests` covers colored action tags, water ritual
  reputation summary translation, mound countdown translation, quest signpost
  directions, water ritual tinkering labels, hermit oath fallback, and
  initiatory skill prompts.
- `test_roslyn_text_construction_inventory.py` covers
  `ConversationChoiceTag`, exact `E.Text` append/replace receiver detection, and
  the non-conversation quest step `Text.Replace` false-positive guard.
- `test_text_construction_surface_policy.py` covers the `conversation_routes`
  lane and issue-719 closure statuses.
