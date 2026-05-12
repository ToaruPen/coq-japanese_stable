# Issue #635 Combat Text Surface Route Proof

## Scope

Pilot surface: `CombatTextSurface`.

The slice is limited to existing Combat owner routes that already gate queued
`AddPlayerMessage` traffic before the generic message queue translator:

- `XRL.World.Parts/Combat.cs::XRL.World.Parts.Combat.HandleEvent`
  with `GetDefenderHitDiceEvent`.
- `XRL.World.Parts/Combat.cs::XRL.World.Parts.Combat.MeleeAttackWithWeaponInternal`.

## Static Producer Evidence

Current #576 static producer queue classifies the Combat source file as:

- `XRL.World.Parts/Combat.cs::XRL.World.Parts.Combat.HandleEvent`
  - 3 `AddPlayerMessage` callsites.
  - status: `owner_patch_required`.
  - shapes: shield block, shield-block stagger, staggered-by-block.
- `XRL.World.Parts/Combat.cs::XRL.World.Parts.Combat.MeleeAttackWithWeaponInternal`
  - 15 `AddPlayerMessage` callsites.
  - status: `owner_patch_required`.
  - shapes: miss, mental-attack no-effect, failed damage, and no-penetration messages.

The adjacent `PerformMeleeAttack` `EmitMessage` callsites are excluded from this
pilot because they are `messages_candidate` and use a different sink boundary.

One production dictionary gap was found while proving the full melee family:
`You don't penetrate <target>'s armor.` had no displayed roll value and did not
match the existing roll-value patterns. This slice adds that exact stable frame
to `messages.ja.json`.

## Route Boundary

Owner boundary:

- Harmony scopes only the two Combat owner methods above.
- The message queue pipeline translates only while that owner scope is active.
- Queue-only traffic stays unchanged when the owner scope is absent.

Sink boundary:

- `CombatAndLogMessageQueuePatch` remains the message queue sink hook.
- The Combat surface remains a route-specific translator in
  `MessageQueueSemanticPipeline`; it is not a generic `AllTextSurface`.

## Consolidation

Current state:

- `CombatGetDefenderHitDiceTranslationPatch`: 1 patch class, 1 target method.
- `CombatMeleeAttackTranslationPatch`: 1 patch class, 1 target method.

Pilot target:

- Replace both with `CombatTextSurfaceTranslationPatch`.
- Expected patch class count delta for this slice: `2 -> 1`.
- L2G-resolved Harmony target method count delta for this slice: `2 -> 2`.
- Fresh startup/runtime patch-count measurement was not run for this slice.

This is a small #635 pilot: it reduces a real patch class and resolver surface,
but does not claim a target-method, startup median, or runtime-patched-method
reduction.

## Verification Plan

- L2: existing Combat queue behavior should pass through the new surface.
- L2: owner-absent traffic remains unchanged.
- L2: direct-marked and empty queue messages remain unchanged.
- L2G: target resolution covers both Combat owner methods on the new patch type.
- Static closure overlay: register only the two reviewed Combat family IDs.

## Closure Evidence

- L2 repository-pattern tests cover all 3 shield-block shapes and all 15 melee
  shapes from `docs/static-producer-inventory.json`.
- L2 negative tests cover owner-absent queue traffic, direct-marked traffic, and
  empty messages.
- L2G target-resolution tests cover both upstream Combat owner methods.
- `scripts/static_producer_closure.py` registers only the two reviewed Combat
  family IDs; `PerformMeleeAttack` remains outside this pilot.
