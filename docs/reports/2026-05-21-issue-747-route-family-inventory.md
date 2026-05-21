# Issue 747 Route-Family Inventory

Date: 2026-05-21

## Scope

This report closes the issue-747 inventory pass across three generated or composed runtime text families:

- quest / journal / generated quest text
- sultan-history / Annals / HistorySpice text
- skill-originated message-log and popup text

The current runtime log does not contain unresolved untranslated triage rows. This report uses `Player.log` as route evidence only; the closure claim comes from static owner-route inventory plus focused tests.

## Evidence Used

Runtime evidence came from `main-mac-mini`:

```bash
ssh main-mac-mini 'stat -f "%Sm %N" -t "%Y-%m-%d %H:%M:%S %z" "$HOME/Library/Logs/Freehold Games/CavesOfQud/Player.log"'
ssh main-mac-mini 'cat "$HOME/Library/Logs/Freehold Games/CavesOfQud/Player.log"' > /tmp/qudjp-issue-747-Player.log
uv run python scripts/triage_untranslated.py --log /tmp/qudjp-issue-747-Player.log
```

Observed runtime evidence:

- `Player.log` modified at `2026-05-21 20:42:03 +0900`.
- `triage_untranslated.py`: `total=0`, `unresolved=0`.
- The log loads `Quests.xml`, `Skills.xml`, `HistorySpice.json`, and `325` `JournalPatternTranslator` patterns.
- The log reports: `QudJP.Patches.HistoricStringExpanderPatch.TargetMethods returned no target methods`.

Static evidence was regenerated locally:

```bash
just localization-coverage-map-check
just text-construction-surface-queue "$HOME/dev/coq-decompiled_stable"   /tmp/issue747-all-text-construction-inventory.json 20
uv run python scripts/text_construction_surface_policy.py   --inventory /tmp/issue747-all-text-construction-inventory.json   --format lanes-json   --include valuable   --limit 0 > /tmp/issue747-lanes.json
just static-producer-preview
uv run python scripts/static_producer_closure.py   --inventory /tmp/qudjp-static-producer-inventory.json   --format json   --limit 0 > /tmp/issue747-static-producer-closure.json
```

Observed static counts:

| Lane | Entries | Text constructions | Status counts |
| --- | ---: | ---: | --- |
| `journal_quest_routes` | 65 | 1,277 | `covered_by_owner_route=65` |
| `history_generated_text` | 78 | 2,351 | `covered_by_owner_route=76`, `runtime_required=2` |
| `combat_message_frame_does` | 500 | 6,518 | `action_required=446`, `covered_by_owner_route=47`, `likely_true_gap=2`, `partial_coverage=5` |
| `producer_message_popup` | 687 | 5,447 | `action_required=650`, `covered_by_owner_route=30`, `likely_true_gap=1`, `partial_coverage=5`, `runtime_required=1` |
| `screen_ui_direct_text` | 76 | 595 | `action_required=73`, `covered_by_owner_route=2`, `partial_coverage=1` |

The exhaustive issue-747 static row list is recorded in `docs/reports/2026-05-21-issue-747-static-analysis-inventory.md`. It includes all `65` quest/journal rows, all `78` history-generated rows, and `72` skill-originated message-log / popup rows selected from skill-like producers.

The static producer preview produced `2208` callsites, `1012` families, and `2238` text arguments. Running `static_producer_closure.py` over that preview returned `family_count=0`, so the issue-747 remaining queue is the text-construction route-family inventory above rather than the tracked static-producer owner queue.

## Quest / Journal / Generated Quest Text

Current classification:

- Exhaustive static appendix status: `65` rows total, `covered_by_owner_route=65`.
- Journal and generated quest text is owned by `JournalAccomplishmentAddTranslationPatch`, `JournalMapNoteAddTranslationPatch`, `JournalObservationAddTranslationPatch`, `JournalEntryDisplayTextPatch`, and `DynamicQuestGeneratedQuestTextTranslationPatch`.
- The closure evidence is limited to the exact family IDs in `ISSUE747_JOURNAL_QUEST_REVIEWED_FAMILY_IDS`. Mixed JournalAPI/Popup/AddPlayerMessage rows also carry popup/message sink-owner evidence for the same reviewed family.

Focused test coverage in this pass:

- `DynamicQuestGeneratedQuestTextTranslatorTests` includes `Locate the rusted relic at {{|the spindle}}.` to prove the generated item and dynamic-location capture in the source-backed `Locate <item> at <target>.` shape.
- `scripts/tests/test_text_construction_surface_policy.py` asserts issue-747 journal rows are closed only when their exact family ID is reviewed; an unlisted row in the same lane remains `action_required`.

## Sultan History / Annals / HistorySpice

Current classification:

- Exhaustive static appendix status: `78` rows total, `covered_by_owner_route=76`, `runtime_required=2`.
- The two `runtime_required` rows are `TextFilters.Angry` and `TextFilters.Lallated`; static probing shows they are speech/status text filters rather than sultan annal producers. They remain issue-726 runtime-evidence routes, not hidden untranslated sultan-history rows.
- `VillageSurface.CheckReveal` is closed by JournalAPI visit-accomplishment owner patterns; its `RevealString` popup is preauthored map-note data, not generated English text-construction.

`HistoricStringExpanderPatch` remains disabled as a broad runtime owner. Its `TargetMethods` intentionally returns no targets, and the runtime log confirms that the patch is skipped. Narrower owner routes cover visible surfaces instead: Journal entry storage/display, Annals patterns, generated name translators, shrine wrapper decomposition, village terrain/reveal descriptions, memorial plaques, relic/name-style routes, and dynamic quest HSE producers.

## Skill-Originated Message Log / Popup Text

Current classification:

- Exhaustive static appendix status for skill-originated message-log / popup rows: `72` rows total, `covered_by_owner_route=72`.
- Policy closure is limited to the exact `72` family IDs listed in `ISSUE747_SKILL_REVIEWED_FAMILY_IDS`; unlisted `XRL.World.Parts.Skill/*` rows still remain action items.
- Each reviewed skill row now records its own family ID in closure evidence and selects route-specific evidence instead of reusing one blanket skill evidence list.
- Message-frame traffic is covered by owner scopes plus `MessageQueueSemanticPipeline` / `MessageFrameTranslator` dictionaries.
- Source-backed skill popups and failure messages are covered by owner-specific patches, not by generic fallback claims.
- Fixed picker titles and fixed stable failure leaves were added only where the upstream text is a stable leaf.

Focused implementation and tests added in this pass:

- `CombatSkillMessageTranslationPatch` translates the source-backed `shield slam` captured slot while leaving unrelated captures unchanged.
- `MessageFrames/verbs.ja.json` now covers issue-747 skill message-frame verbs and templates such as `dive`, `leap`, `juke`, `hook`, `make camp`, `hobble`, `shame`, `work`, `reel`, and repair critical-failure frames.
- `SingleCallsiteOwnerPopupTranslationPatch` now owns source-backed popups/failures for `Tactics_DeathFromAbove`, `Tactics_Charge`, `Tactics_Juke`, `Axe_HookAndDrag`, `Cudgel_Slam`, `Persuasion_Proselytize`, and `Tinkering_Tinker1` recharge routes.
- `SurvivalCampAttemptCampPopupTranslationPatch` now covers the non-navigation `Survival_Camp.AttemptCamp` failures.
- `PhysicAmputateLimbTranslationPatch` now covers field-amputation popup failures, including hostile-nearby, missing weapon, unreachable target, refusal, no limbs, no-reason target refusal, self-amputation refusal, and limb-holding-item cases. Stable body-part captures such as `left arm`, `right hand`, and possessive `limbs` fragments are translated, while object captures use the shared display-name capture route with leading English articles stripped.
- `RepairTranslationPatch` now covers Tinkering Repair reach/phase failures and critical failure popups.
- `PopupAskNumberTranslationPatch` now gives active owner routes first chance to translate ask-number prompts, which covers Tinkering recharge amount prompts.
- `DirectionPhraseTranslator` now covers bare compass stems used by Juke message-frame placeholders.
- `ui-popup.ja.json` adds fixed stable leaves for skill picker titles and source-backed fixed failures such as Death From Above, Juke, Slam, field amputation target/limb selection, Proselytize target selection, item charging, conk, and bow/rifle equipment failure.

## Acceptance Ledger

| Criterion | Status |
| --- | --- |
| One route-family report covers quest/journal, sultan-history, and skill message-log/popup text. | Satisfied by this report. |
| Exhaustive static appendix lists every issue-747 scoped row. | Satisfied by `2026-05-21-issue-747-static-analysis-inventory.md`. |
| Quest/journal rows are closed as owner-route coverage. | Satisfied: `covered_by_owner_route=65`. |
| Skill-originated message-log / popup rows are closed as owner-route coverage. | Satisfied: `covered_by_owner_route=72`. |
| Sultan history rows are closed or explicitly classified with non-sultan runtime deferral. | Satisfied: `covered_by_owner_route=76`, `runtime_required=2` TextFilters rows. |
| Skills and Powers UI coverage is distinguished from skill/action message-log coverage. | Satisfied in the skill section and appendix scope. |
| `HistoricStringExpanderPatch` status is explicit. | Satisfied; it remains disabled and is not used as broad closure evidence. |
| No synthetic fallback examples are used as proof. | Satisfied; examples and tests are source-backed or fixed leaves. |

## Verification

Static closeout recount:

```bash
uv run python scripts/text_construction_surface_policy.py --inventory /tmp/issue747-all-text-construction-inventory.json --format json --include valuable --limit 0 > /tmp/issue747-policy-current.json
```

Observed issue-747 scoped counts from the regenerated policy JSON:

- `journal_quest_routes`: `65` rows, `covered_by_owner_route=65`
- `history_generated_text`: `78` rows, `covered_by_owner_route=76`, `runtime_required=2`
- skill-originated message-log / popup rows: `72` rows, `covered_by_owner_route=72`

Focused checks run during this pass:

```bash
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj   --filter "FullyQualifiedName~SingleCallsiteOwnerPopupTranslationPatchTests|FullyQualifiedName~RepairTranslationPatchTests|FullyQualifiedName~PopupAskNumberTranslationPatchTests|FullyQualifiedName~TargetMethodResolutionTests.OwnerProducerTargetMethods"

uv run pytest scripts/tests/test_text_construction_surface_policy.py
```

Full local gates run during this pass:

```bash
just build
just test-l1
just test-l2
just test-l2g
just python-check
just python-test
just localization-check
just translation-token-check
just localization-coverage-map-check
just markdown-report-check
just release-note-check origin/main HEAD
git diff --check
```
