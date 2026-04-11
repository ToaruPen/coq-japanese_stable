# 2026-04-11 DidX review: `XRL.World.Effects/Prone.cs`

## Scope

This note is the first concrete review unit under `docs/reports/2026-04-11-didx-messageframe-batch-01.md`.

Inventory filter used:

- `source_route = DidX`
- `type = MessageFrame`
- `status = needs_patch`
- `file = XRL.World.Effects/Prone.cs`

Total sites in this family: **6**

## Current producer patterns

| Inventory id | Shape |
| --- | --- |
| `XRL.World.Effects/Prone.cs::L158:C5` | `DidXToY("lie", "down on", LyingOn, ...)` |
| `XRL.World.Effects/Prone.cs::L162:C5` | `DidX("lie", "down", ...)` |
| `XRL.World.Effects/Prone.cs::L167:C4` | `DidX("are", "knocked prone", "!", ...)` |
| `XRL.World.Effects/Prone.cs::L273:C5` | `DidXToY("rise", "from", LyingOn, ...)` |
| `XRL.World.Effects/Prone.cs::L277:C5` | `DidXToY("stand", "up from", LyingOn, ...)` |
| `XRL.World.Effects/Prone.cs::L282:C4` | `DidX("stand", "up", ...)` |

## Existing message-frame asset coverage

Already present in `Mods/QudJP/Localization/MessageFrames/verbs.ja.json`:

- tier1 `lie` → `横になった` (`370-371`)
- tier1 `rise` → `立ち上がった` (`494-495`)
- tier1 `stand` → `立ち上がった` (`574-575`)
- tier2 `lie` + `down` → `横になった` (`1436-1438`)
- tier2 `stand` + `up` → `立ち上がった` (`1631-1633`)
- tier2 `are` + `knocked prone` → `倒れた` (`1803-1813`, especially `1812-1813`)

Not present yet:

- `lie` + `down on {0}`
- `rise` + `from {0}`
- `stand` + `up from {0}`

## Ownership reading

This family does **not** look like a new-owner-route problem.

Reasons:

1. The producer is already on the existing `DidX` seam.
2. The missing pieces are object-tail phrase shapes, not a missing route owner.
3. The current `MessageFrameTranslator` contract already supports `XDidYToZ` object-tail resolution through exact/tail/template rows.

## Initial verdict

`Prone` is an **asset-first** family on the existing owner seam.

### Practical meaning

- keep using `XDidYTranslationPatch` + `MessageFrameTranslator`
- do **not** create a new sink or producer patch first
- add message-frame asset coverage for the three missing object-tail forms

## Concrete follow-up candidates

The next asset review for this family should target these possible rows in `verbs.ja.json`:

1. `lie` + `down on {0}`
2. `rise` + `from {0}`
3. `stand` + `up from {0}`

Before adding rows, verify whether the current object-tail normalization in `MessageFrameTranslator.TryTranslateXDidYToZ(...)` expects these as tier2 exact-tail or tier3 template-style entries.

## Proposed row shape

Because these tails contain object placeholders, they should be modeled as **tier3** rows, not tier2 exact pairs.

Reason:

- `TryResolveExactPair(...)` matches the normalized tail literally
- `TryResolveTemplate(...)` is the path that turns patterns like `through {0}` or `at the flames on {0} with {1}` into translated predicates
- the existing test coverage for object-tail frames (`MessageFrameTranslatorTests`) already exercises this tier3 path

Provisional row candidates:

```json
{ "verb": "lie", "extra": "down on {0}", "text": "{0}の上に横になった" }
{ "verb": "rise", "extra": "from {0}", "text": "{0}から起き上がった" }
{ "verb": "stand", "extra": "up from {0}", "text": "{0}から立ち上がった" }
```

## Why tier3 is the right fit

Relevant existing precedents in `verbs.ja.json`:

- `wade` + `through {0}`
- `impale` + `{0} on {1}`
- `try` + `to beat at the flames on {0}, but {1}`

These all use placeholder-bearing `extra` patterns, which is the same shape `Prone` needs for its object-tail forms.

## Remaining caution

The row shapes are now clear, but the exact Japanese wording is still a translation-quality choice rather than an ownership question. So the technical verdict is already stable:

- **owner seam**: existing `DidX` / `MessageFrameTranslator`
- **asset tier**: `tier3`
- **new C# route needed**: no
