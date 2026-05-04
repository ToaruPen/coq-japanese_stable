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

Additional structural search used for the unit numeric slice:

```bash
just sg-cs 'return $BONUS.Signed() + $SUFFIX;' <coq-decompiled>/XRL.World.Effects
```

## Current Owner Coverage

`CookingEffectTranslationPatch` now targets 97 method routes. The first slice
covered HP owner routes:

- `XRL.World.Effects.CookingDomainHP_UnitHP.GetDescription`
- `XRL.World.Effects.CookingDomainHP_UnitHP.GetTemplatedDescription`

These reuse the existing `CookingEffectFragmentTranslator` max-HP details rule:

- `+12% max HP` -> `最大HP+12%`
- `+10-15% max HP` -> `最大HP+10-15%`

The 2026-05-04 unit numeric slice adds producer coverage for these short
dynamic fragment families:

- resistance units: `+N Acid Resistance`, `Cold Resistance`,
  `Electric Resist/Resistance`, `Heat Resistance`
- stat units: `+N Agility`, `AV`, `DV`, `Ego`, `MA`, `Quickness`,
  `Strength`, `Willpower`, `STR`
- regeneration/save units: `+N% to natural healing rate`,
  `+N to saves vs. bleeding`
- reflect percent units: `Reflect N% damage back at @their attackers, rounded up.`
- damage-trigger chance units: `whenever @thisCreature take@s damage, there's a
  N% chance`
- phase-on-damage and avoidable-damage teleport descriptions:
  `N% chance @they start phasing for 8-10 turns` and
  `N% chance @they teleport to a random space on the map instead`
- turn-bounded stat/resistance descriptions:
  `@they gain@s +N Stat for N turns` and
  `@they gain N Resistance for N turns/hours`

The covered routes include the corresponding `GetDescription` and
`GetTemplatedDescription` methods for the low-tier/high-tier resistance,
stat, regeneration, bleeding-save, and reflect unit classes. Static
`GetTemplatedDescription` range rows in `world-effects-cooking.ja.json` are
retained as low-risk exact documentation in this slice; the runtime
`GetDescription` values are now owner-routed through
`CookingEffectFragmentTranslator`. The damage-trigger slice covers the
`CookingDomainHP_OnDamaged`, `CookingDomainReflect_OnDamaged`,
`CookingDomainRegenLowtier_OnDamaged`, `CookingDomainPhase_UnitPhaseOnDamage`,
and `CookingDomainTeleport_UnitBlink` route families, including high-tier
`GetTemplated*` overrides where the subclass owns the range text.
The turn-bounded description slice covers the agility, armor, strength, cold
resistance, electric resistance, and heat resistance triggered-action producer
routes.

The mutation-use slice covers the concrete mutation/skill unit producers that
had dynamic exact rows in `world-effects-cooking.ja.json`:

- `CookingDomainElectric_EMPUnit`
- `CookingDomainEgo_UnitEgoProjection`
- `CookingDomainBurrowing_UnitBurrowingClaws`
- `CookingDomainArtifact_UnitPsychometry`
- `CookingDomainPlant_UnitBurgeoningHighTier`
- `CookingDomainPlant_UnitBurgeoningLowTier`
- `CookingDomainReflect_UnitQuills`
- `CookingDomainTongue_UnitStickyTongue`
- `CookingDomainFear_UnitIntimidate`

It handles `Can use X at level N`, `Can use X at level N. If @they already
have X...`, `+N level(s) to X`, `Can use Intimidate`, and Intimidate bonus
forms. Cooking-specific legacy terminology is preserved for the exact rows
replaced in this slice.

The basic cooking details slice covers `BasicCookingEffect_* .GetDetails()` and
`BasicTriggeredCookingStatEffect.GetDetails()` for signed HP, move speed,
quickness, MA, to-hit, XP, natural healing, random stat, and triggered stat
detail rows.

## Dictionary Cleanup

The following broad/prefix-like exact rows were removed from
`world-effects-cooking.ja.json` and guarded by
`LocalizationCoverageTests.DynamicProducerRoutes_DoNotKeepKnownConcreteExactKeys`:

- `@they get +`
- `@they expel quills per the Quills mutation at level`
- `@they expel quills per the Quills mutation at level␠`
- `Reflect`
- `Reflect␠`
- `whenever @thisCreature take@s damage, there's a`
- `whenever @thisCreature take@s damage, there's a␠`
- `Whenever @thisCreature take@s avoidable damage, there's a`
- `Whenever @thisCreature take@s avoidable damage, there's a␠`
- `Can use`
- `Can use␠`
- concrete `Can use X at level N-M. If @they already have X...` mutation rows
- generic `{MutationDisplayName}` mutation-use template rows
- `Can use Intimidate.` and Intimidate bonus template/concrete rows
- `BasicCookingEffect_*` default exact detail rows such as `+10% hit points`,
  `+6% Move Speed`, and `+5% XP gained`

These are not proven fixed leaves. They are partial producer fragments and must
remain owned by route-local translators or documented as deferred route work.

## Remaining High-Confidence Follow-Up Clusters

These dynamic clusters are good next candidates because their source shape is
simple and already resembles existing dictionary exact/range entries:

- wrapped `{{w|... for N turns}}` active-effect details, if fresh runtime
  evidence confirms they bypass the owner `GetDetails()` route already covered
  by `BasicTriggeredCookingStatEffect`

## Additional PR Scope

Two adjacent localization regressions were fixed while preparing this PR:

- Main menu `Mods` remains `Mod` after the menu is rebound or refreshed. This
  prevents the Tinkering-specific `Mod` -> `改造` entry from being applied to
  the main menu label after screen transitions.
- Message frame tests and `Localization/MessageFrames/verbs.ja.json` now agree
  on play-quality Japanese for several fixed producer tails, including
  possessive mutation fragments such as `its carapace`, `its reflective shield`,
  `its quills`, `its geospatial core`, and adjacent concrete tails such as
  `with its fists`, `on the head`, `a psychic presence...`, `bleeding from
  another wound`, and `a puddle of acid`.

## Deferred

Do not bulk-remove static range entries from `world-effects-cooking.ja.json` in
this slice. Range templates such as `+10-15% max HP` are still useful as stable
template documentation until each owner route is explicitly patched and tested.
