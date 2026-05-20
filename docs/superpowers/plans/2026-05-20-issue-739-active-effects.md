# Issue 739 Active Effects Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close the observed active-effects UI coverage gaps without broad sink fallback or breaking existing cooking-route ownership.

**Architecture:** Keep `EffectDescriptionPatch` / `EffectDetailsPatch` as the owner seam for non-cooking effect names and details, and keep cooking-specific routes separate. Add fixed leaf and generated-template coverage for observed active-effect strings, prove the active-effects book body composes translated effect name/detail text, and check in a deterministic active-effect producer inventory generated from the existing Roslyn text-construction inventory.

**Tech Stack:** C# Harmony patches and NUnit L1/L2 tests, JSON localization dictionaries, existing `just` validation recipes, existing Roslyn text-construction inventory.

---

## Brief

**Goal:**
Issue #739 covers English residue visible through Active Effects UI routes: the active-effects book, status effect rows/details, and ability-bar active-effect summaries.

**Non-goals:**
- Do not remove the existing `Cooking` exclusion from `ActiveEffectOwnerTargetResolver`; that would overlap with cooking-specific owner routes.
- Do not add broad `UITextSkin` or final-output sink fallback behavior.
- Do not attempt full runtime proof inside this implementation branch; runtime log verification remains a manual L3 gate.

**Scope ledger:**
- Original requested themes: pull latest changes, create a worktree, use ORCHID, address issue #739.
- Covered implementation themes: observed `metabolizing`, Apple Matz thirst detail, long blade stance details, active-effects book body composition, status rows, ability-bar active-effect summaries, inventory artifact and release note.
- Deferred themes: replacing existing static-analysis infrastructure with a dedicated Roslyn scanner. The checked-in artifact can be regenerated from the current existing Roslyn inventory lane.

**Acceptance criteria:**
- Observed active-effect samples translate before generic final-output sinks.
- Active-effects book body, status rows, and ability-bar active-effect summaries share consistent coverage for names and details.
- Tests cover cooking/metabolizing, long blade stance effects, and representative non-cooking active effects.
- A current decompiled-source active-effect producer inventory is checked into docs and each item is classified.
- Fresh runtime `Player.log` evidence is called out as a remaining L3 verification gate if it is not run.

**Relevant files and ownership boundaries:**
- `Mods/QudJP/Assemblies/src/Patches/ActiveEffectTextTranslator.cs`: shared active-effect text translator.
- `Mods/QudJP/Assemblies/src/Patches/AbilityBarAfterRenderTranslationPatch.cs`: ability-bar active-effects owner.
- `Mods/QudJP/Assemblies/src/Patches/CharacterEffectLineTranslationPatch.cs`: status row effect-name owner.
- `Mods/QudJP/Assemblies/src/Patches/GameObjectShowActiveEffectsPatch.cs`: active-effects book title/empty literal transpiler; body is owned by effect name/detail owner methods.
- `Mods/QudJP/Localization/Dictionaries/world-effects-status.ja.json`: stable non-cooking effect leaves.
- `Mods/QudJP/Localization/Dictionaries/world-effects-cooking.ja.json`: stable cooking effect leaves.
- `Mods/QudJP/Localization/Dictionaries/Scoped/world-effects-generated-templates.ja.json`: active-effect generated templates.
- `docs/active-effect-producer-inventory.json`: issue-specific current static inventory.

## Tasks

### Task 1: Red tests for observed active-effect text

**Files:**
- Modify: `Mods/QudJP/Assemblies/QudJP.Tests/L1/ActiveEffectTextTranslatorTests.cs`
- Modify: `Mods/QudJP/Assemblies/QudJP.Tests/L2/ActiveEffectsOwnerPatchTests.cs`
- Modify: `Mods/QudJP/Assemblies/QudJP.Tests/L2/AbilityBarAfterRenderTranslationPatchTests.cs`
- Modify: `Mods/QudJP/Assemblies/QudJP.Tests/L2/StatusScreenBindingOwnerPatchTests.cs`

- [ ] Add tests for `metabolizing` / `{{W|metabolizing}}`, `You thirst at half rate.`, and long blade stance details.
- [ ] Run focused tests and confirm they fail because the translations or coverage are missing.

### Task 2: Minimal active-effect coverage implementation

**Files:**
- Modify: `Mods/QudJP/Localization/Dictionaries/world-effects-status.ja.json`
- Modify: `Mods/QudJP/Localization/Dictionaries/world-effects-cooking.ja.json`
- Modify: `Mods/QudJP/Localization/Dictionaries/Scoped/world-effects-generated-templates.ja.json`
- Modify: `Mods/QudJP/Assemblies/src/Patches/ActiveEffectTextTranslator.cs` only if exact/template dictionary coverage is insufficient.

- [ ] Add stable active-effect leaves for observed samples and sibling long blade stance details.
- [ ] Add generated templates only where numeric variation is proven by decompiled source.
- [ ] Re-run focused L1/L2 tests and keep existing route ownership intact.

### Task 3: Checked-in active-effect producer inventory

**Files:**
- Create: `docs/active-effect-producer-inventory.json`
- Modify: `Mods/QudJP/Assemblies/QudJP.Tests/L1/LocalizationCoverageTests.cs`

- [ ] Generate a deterministic issue-specific inventory from the current decompiled source via `just text-construction-inventory`.
- [ ] Filter to `XRL.World.Effects/*` families with active-effect display surfaces.
- [ ] Classify each row as `owner-translated`, `fixed-leaf translated`, `generated/composed route translated`, `intentional pass-through`, or `deferred with reason`.
- [ ] Add an L1 coverage test that the artifact exists, is current enough for observed producers, and has only accepted classifications.

### Task 4: Release note and verification

**Files:**
- Create: `docs/release-notes/unreleased/issue-739-active-effects.md`

- [ ] Add a release-note fragment because localization assets change.
- [ ] Run focused C# tests, `just localization-check`, `just translation-token-check`, and `just release-note-check origin/main HEAD`.
- [ ] Report that fresh runtime `Player.log` verification remains if not run locally.
