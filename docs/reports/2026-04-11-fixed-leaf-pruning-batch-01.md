# 2026-04-11 fixed-leaf pruning batch 01

## Scope

This note covers the current fixed-leaf queue described in `docs/reports/2026-04-11-fixed-leaf-owner-triage.md:52-97`.

The immediate problem is not "which new strings should we import first?" It is "which current candidates are obviously too broad, pseudo-leaf, or route-owned noise and must be pruned before any dictionary promotion work?"

## Proven fixed-leaf gate

- `docs/RULES.md:65-74`
  - a proven fixed-leaf must be stable, owner-safe, markup-preserving, and not `needs_runtime`
- `docs/RULES.md:86-88`
  - dynamic, procedural, `message-frame`, builder/display-name, unresolved, and runtime-dependent candidates must not be routed into dictionary work unless separately proven safe
- `scripts/scanner/inventory.py:221-233`
  - scanner-side `is_proven_fixed_leaf` requires `Leaf`, `HIGH` confidence, route provenance, ownership provenance, `destination_dictionary`, and no `needs_review` / `needs_runtime` / rejection reason
- `scripts/scanner/fixed_leaf_validation.py:107-132`
  - anything that fails that gate is rendered as a `BROAD_ENTRY`

So the first pruning question is simple: does this candidate still satisfy the proven fixed-leaf contract after route ownership is considered?

## Current pending queue shape

`docs/reports/2026-04-11-fixed-leaf-owner-triage.md:54-96` already shows that the current non-translated fixed-leaf queue is only **27** rows and is dominated by non-import-ready values.

Representative examples from that queue:

- empty-string placeholders
  - `Qud.UI/AchievementViewRow.cs:79,87,99`
  - `Qud.UI/PopupMessage.cs:610`
  - `Qud.UI/WorldGenerationScreen.cs:229-252`
- blank spacer text
  - `Qud.UI/WorldGenerationScreen.cs:223` -> `" "`
- UI channel / widget identifiers rather than user-facing English
  - `Qud.UI/OptionsRow.cs:60` -> `BodyText`
  - `XRL.CharacterBuilds.Qud.UI/AttributeSelectionControl.cs:69` -> `BodyText`
  - `XRL.UI/Look.cs:293,316` -> `BodyText`
  - `SteamWorkshopUploaderView.cs:121` -> `SelectedModLabel`

These are not strong dictionary-registration candidates. They are mostly placeholders, spacing tokens, or sink/widget identifiers.

## Why these rows are pruning candidates

### 1. Literal strings are not automatically safe fixed-leaf entries

- `scripts/scanner/rule_classifier.py:131-154`
  - generic sink scanning marks string literals as `Leaf`
  - but template syntax, `GetDisplayName`, `StringBuilder`, and unresolved expressions are classified out of the fixed-leaf default immediately
- `scripts/scanner/rule_classifier.py:221-278`
  - only a high-confidence `Leaf` with no review/runtime flags gets default fixed-leaf provenance
  - other types are assigned rejection reasons such as `template`, `builder_display_name`, `message_frame`, `verb_composition`, `procedural`, `narrative_template`, or `unresolved`

So the fixed-leaf queue must still be reviewed for "literal but not meaningful" candidates. Empty strings and channel labels are the clearest examples.

### 2. Validator rejects broad or malformed promotions upstream

- `scripts/scanner/fixed_leaf_validation.py:107-118,169-178`
  - non-proven candidates fail as `BROAD_ENTRY`
  - failure factors are surfaced as ownership class, `needs_review`, and `needs_runtime`
- `scripts/scanner/fixed_leaf_validation.py:135-166`
  - duplicate keys fail separately as `DUPLICATE_KEY`
  - destination mismatch is also validated before promotion

That means pruning obvious pseudo-leaf rows is not optional cleanup. It directly reduces validator noise before any real import attempt.

## Broader noise classes that should stay out of fixed-leaf work

Even outside the current 27-row queue, the scanner rules make the broader pruning target explicit:

- `template` / `builder_display_name` / `message_frame` / `verb_composition` / `variable_template` / `procedural` / `narrative_template` / `unresolved`
  - see `scripts/scanner/inventory.py:82-94`
  - and `scripts/scanner/rule_classifier.py:260-279`

Representative current inventory examples in `docs/candidate-inventory.json`:

- unresolved sink route
  - `Qud.API/IBaseJournalEntry.cs::L243:C3` -> `MessageQueue.AddPlayerMessage(Message)` (`type=Unresolved`, `rejection_reason=unresolved`)
- dynamic unresolved string assembly
  - `SoundManager.cs::L434:C6` -> `MessageQueue.AddPlayerMessage(Channel + ": " + Track + " (Wasn't found)")` (`type=Unresolved`)
- proven safe leaf, shown here as contrast rather than pruning
  - `XRL.Core/ActionManager.cs::L1325:C12` -> `You can't figure out how to safely reach the stairs from here.` (`type=Leaf`, `destination_dictionary=scoped`, `rejection_reason=null`)

This contrast matters: the pruning batch is about removing pseudo-leaf and route-owned noise so that the remaining survivors look more like the ActionManager exact-leaf case and less like unresolved sink traffic.

## Initial verdict

The current fixed-leaf batch should be treated as **prune-first, import-later**.

Immediate actions implied by the evidence:

1. drop obvious placeholder rows (`""`, `" "`)
2. drop or reclassify UI channel/widget identifiers (`BodyText`, `SelectedModLabel`)
3. keep validator attention on duplicate exact leaves and true proven fixed-leaf survivors
4. avoid spending review effort on owner-side route families under the fixed-leaf label

## PR-facing takeaway

This batch does not yet nominate a strong set of new dictionary entries.

What it does provide is a cleaner rule for the next pass: fixed-leaf work should start only after placeholder/UI-identifier rows are pruned, because the current queue is still dominated by pseudo-leaf noise rather than player-facing import-ready strings.
