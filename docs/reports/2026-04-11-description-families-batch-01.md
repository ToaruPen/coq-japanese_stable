# 2026-04-11 Description families batch 01

## Scope

This note starts the description-family buckets called out in `docs/reports/2026-04-11-fixed-leaf-owner-triage.md:109-114`:

- `Parts.GetShortDescription` — 264
- `Effects.GetDescription` — 180
- `Effects.GetDetails` — 171

The goal is to separate true owner-side description routes from the asset layers they already use, so later review can stay route-family-first instead of treating these as generic leaf imports.

## Route policy

- `docs/RULES.md:50-63`
  - stable exact leaves may live in localization assets
  - dynamic, procedural, markup-preserving, or route-owned text should stay on C# owner seams
- `docs/RULES.md:86-88,111-118`
  - if a family already has a narrower route owner, prefer that route over broad sink-side handling

That policy fits description families well: they already have dedicated producer hooks, while assets supply exact rows or templates underneath those hooks.

## 1. `Parts.GetShortDescription`

### Existing owner seam

- `Mods/QudJP/Assemblies/src/Patches/DescriptionShortDescriptionPatch.cs:11-48`
  - directly patches `XRL.World.Parts.Description.GetShortDescription(bool, bool, string)`
  - hands the returned text to `DescriptionTextTranslator.TranslateShortDescription(...)`
- `Mods/QudJP/Assemblies/src/Patches/DescriptionTextTranslator.cs:22-33,35-133`
  - keeps short/long description routes separate at the patch entry
  - tries color-preserving translation, scoped world-mod translation, compare/status helpers, exact-leaf lookup, then pattern translation
- `Mods/QudJP/Assemblies/src/Patches/WorldModsTextTranslator.cs:10-11,91-145,148-260`
  - provides the dedicated world-mod scoped dictionary/template layer used by the description route

### Current proof level

- `Mods/QudJP/Assemblies/QudJP.Tests/L2/DescriptionShortDescriptionPatchTests.cs:47-76`
  - proves scoped world-mod entries translate through the patch
- `.../DescriptionShortDescriptionPatchTests.cs:84-117`
  - proves route ownership is recorded on `DescriptionShortDescriptionPatch` rather than falling back to `UITextSkin`
- `.../DescriptionShortDescriptionPatchTests.cs:125-195`
  - proves pattern-driven village-description and world-mod template handling
- `.../DescriptionShortDescriptionPatchTests.cs:254-262`
  - shows the test asset split explicitly: `description-short-l2.ja.json` for exact leaf rows and `world-mods.ja.json` for scoped world-mod rows
- `Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs:161`
  - locks the hook target to the intended game method

### Reading

`Parts.GetShortDescription` is an **owner-side description route with mixed asset support**.

It should not be triaged as a flat fixed-leaf bucket because the current route already owns:

1. color preservation
2. scoped world-mod templates
3. compare/status helper subfamilies
4. exact-leaf fallback only after the description route has first-class ownership

So later review should split this bucket by translator subfamily (`WorldMods`, compare/status helpers, exact-leaf fallback, pattern fallback), not by raw scanner leaf count.

## 2. `Effects.GetDescription` / `Effects.GetDetails`

### Existing owner seam

- `Mods/QudJP/Assemblies/src/Patches/EffectDescriptionPatch.cs:11-49`
  - patches `XRL.World.Effect.GetDescription()`
  - routes through `ActiveEffectTextTranslator.TryTranslateText(..., "ActiveEffects.Description", ...)`
- `Mods/QudJP/Assemblies/src/Patches/EffectDetailsPatch.cs:11-49`
  - patches `XRL.World.Effect.GetDetails()`
  - routes through `ActiveEffectTextTranslator.TryTranslateText(..., "ActiveEffects.Details", ...)`
- `Mods/QudJP/Assemblies/src/Patches/ActiveEffectTextTranslator.cs:7-66`
  - provides exact color-preserving translation first
  - then line-by-line fallback for multiline effect text

### Current proof level

- `Mods/QudJP/Assemblies/QudJP.Tests/L2/ActiveEffectsOwnerPatchTests.cs:38-68`
  - proves both effect methods translate owner text through the dedicated effect patches
- `.../ActiveEffectsOwnerPatchTests.cs:70-103`
  - proves the translated description/details flow into the status-pane owner path
- `.../ActiveEffectsOwnerPatchTests.cs:146-170`
  - shows the test asset is a dedicated effect dictionary file: `active-effects-owner-l2.ja.json`
- `Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs:156-160`
  - locks `EffectDescriptionPatch`, `EffectDetailsPatch`, `CharacterStatusScreenHighlightEffectPatch`, and `GameObjectShowActiveEffectsPatch` to their intended targets

### Reading

`Effects.GetDescription` and `Effects.GetDetails` read as one **shared active-effect owner bucket**, not two unrelated leaf queues.

The important split is:

1. owner-side effect hooks and status-pane/book display routes stay in C#
2. exact effect strings can still come from assets beneath that owner seam
3. multiline effect details already have dedicated helper behavior, so this is not a plain flat-dictionary import problem

## Best first review split

The next review decomposition should be:

1. **`Parts.GetShortDescription` subfamilies**
   - `WorldModsTextTranslator`-owned rows
   - compare/status helper rows
   - true exact-leaf leftovers that survive the route helper chain
2. **Active effect bucket**
   - `Effects.GetDescription` + `Effects.GetDetails` together
   - separate exact single-line effects from multiline/details-heavy families

## Initial verdict

Description families are **already owner-routed**.

This batch should therefore be treated as a route-family decomposition task, not as evidence that these counts should move directly into fixed-leaf dictionary registration.

In PR terms, the key takeaway is simple:

- `Parts.GetShortDescription` -> existing owner seam with scoped asset/helper subfamilies
- `Effects.GetDescription` / `Effects.GetDetails` -> shared active-effect owner seam with exact/line-based asset support
