# HistoricStringExpander Generated Text Owner Plan

Date: 2026-05-17

## Scope

This report records the current owner-route plan for generated
`HistoricStringExpander` and HistorySpice prose. It is based on decompiled source
inspection under `/Users/toarupen/dev/coq-decompiled_stable/` and the current
QudJP patch surface.

The core rule is unchanged: do not translate at the generic
`HistoricStringExpander.ExpandString` boundary. The expander is used for both
visible prose and symbolic HistorySpice path construction, so expander-wide
postfix translation can corrupt world generation. The disabled
`HistoricStringExpanderPatch` must remain disabled.

## Evidence

Commands and inputs used in this pass:

```bash
just annals-pattern-preview \
  /Users/toarupen/dev/coq-decompiled_stable/XRL.Annals \
  "*.cs" \
  /tmp/qudjp-annals-all-candidates.json
uv run python scripts/validate_candidate_schema.py \
  /tmp/qudjp-annals-all-candidates.json
uv run python scripts/text_construction_surface_policy.py \
  --inventory /tmp/roslyn-text-construction-inventory-hse.json \
  --format json \
  --include valuable \
  --limit 0
```

Annals preview state from the current decompiled source:

| Source | Candidates | Accepted/extractable | Needs manual |
| --- | ---: | ---: | ---: |
| Tracked artifact before this pass | 67 | 66 | 0 |
| Current tracked artifact | 135 | 132 accepted, 2 skipped | 1 |

The remaining manual row is:

| Source | Reason | Decision |
| --- | --- | --- |
| `GospelEvent.cs` | `Gospel ?? "[NO GOSPEL]"` coalesce expression | Defer as pass-through/observability, not a fixed prose frame. |

## Owner Lanes

| Priority | Lane | Producer examples | Current decision |
| ---: | --- | --- | --- |
| 1 | Annals persisted narrative | `XRL.Annals/*Generate()` setting `gospel` or `tombInscription` | Extend `scripts/_artifacts/annals/candidates_pending.json`, merge into `annals-patterns.ja.json`, and keep the existing `HistoricNarrativeTextTranslator` route. No new Harmony owner is needed for this slice. |
| 2 | Cooking recipe, meal, and cookbook prose | `CookingRecipe.GenerateRecipeName`, `CookingRecipe.GetDisplayName`, `Cookbook.GenerateCookbook`, `Campfire.CookFromIngredients`, `Campfire.DescribeMeal`, `Campfire.RollIngredients` | `Campfire.DescribeMeal` cook-template frames are covered by a scoped owner postfix. Generated recipe display names are covered at `CookingRecipe.GetDisplayName()` using component reconstruction while suppressing translation during `GenerateRecipeTile` so tile matching still sees the English persisted `DisplayName`. Generated cookbook display-name frames are covered after `Cookbook.GenerateCookbook()` composes the final `Render.DisplayName`, preserving focus ingredient and Markov-title captures. Generated fallback ingredient fragments from `Campfire.RollIngredients` are covered by a scoped owner postfix before the meal-description frame is composed. Do not count cooking `world-gospels` leaves as visible-prose coverage. |
| 3 | Dynamic quest conversations, quest text, and item names | `DynamicQuestConversationHelper`, `DynamicQuestSignpostConversation`, `VillageDynamicQuestContext.getQuestItemNameMutation`, `FindASpecificItem*`, `FindASpecificSite*`, `InteractWithAnObject*` | `DynamicQuestConversationHelper.appendQuestCompletionSequence` is covered by a producer-scoped transpiler that translates the three expanded completion/start-work frames and a prefix that translates explicit generated completion/incomplete choice text. `DynamicQuestConversationHelper.fabricateIntroAcceptChoice`, `fabricateIntroRejectChoice`, and `fabricateIntroAdditionalChoice` are covered by a scoped prefix for finite explicit quest intro choice lines. `DynamicQuestSignpostConversation.HandleEvent` is covered by a producer-scoped transpiler for quest-work intro choices and signpost target prefixes. `VillageDynamicQuestContext.getQuestItemNameMutation(string)` is covered by a producer return-value postfix for the finite generated item-name mutation templates, reconstructing known sacred-prefix and `of the *adj* *noun*` forms while preserving item-name captures. `FindASpecificItem*`, `FindASpecificSite*`, and `InteractWithAnObject*` quest constructor conversations are covered by a producer-scoped transpiler for finite no-capture prompt, personal task, personal retrieve reward, personal rumor/need, sacred intro/after-learning/lost/recover/taken-to, reward-tail, travelers/records site bodies, site-boon, site-treasure, site find/direction, holy-item, and strange-plan frames. The same three quest constructors are also covered at the generated `Quest` return value for finite quest names, step names, and step text (`Find`, `Return`, `Locate`, `Travel`, helping, sanctity, and quest-verb frames), preserving generated object/site/giver/location captures. |
| 4 | Village wall and terrain descriptions | `VillageBase.getAVillageWall`, `VillageBase.getAVillageCanvas`, `VillageCodaBase.getAVillageWall`, `VillageCodaBase.getAVillageCanvas`, `VillageTerrain.VillageReveal` | Wall/canvas frames are covered by `VillageWallDescriptionTranslationPatch` at the producer return value by translating `Description.Short`. `VillageTerrain.VillageReveal` is covered by `VillageTerrainRevealDescriptionTranslationPatch`, guarded to successful reveal events and translating the generated `Description.Short` six-frame surface plus finite terrain fragments. Existing description-route matching is not counted as owner proof. |
| 4b | Village leader and pet conversations | `Village/VillageCoda` warden/mayor/pet conversation assembly through `ConversationsAPI.addSimpleConversationToObject`, `VillageBase.AddVillagerConversation`, and `VillageCodaBase.AddVillagerConversation` | Warden and mayor intro frames from `spice.villages.warden.introDialog` / `spice.villages.mayor.introDialog` are covered at the conversation-owner boundary. Pet origin-story answers from `spice.villages.pet.originStory` and the generated "Why is/are there..." pet question are also covered there. These patches translate only the finite leader/pet question/origin-story shapes and preserve village names, pet names, and activity/ore/sacred/profane captures. |
| 4a | Sultan-region reveal descriptions | `SultanRegion.FireEvent(\"SultanReveal\")` | Sultan-region reveal `Description.Short` frames are covered by `SultanRegionRevealDescriptionTranslationPatch`, guarded to successful `SultanReveal` events and translating the two finite outer frames while preserving terrain and organizing-principle captures. |
| 5 | Tombstone, urn, and crypt plaque inscriptions | `Tombstone.GenerateTombstone`, `RachelsTombstone.GenerateTombstone`, `EaterUrn.GenerateUrn`, `EaterCryptPlaque.GeneratePlaque` | Tombstone/Rachel tombstone/urn intro frames are covered by `MemorialInscriptionIntroTranslationPatch`. `Tombstone.GenerateTombstone` generated death causes and Rachel's fixed glotrot cause are covered by `TombstoneDeathCauseTranslationPatch` at the producer `StringFormat.ClipText` boundary, with custom inscriptions left unchanged. Crypt plaque intro/title/cognomen and finite `familyWords` shell fragments are covered by `EaterCryptPlaqueTextTranslationPatch` at `GeneratePlaque` `ExpandString` return sites. The crypt translator normalizes seeded `*markovSeed:*` bodies to the null-seed `*shortMarkov*` marker, so urn eulogies and crypt plaque Markov bodies are covered by the existing `MarkovCorpusTranslationPatch` vanilla-corpus replacement, proven against `GenerateShortSentence(data, null, 12)`. |
| 6 | Relic, heirloom, and generated item names | `RelicGenerator`, `ItemNaming`, `RandomAltarBaetyl`, `Faction` reward routes | `RelicGenerator.GenerateRelicName` HistorySpice names are covered by a producer postfix that reconstructs finite generated name shapes from components, including region names where the generated item/title side is translated and the region proper name is preserved, and clears English article ownership. `AfterPseudoRelicGeneratedEvent.Send` covers faction heirloom, baetyl reward, and item-naming bestowal pseudo-relic proper names after generation. `ItemNaming.GenerateRelicStyleName` covers player-visible relic-style candidate names before color selection/storage. `RelicGenerator.GenerateRelic` description addenda are covered for finite stamped/engraving frames. Do not exact-leaf generated relic names. |
| 6b | Psychic hunter and extradimensional names | `PsychicHunterSystem`, `DimensionManager` | Psychic hunter generated title fragments are covered inside the four psychic-hunter producer methods by translating `HistoricStringExpander` fragments and replacing `Titles.AddTitle` calls with an owner-scoped translated title helper. Persistent extradimensional faction and dimension names are covered at `DimensionManager.InitializeFaction()` and `GenerateMoreDimensions()`, with numeric cult-symbol expansions intentionally passed through for `int.Parse`. |
| 6c | Generated settlement farm names | `SettlementNames.GenerateFarmName` | Pig/starapple farm and shire names are covered at the `GenerateFarmName(History,string)` return value, reconstructing finite secluded-farm, owner-kind, kind-of-owner, and prefix-kind frames while preserving generated owner/cognomen captures. |
| 7 | Misc finite generated description addenda | `BroadcastPowerReceiver.HandleEvent(GetShortDescriptionEvent)` | Satellite broadcast-power occlusion reasons are covered at the producer `ExpandString` call by `BroadcastPowerOcclusionReasonTranslationPatch`. |
| 8 | Misc finite generated book text | `MerchantRevealer.GenerateMerchantLocation` | Merchant advertisement book body frames are covered at the producer `ExpandString` call by `MerchantAdvertisementTextTranslationPatch`, preserving generated workshop and location captures. |
| 9 | Misc finite generated inscriptions | `TempleDedicationPlaque.GenerateInscription` | The temple dedication fixed inscription frame is covered at the producer return value by `TempleDedicationPlaqueInscriptionTranslationPatch`, including scoped component reconstruction for generated egregore and era captures where their component words are known. |
| 10 | NameStyle XML templatevars | `NameStyle.Generate` for Godhed hero honorific/epithet/title template variables | The remaining `spice.adjectives.!random`, `spice.nouns.!random`, and `spice.nouns.!random.pluralize` templatevars in `Naming.jp.xml` were replaced with finite Japanese value lists, so `NameStyle.Generate` no longer calls HSE for the localized Godhed templates. Broader visible name consumers remain covered by localized `Naming.jp.xml` templates rather than a generic `NameStyle.Generate` postfix. |

## Acceptance Criteria

- `HistoricStringExpanderPatch.TargetMethods()` remains disabled.
- Each closed family has producer or mid-pipeline owner proof, not only a final
  popup/UI sink.
- Annals closure is measured through accepted candidate coverage and merged
  `route=annals` patterns.
- Non-Annals generated text is split by owner lane before implementation.
- Tests for new owner translators cover observed examples, non-observed
  variants, unknown fallback, empty/direct-marker input, color markup, and
  placeholder preservation where applicable.

## Verification Plan

For the Annals slice:

```bash
uv run python scripts/validate_candidate_schema.py \
  scripts/_artifacts/annals/candidates_pending.json
uv run python scripts/merge_annals_patterns.py \
  scripts/_artifacts/annals/candidates_pending.json
just test-l1
just localization-check
just translation-token-check
```

For later owner-translator slices, add focused L1 translator tests, L2 owner
handoff tests, and L2G target-resolution tests before counting the lane closed.

Current non-Annals owner slices covered in this pass:

- `Campfire.DescribeMeal(IReadOnlyList<GameObject>)` cook-template final frames.
- `Campfire.RollIngredients(int, IReadOnlyList<GameObject>, Random)` generated
  fallback ingredient fragments such as measured, article, and "some" forms
  before they are composed into the meal description frame.
- `CookingRecipe.GetDisplayName()` generated recipe display names, with
  `CookingRecipe.GenerateRecipeTile(CookingRecipe)` suppression so the
  persisted generated English `DisplayName` still drives tile matching.
- `Cookbook.GenerateCookbook()` generated cookbook display-name frames,
  preserving focus ingredient, noun/adjective, and Markov-title captures.
- `RelicGenerator.GenerateRelicName(...)` generated relic names for finite
  component-reconstructable HistorySpice name shapes, including region-name
  forms that translate the generated item/title side while preserving the
  region proper name.
- `AfterPseudoRelicGeneratedEvent.Send(...)` pseudo-relic proper names generated
  by faction heirlooms, baetyl reward items, and item-naming bestowals.
- `ItemNaming.GenerateRelicStyleName(...)` player-visible relic-style generated
  name candidates before color selection and final naming.
- `RelicGenerator.GenerateRelic(...)` finite generated description addenda for
  stamped element imagery and venerated/disparaged faction engravings.
- `VillageBase.getAVillageWall/getAVillageCanvas` and
  `VillageCodaBase.getAVillageWall/getAVillageCanvas` generated wall/canvas
  `Description.Short` frames.
- `VillageTerrain.FireEvent(Event)` successful `VillageReveal` generated
  `Description.Short` frames and finite terrain fragments.
- `ConversationsAPI.addSimpleConversationToObject(...)`,
  `VillageBase.AddVillagerConversation(...)`, and
  `VillageCodaBase.AddVillagerConversation(...)` generated village warden,
  mayor, and pet conversation frames, preserving village names, pet names, and
  activity/ore/sacred/profane captures.
- `SultanRegion.FireEvent(Event)` successful `SultanReveal` generated
  `Description.Short` frames, preserving terrain and organizing-principle
  captures.
- `DynamicQuestConversationHelper.appendQuestCompletionSequence(...)`
  `HistoricStringExpander.ExpandString` completion/start-work frames.
- `DynamicQuestConversationHelper.appendQuestCompletionSequence(...)`
  explicit generated completion and incomplete choice text, plus
  `fabricateIntroAcceptChoice(...)`, `fabricateIntroRejectChoice(...)`, and
  `fabricateIntroAdditionalChoice(...)` explicit generated intro choice text.
- `DynamicQuestSignpostConversation.HandleEvent(BeforeConversationEvent)`
  generated quest-work intro choices and signpost target prefixes.
- `VillageDynamicQuestContext.getQuestItemNameMutation(string)` generated
  dynamic quest item-name mutation templates, reconstructing known
  sacred-prefix and `of the *adj* *noun*` forms while preserving item-name
  captures.
- `FindASpecificItemDynamicQuestTemplate_FabricateQuestGiver.addQuestConversationToGiver(...)`,
  `FindASpecificSiteDynamicQuestTemplate_FabricateQuestGiver.addQuestConversationToGiver(...)`,
  and `InteractWithAnObjectDynamicQuestTemplate_FabricateQuestGiver.addQuestConversationToGiver(...)`
  finite no-capture prompt, personal task, personal rumor/need, sacred
  intro/after-learning/recover prompt/lost-item/`takenTo` destination, personal
  retrieve reward, reward-tail, travelers/records site bodies, site-boon,
  site-treasure, site find/direction, holy-item, and strange-plan frames from
  quest constructor conversations.
- `FindASpecificItemDynamicQuestTemplate_FabricateQuestGiver.fabricateFindASpecificItemQuest(...)`,
  `FindASpecificSiteDynamicQuestTemplate_FabricateQuestGiver.fabricateFindASpecificSiteQuest(...)`,
  and `InteractWithAnObjectDynamicQuestTemplate_FabricateQuestGiver.fabricateInteractWithAnObjectQuest(...)`
  generated quest names, step names, and step text for finite `Find`,
  `Return`, `Locate`, `Travel`, helping, sanctity, and quest-verb frames.
- `Tombstone.GenerateTombstone`, `RachelsTombstone.GenerateTombstone`, and
  `EaterUrn.GenerateUrn` finite memorial intro frames.
- `Tombstone.GenerateTombstone` generated death-cause frames and
  `RachelsTombstone.GenerateTombstone` fixed glotrot cause, with preauthored
  custom tombstone inscriptions intentionally passed through.
- `EaterCryptPlaque.GeneratePlaque` finite crypt intro, family title, and
  family cognomen fragments, plus finite `familyWords` shell fragments. Seeded
  `*markovSeed:*` family words are normalized to `*shortMarkov*` before the
  upstream Markov replacement so they use the proven Japanese null-seed corpus
  route instead of English seed words.
- `EaterUrn.GenerateUrn` Markov eulogies and `EaterCryptPlaque.GeneratePlaque`
  `*shortMarkov*` null-seed bodies through the existing
  `MarkovCorpusTranslationPatch` replacement of the vanilla `LibraryCorpus.json`
  with the Japanese corpus.
- `BroadcastPowerReceiver.HandleEvent(GetShortDescriptionEvent)` generated
  satellite broadcast-power occlusion reasons.
- `MerchantRevealer.GenerateMerchantLocation()` generated merchant
  advertisement book body frames, preserving generated workshop and location
  captures.
- `TempleDedicationPlaque.GenerateInscription()` generated temple dedication
  frame, including known egregore and era component reconstruction.
- `Naming.jp.xml` Godhed hero honorific/epithet/title template variables, with
  finite Japanese value lists replacing the remaining HistorySpice templatevar
  leaves used by `NameStyle.Generate`.
- `PsychicHunterSystem.CreateSeekerHunters(...)`,
  `CreateExtradimensionalSoloHunters(...)`, `CreateExtradimensionalSoloDeviant(...)`,
  and `CreateExtradimensionalCultHunters(...)` generated psychic hunter title
  fragments and title additions.
- `DimensionManager.InitializeFaction()` generated psychic faction cult forms
  and dimension names, plus `DimensionManager.GenerateMoreDimensions()`
  generated extra-dimension names, leaving numeric cult-symbol expansions
  untouched.
- `SettlementNames.GenerateFarmName(History,string)` generated pig/starapple
  farm and shire names.

Residual follow-up:

- `GospelEvent.cs` remains a pass-through/coalesce wrapper rather than a fixed
  generated prose frame; track only if a concrete upstream English gospel source
  is identified.
- `TextFilters.Angry/Lallated` still call HSE for speech-noise filters. Keep
  this as runtime-evidence follow-up issue
  [#726](https://github.com/ToaruPen/coq-japanese_stable/issues/726) rather
  than a blocker for the generated HistorySpice prose closure.
