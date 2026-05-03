# Issue 466 Procedural Cooking Inventory Slice

Date: 2026-05-03

## Scope

This is the first mechanical inventory slice for procedural cooking localization.
It follows `docs/RULES.md`: dynamic/procedural cooking strings should be owned by
producer or mid-pipeline translation routes, not by broad concrete dictionary
entries.

## Inventory Commands

Structural check used for the known HP producer shape:

```bash
ast-grep run --lang csharp \
  --pattern 'return "@they get +" + $TIER + "% max HP for 1 hour.";'
```

Method inventory was gathered from decompiled `XRL.World.Effects` cooking files
for these methods:

- `GetDescription`
- `GetTemplatedDescription`
- `GetDetails`
- `GetTriggerDescription`
- `GetTemplatedTriggerDescription`

The initial scan found 207 candidate methods:

- 88 dynamic/procedural candidates
- 119 static or low-risk exact candidates

## Current Owner Coverage

`CookingEffectTranslationPatch` now targets 14 method routes. The newly covered
routes in this slice are:

- `XRL.World.Effects.CookingDomainHP_UnitHP.GetDescription`
- `XRL.World.Effects.CookingDomainHP_UnitHP.GetTemplatedDescription`

These reuse the existing `CookingEffectFragmentTranslator` max-HP details rule:

- `+12% max HP` -> `最大HP+12%`
- `+10-15% max HP` -> `最大HP+10-15%`

## High-Confidence Follow-Up Clusters

These dynamic clusters are good next candidates because their source shape is
simple and already resembles existing dictionary exact/range entries:

- stat/resistance units: `Bonus.Signed() + " Acid Resistance"`,
  `Cold Resistance`, `Electric Resistance`, `Heat Resistance`, `AV`, `DV`,
  `Agility`, `Ego`, `MA`
- turn-bounded buffs: `@they gain@s +8 Agility for 50 turns.`,
  `@they gain@s +6 AV for 50 turns.`, `@they gain@s +8 Strength for 50 turns.`
- damage-trigger chances: `whenever @thisCreature take@s damage, there's a N%
  chance`, including phase-on-damage and regen-on-damaged variants
- mutation-use templates: `Can use {MutationDisplayName} at level ...` and
  `+N level(s) to {MutationDisplayName}`

## Deferred

Do not bulk-remove static range entries from `world-effects-cooking.ja.json` in
this slice. Range templates such as `+10-15% max HP` are still useful as stable
template documentation until each owner route is explicitly patched and tested.
