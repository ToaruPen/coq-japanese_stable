# Design: Final Display-Path Ownership Per Route (Issue #183)

**Date**: 2026-03-28
**Status**: Approved
**Strategy**: Horizontal-first spec, vertical-slice implementation

## Problem

Current L2 tests prove that producer/owner patches translate their local outputs correctly, but do not consistently prove that those translated values are the exact strings the player finally reads on screen. This gap means route-local translation success is treated as if it implies final on-screen correctness.

## Goals

1. Classify every route family by how its final display-path ownership can be proven.
2. Define what each automated layer (L1, L2, L2G, L3) proves and does not prove.
3. Add missing ownership tests for 3 pilot families.
4. Document L3 reproduction procedures for runtime-required families.
5. Provide a contributor-facing "done" checklist per route.

## Non-goals

- Full L2 ownership test coverage for all 16 families (deferred to child issues).
- L3 harness automation (manual procedure documentation only).
- Game launch or runtime verification within this issue.

## Route Ownership Matrix

### Classification definitions

| Category | Definition | L2 proves | L3 role |
|----------|-----------|-----------|---------|
| **Statically-provable** | DummyTarget can verify the final returned/displayed value end-to-end | owner translates -> sink observation = 0 -> return value is Japanese | Regression only |
| **Narrowable** | L2 proves ownership and sink suppression, but UI rendering depends on Unity runtime | owner translates + sink suppressed | Tab/screen navigation to visually confirm |
| **Runtime-required** | Route fires only under specific game state; sink is observation-only | producer translation logic + observation-only contract | Reproduction steps with specific game actions |

### Family assignments (Codex-verified)

| Family | Category | Patch type | Key patches |
|--------|----------|-----------|-------------|
| Conversation display text | Statically-provable | Postfix, `ref __result` | ConversationDisplayTextPatch |
| Descriptions/tooltips | Statically-provable | Postfix, `ref __result` / StringBuilder | DescriptionShortDescriptionPatch, DescriptionLongDescriptionPatch, LookTooltipContentPatch |
| Zone display names | Statically-provable | Postfix, `ref __result` | ZoneDisplayNameTranslationPatch |
| Journal entry display | Statically-provable | Postfix, `ref __result` | JournalEntryDisplayTextPatch, JournalMapNoteDisplayTextPatch |
| Popup message | Statically-provable | Prefix, `__args` + item rewrite | PopupMessageTranslationPatch |
| Inventory/equipment | Narrowable | Postfix, field update | InventoryAndEquipmentStatusScreenTranslationPatch |
| Character status | Narrowable | Postfix, field update | CharacterStatusScreenTranslationPatch, CharacterStatusScreenMutationDetailsPatch |
| Skills/powers | Narrowable | Postfix, field/text update | SkillsAndPowersStatusScreenTranslationPatch, SkillsAndPowersStatusScreenDetailsPatch |
| Factions | Narrowable | Postfix, field update | FactionsLineDataTranslationPatch, FactionsLineTranslationPatch, FactionsStatusScreenTranslationPatch |
| Player status bar / ability bar | Narrowable | Postfix, dictionary/text update | PlayerStatusBarProducerTranslationPatch, AbilityBarAfterRenderTranslationPatch |
| Popup conversation | Narrowable | Observation-only at sink; owner = ConversationDisplayTextPatch | PopupTranslationPatch (ShowConversation path) |
| Popup (ShowBlock/ShowOptionList) | Runtime-required | Observation-only | PopupTranslationPatch |
| Message log | Runtime-required | Observation-only | MessageLogPatch |
| UITextSkin | Runtime-required | Observation-only sink | UITextSkinTranslationPatch |
| SinkPrereq | Runtime-required | Observation-only near-sink | SinkPrereqSetDataTranslationPatch, SinkPrereqUiMethodTranslationPatch |

## Layer Proof Rules

| Layer | Proves | Does not prove |
|-------|--------|---------------|
| **L1** | Translation logic, color code preservation, pattern matching correctness | Patch application target, game type compatibility |
| **L2** | Owner route translates -> sink observation suppressed -> return value/field is Japanese | UI rendering, font/glyph display, Unity runtime behavior |
| **L2G** | TargetMethod resolves on real DLL, signature matches | Patch body translation result, display |
| **L3** | Final screen shows Japanese text, no log errors | (Terminal proof layer) |

### L2 Ownership 3-point assertion pattern

For any route claiming display-path ownership, L2 tests must verify:

1. **Translation**: Patched return value or field contains translated Japanese text.
2. **Route recording**: `DynamicTextObservability` records the transform under the correct owner route family.
3. **Sink suppression**: Downstream `SinkObservation` hit count for the corresponding sink is 0.

## Pilot Families

### 1. Conversation display text (statically-provable)

- **Existing coverage**: `ConversationDisplayTextPatchTests` already has the 3-point pattern at lines 67-100.
- **Gap**: Verify completeness; add edge cases if missing.
- **L3**: Regression confirmation only.

### 2. Inventory/equipment (narrowable)

- **Existing coverage**: `InventoryAndEquipmentStatusScreenTranslationPatchTests` has field value checks (lines 42-80) and owner route recording (lines 84-100).
- **Gap**: Add explicit sink suppression assertion if missing.
- **L3**: Document tab-navigation verification procedure using existing `verify_inventory.py`.

### 3. Popup ShowBlock (runtime-required)

- **Existing coverage**: `PopupTranslationPatchTests` verifies observation-only behavior (lines 47-75).
- **Gap**: Add explicit observation-only contract test asserting `SinkObservation` is logged with `ObservationOnlyDetail`.
- **L3**: Document reproduction steps (quit popup, NPC interaction popups, skill point popup).

## Output Files

| File | Purpose |
|------|---------|
| `docs/superpowers/specs/2026-03-28-display-path-ownership-design.md` | This design spec |
| `docs/display-path-ownership.md` | Contributor-facing matrix, rules, and "done" checklist |

## Follow-up (child issues)

- Extend L2 ownership 3-point assertions to remaining statically-provable families
- Extend L2 ownership assertions to remaining narrowable families
- Expand L3 Rosetta verification harness beyond inventory
- Add L3 reproduction procedures for all runtime-required families
