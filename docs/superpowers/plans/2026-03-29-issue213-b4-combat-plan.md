# Issue #213 B4: Combat Producer Patch Plan

Date: 2026-03-29
Status: investigation only, no implementation in this document

## Scope

This plan covers the 18 direct `AddPlayerMessage` call sites in `~/Dev/coq-decompiled/XRL.World.Parts/Combat.cs` that currently bypass `Messaging.XDidY` and therefore never acquire a producer-owned translation marker before reaching the message log sink.

Observed facts:

- `Combat.cs` has 18 direct `AddPlayerMessage` sites in exactly two methods:
  - `HandleEvent(GetDefenderHitDiceEvent E)` at `~/Dev/coq-decompiled/XRL.World.Parts/Combat.cs:61-132`
  - `MeleeAttackWithWeaponInternal(...)` at `~/Dev/coq-decompiled/XRL.World.Parts/Combat.cs:892-1595`
- `CombatAndLogMessageQueuePatch` dispatches many producers but has no Combat entry, so current Combat messages always arrive with Combat-owned depth at 0: `Mods/QudJP/Assemblies/src/Patches/CombatAndLogMessageQueuePatch.cs:23-37`
- `MessageLogPatch` is observation-only by contract and only strips direct-translation markers; it does not translate raw combat text: `Mods/QudJP/Assemblies/src/Patches/MessageLogPatch.cs:21-41`
- L2 tests assert that message-log sink text must pass through unchanged even when a matching pattern exists:
  - hit message: `Mods/QudJP/Assemblies/QudJP.Tests/L2/MessageLogPatchTests.cs:47-69`
  - weapon combat message: `Mods/QudJP/Assemblies/QudJP.Tests/L2/MessageLogPatchTests.cs:206-225`
- producer patches follow the same owner pattern:
  - thread-static `activeDepth`
  - `Prefix`/`Finalizer`
  - `TryTranslateQueuedMessage(ref message, color)`
  - examples: `Mods/QudJP/Assemblies/src/Patches/GameObjectDieTranslationPatch.cs:10-78`, `Mods/QudJP/Assemblies/src/Patches/GameObjectEmitMessageTranslationPatch.cs:10-73`
- producer helpers already provide the expected handoff into `MessagePatternTranslator` via `TryPreparePatternMessage`: `Mods/QudJP/Assemblies/src/Patches/MessageLogProducerTranslationHelpers.cs:82-128`
- runtime pattern translation currently loads only `Dictionaries/messages.ja.json`; exact leaf loading is limited to `ui-messagelog-leaf.ja.json`:
  - pattern file path: `Mods/QudJP/Assemblies/src/MessagePatternTranslator.cs:241-305`
  - leaf file path: `Mods/QudJP/Assemblies/src/MessagePatternTranslator.cs:24-25`, `Mods/QudJP/Assemblies/src/MessagePatternTranslator.cs:102-177`
- `ui-messagelog-combat.ja.json` has 29 entries (`rg -c '"key"' Mods/QudJP/Localization/Dictionaries/ui-messagelog-combat.ja.json` => `29`) but no code path loads it; repository references are the file itself and dictionary docs only (`rg -n "ui-messagelog-combat" Mods/QudJP/Assemblies Mods/QudJP/Localization`)

## 1. Combat.cs Methods That Need Producer Patches

Do not patch `AttackObject`, `AttackCell`, or `PerformMeleeAttack`. Patch the exact owner methods that contain the direct `AddPlayerMessage` calls.

### Method A: `HandleEvent(GetDefenderHitDiceEvent E)`

Evidence: `~/Dev/coq-decompiled/XRL.World.Parts/Combat.cs:95-130`

This method owns 3 shield-block/stagger messages:

| Line | Source text shape | Kind | Existing pattern coverage | Plan |
| --- | --- | --- | --- | --- |
| 105 | `You block with {arg}! (+{AV} AV)` | dynamic | none found in `messages.ja.json` | new producer patch + new pattern |
| 121 | `You stagger {target} with your shield block!` | dynamic | none found | new producer patch + new pattern |
| 125 | `You are staggered by {ownerPossBlock}!` | dynamic, mixed possessive (`'s block` or `の block` risk) | none found | new producer patch + new normalized possessive pattern family |

Assessment:

- All 3 are true Combat-owned producer strings.
- All 3 need a producer patch.
- All 3 can still use pattern translation once they are routed through the producer queue.
- The staggered-by route is the only one in this method with moderate grammar risk because `ParentObject.poss("block")` can produce mixed-language possessives.

### Method B: `MeleeAttackWithWeaponInternal(...)`

Evidence: `~/Dev/coq-decompiled/XRL.World.Parts/Combat.cs:1015-1574`

This method owns 15 direct message-log writes:

| Line | Source text shape | Kind | Existing pattern coverage | Plan |
| --- | --- | --- | --- | --- |
| 1015 | `You miss!` | simple leaf | none found | new producer patch + new pattern |
| 1019 | `{{r|You miss with {Attacker.its_(Weapon)}!}} [{hit} vs {dv}]` | dynamic | covered by existing weapon-miss pattern family; see `Mods/QudJP/Localization/Dictionaries/messages.ja.json:110-113` and `Mods/QudJP/Assemblies/QudJP.Tests/L1/MessagePatternTranslatorTests.cs:55-64` | producer patch only |
| 1023 | `{{r|You miss!}} [{hit} vs {dv}]` | dynamic | none found | new producer patch + new pattern |
| 1037 | `{Attacker.Does("miss")} you!` | dynamic | covered by defender miss pattern; `Mods/QudJP/Localization/Dictionaries/messages.ja.json:120-123` | producer patch only |
| 1041 | `{Attacker.Does("miss")} you with {Attacker.its_(Weapon)}! [{hit} vs {dv}]` | dynamic | covered by defender weapon-miss patterns; `Mods/QudJP/Localization/Dictionaries/messages.ja.json:345-352` | producer patch only |
| 1045 | `{Attacker.Does("miss")} you! [{hit} vs {dv}]` | dynamic | none found | new producer patch + new pattern |
| 1281 | `Your mental attack does not affect {Defender.t()}.` | dynamic | existing mental-attack patterns cover the hit-message family, not this direct sentence; see `Mods/QudJP/Localization/Dictionaries/messages.ja.json:45-52`, `Mods/QudJP/Assemblies/QudJP.Tests/L1/DoesVerbFamilyTests.cs:575-580` | new producer patch + new pattern |
| 1527 | `You fail to deal damage with your attack! [{roll}]` | dynamic | none found | new producer patch + new pattern |
| 1531 | `{Attacker.Does("fail")} to deal damage with {Attacker.its} attack! [{roll}]` | dynamic | covered by existing pattern; `Mods/QudJP/Localization/Dictionaries/messages.ja.json:945-948` | producer patch only |
| 1551 | `You don't penetrate {Defender.poss("armor")}.` | dynamic, mixed possessive | partial coverage only for `'s armor` terse form; `Mods/QudJP/Localization/Dictionaries/messages.ja.json:570-582`, but no `の armor` variant | producer patch + expand existing armor pattern family |
| 1555 | `You don't penetrate {Defender.poss("armor")} with {Attacker.its_(Weapon)}. [{roll}]` | dynamic, mixed possessive | partial coverage only via the current `の armor`-specific rule; `Mods/QudJP/Localization/Dictionaries/messages.ja.json:535-538`, `Mods/QudJP/Assemblies/QudJP.Tests/L1/MessagePatternTranslatorTests.cs:228-245` | producer patch + replace/expand with mixed possessive pattern |
| 1559 | `You don't penetrate {Defender.poss("armor")}. [{roll}]` | dynamic, mixed possessive | none found for roll-bearing no-weapon variant | producer patch + new pattern |
| 1566 | `{Attacker.Does("don't")} penetrate your armor.` | dynamic | covered by existing pattern; `Mods/QudJP/Localization/Dictionaries/messages.ja.json:895-898` | producer patch only |
| 1570 | `{Attacker.Does("don't")} penetrate your armor with {Attacker.its_(Weapon)}! [{roll}]` | dynamic | covered by existing pattern; `Mods/QudJP/Localization/Dictionaries/messages.ja.json:885-888` | producer patch only |
| 1574 | `{Attacker.Does("don't")} penetrate your armor! [{roll}]` | dynamic | covered by existing pattern; `Mods/QudJP/Localization/Dictionaries/messages.ja.json:890-893` | producer patch only |

Assessment:

- All 15 need a producer patch because the sink is observation-only.
- 8 of the 15 already have usable regex coverage in `messages.ja.json`; those only need the producer seam.
- 7 of the 15 need new or expanded patterns.
- The highest-risk family in this method is the player-facing `Defender.poss("armor")` family because current coverage is split between English possessive (`'s armor`) and mixed Japanese possession (`の armor`) and does not fully cover all punctuation/roll variants.

## 2. One `CombatTranslationPatch` vs Multiple Per-Method Patches

Recommendation: create multiple per-method patches, not one monolithic `CombatTranslationPatch`.

Proposed classes:

1. `CombatGetDefenderHitDiceTranslationPatch`
2. `CombatMeleeAttackTranslationPatch`

Why this is the lower-risk shape:

- It matches the existing producer architecture: one owner seam per patch file, one `activeDepth`, one `TryTranslateQueuedMessage`.
- The two methods emit different message families:
  - shield block / stagger
  - miss / mental immunity / fail-damage / fail-penetration
- `MeleeAttackWithWeaponInternal` invokes `GetDefenderHitDiceEvent.Process(...)` inside its own flow (`~/Dev/coq-decompiled/XRL.World.Parts/Combat.cs:1086-1089`). That means these owners can be nested at runtime. Separate patches plus ordered dispatcher precedence is clearer than one combined patch with internal branching.
- The test surface becomes more precise:
  - shield block/stagger tests stay independent
  - miss/penetration tests stay independent
- It avoids patching broader wrapper methods like `PerformMeleeAttack`, which would widen ownership and increase false-positive capture risk for unrelated nested producers.

A single `CombatTranslationPatch` would only be worth considering if we needed to share nontrivial structured state across both methods. This investigation did not find that need.

## 3. Integration with `CombatAndLogMessageQueuePatch`

Recommendation: keep the current dispatcher model and add two new entries.

Planned change:

- Add `|| CombatGetDefenderHitDiceTranslationPatch.TryTranslateQueuedMessage(ref Message, Color)`
- Add `|| CombatMeleeAttackTranslationPatch.TryTranslateQueuedMessage(ref Message, Color)`

Placement/order:

1. `CombatGetDefenderHitDiceTranslationPatch`
2. `CombatMeleeAttackTranslationPatch`

Reason for this order:

- shield-block messages happen during the broader melee attack flow
- if both depths are active, the narrower shield/stagger patch must claim its own messages before the broader melee patch sees them

Guard strategy:

- `CombatGetDefenderHitDiceTranslationPatch.TryTranslateQueuedMessage`
  - only consider:
    - `You block with `
    - `You stagger `
    - `You are staggered by `
- `CombatMeleeAttackTranslationPatch.TryTranslateQueuedMessage`
  - only consider:
    - `You miss!`
    - `You miss with `
    - ` misses you`
    - `Your mental attack does not affect `
    - `You fail to deal damage with your attack!`
    - ` fail to deal damage with `
    - `You don't penetrate `
    - ` penetrate your armor`

Translation mechanism:

- Reuse `MessageLogProducerTranslationHelpers.TryPreparePatternMessage(...)`
- Do not alter `MessageLogPatch`
- Do not add sink-side fallback translation
- Let color-stripping and direct-marker behavior continue to flow through existing helper/translator code:
  - strip/restore colors: `Mods/QudJP/Assemblies/src/MessagePatternTranslator.cs:64-76`
  - direct marker insertion: `Mods/QudJP/Assemblies/src/Patches/MessageLogProducerTranslationHelpers.cs:82-128`

## 4. `ui-messagelog-combat.ja.json`: Consume or Merge?

Recommendation: do not add a new runtime consumer for `ui-messagelog-combat.ja.json`. Selectively merge any still-useful content into `messages.ja.json`, then clean up the dead asset in a follow-up change.

Why:

- `ui-messagelog-combat.ja.json` uses exact-entry schema:
  - `meta`
  - `rules`
  - `entries[].key/context/text`
- `MessagePatternTranslator` does not load this file and does not load arbitrary extra exact-entry combat dictionaries:
  - it loads regex patterns from `messages.ja.json`
  - it loads exact leaf entries only from `ui-messagelog-leaf.ja.json`
- many `ui-messagelog-combat.ja.json` rows duplicate combat families already present in `messages.ja.json`:
  - hit/crit families already exist near `Mods/QudJP/Localization/Dictionaries/messages.ja.json:15-30`
  - miss / penetrate / fail-damage families already exist across `Mods/QudJP/Localization/Dictionaries/messages.ja.json:110-123`, `Mods/QudJP/Localization/Dictionaries/messages.ja.json:535-582`, `Mods/QudJP/Localization/Dictionaries/messages.ja.json:885-948`, `Mods/QudJP/Localization/Dictionaries/messages.ja.json:1015-1067`

Practical handling:

- treat `ui-messagelog-combat.ja.json` as migration inventory only
- port only unique combat-specific rows that still matter, and port them as regex `patterns` into `messages.ja.json`
- avoid teaching the translator a second combat-only loader just to preserve a dead asset format

## 5. Pattern Entries Needed in `messages.ja.json`

### New shield-block / stagger family

Add new combat-producer patterns for:

1. `^You block with (.+)! \\(\\+(\\d+) AV\\)$`
2. `^You stagger (.+) with your shield block!$`
3. `^You are staggered by (?:the )?(.+?)(?:'s|s'|の) block!$`

Notes:

- The third pattern should normalize the mixed possessive block phrase instead of passing through raw `snapjaw's block` or `タムの block`.
- If this grammar proves unstable in tests, that is the one place where a tiny producer-specific helper is justified.

### New miss / immunity / zero-damage family

Add new combat-producer patterns for:

1. `^You miss!$`
2. `^You miss! \\[(.+?) vs (.+?)\\]$`
3. `^(?:The |the |[Aa]n? )?(.+) misses you! \\[(.+?) vs (.+?)\\]$`
4. `^Your mental attack does not affect (.+?)\\.$`
5. `^You fail to deal damage with your attack! \\[(.+?)\\]$`

These are direct gaps. Current pattern inventory does not cover them.

### Expand the player-facing armor penetration family

Replace the narrow mixed-language player rule with a broader mixed-possessive family and add the missing roll-bearing variant.

Current partial rule:

- `Mods/QudJP/Localization/Dictionaries/messages.ja.json:535-538`

Planned normalization:

1. `^You don't penetrate (?:the )?(.+?)(?:'s|s'|の) armor with your (.+?)[.!] \\[(.+?)\\]$`
2. `^You don't penetrate (?:the )?(.+?)(?:'s|s'|の) armor[.!] \\[(.+?)\\]$`
3. Expand existing terse no-roll player rules from `(?:'s|s') armor` to `(?:'s|s'|の) armor`

Why:

- current coverage proves that `Defender.poss("armor")` can surface as `の armor` in translated-name paths
- current coverage also proves that terse no-roll lines already exist in the English possessive family
- the Combat producer patch should not depend on a brittle split between English and Japanese possession shapes

### Existing patterns that should be reused unchanged

Do not duplicate these; just route the messages through the producer patch:

- player weapon miss with roll: `Mods/QudJP/Localization/Dictionaries/messages.ja.json:110-113`
- defender terse miss: `Mods/QudJP/Localization/Dictionaries/messages.ja.json:120-123`
- defender weapon miss with roll: `Mods/QudJP/Localization/Dictionaries/messages.ja.json:345-352`
- defender no-damage: `Mods/QudJP/Localization/Dictionaries/messages.ja.json:945-948`
- defender fail-penetration vs player armor: `Mods/QudJP/Localization/Dictionaries/messages.ja.json:885-893`

## 6. Estimated Scope and Risk

### Scope

C#:

- 2 new Harmony patch files in `Mods/QudJP/Assemblies/src/Patches/`
- 2 new dispatcher entries in `CombatAndLogMessageQueuePatch`
- likely no helper changes beyond optional tiny possessive-normalization support for shield-block phrasing

Dictionary:

- add about 8 new regex patterns
- expand about 2 to 4 existing player armor-penetration patterns into mixed-possessive forms
- do not add anything to sink-only exact combat assets

Tests:

- add L1 pattern tests for:
  - `You miss!`
  - `You miss! [x vs y]`
  - `X misses you! [x vs y]`
  - `Your mental attack does not affect X.`
  - `You fail to deal damage with your attack! [x]`
  - mixed-possessive `You don't penetrate ... armor` variants
  - shield-block/stagger patterns
- add L2 producer tests mirroring the existing queue-patch style in `Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs:39-260`

### Risk

Overall risk: medium.

Main risks:

1. Nested producer ownership:
   `MeleeAttackWithWeaponInternal` and `HandleEvent(GetDefenderHitDiceEvent)` can both be active in one attack path.
   Mitigation: separate patches, specific guards, ordered dispatcher precedence.

2. Mixed possessive grammar:
   `Defender.poss("armor")` and `ParentObject.poss("block")` can produce mixed-language strings.
   Mitigation: normalize with regex that accepts both `'s` and `の`.

3. Color-tag + roll suffix combinations:
   miss and penetration messages include full-message color wrapping and bracket payloads.
   Mitigation: rely on existing strip/restore pipeline already exercised by L1 tests for weapon miss and armor penetration.

4. Over-capturing unrelated combat text:
   patching the outer combat wrapper would make this worse.
   Mitigation: patch only the exact owner methods with direct `AddPlayerMessage` calls.

### Recommended implementation order

1. Add `CombatGetDefenderHitDiceTranslationPatch`
2. Add `CombatMeleeAttackTranslationPatch`
3. Wire both into `CombatAndLogMessageQueuePatch` in shield-first order
4. Add/expand the combat regex patterns in `messages.ja.json`
5. Add L1 tests for new pattern families
6. Add L2 queue integration tests for both producer patches
7. Only after tests pass, decide whether to delete or archive `ui-messagelog-combat.ja.json`
