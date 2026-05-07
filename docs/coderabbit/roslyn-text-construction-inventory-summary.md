# Roslyn Text Construction Inventory Summary

This is the CodeRabbit-facing summary for the raw-free Roslyn text construction
inventory generated from local decompiled Caves of Qud `1.0.4` C# sources.

The full generated family inventory is intentionally not committed and should
not be added to CodeRabbit knowledge-base file patterns. It is a large
symbol-level audit artifact. Maintainers may regenerate it locally when a PR
claims broad runtime text coverage.

Committed CodeRabbit knowledge should use this summary and the route/token/
glossary review rules instead of the full JSON.

Complementary review knowledge:

- `CODERABBIT.md`
- `docs/coderabbit/owner-route-coverage.md`
- `docs/coderabbit/color-token-review.md`
- `docs/coderabbit/glossary-variance-review.md`

Generator:

- `scripts/tools/TextConstructionInventory/`

Reproduction:

```bash
just text-construction-inventory \
  "$HOME/dev/coq-decompiled_stable" \
  /tmp/roslyn-text-construction-inventory.json \
  docs/coderabbit/roslyn-text-construction-inventory-summary.md
```

## Scope

The inventory is built with `Microsoft.CodeAnalysis.CSharp` and records derived
metadata only. It does not include raw source text, raw English strings,
decompiled method bodies, or absolute source-root paths. The full local JSON
includes relative `parse_error_files` so skipped files are auditable without
exposing source content.

The scanner captures string construction shapes across parsed C# files:

- string literals;
- interpolated strings;
- string concatenations;
- invocation arguments;
- assignment right-hand sides;
- return expressions;
- expression-bodied return expressions;
- initializers;
- attribute arguments.

## Totals

| Metric | Count |
| --- | ---: |
| Files with text constructions | 3905 |
| Producer/member families | 17459 |
| Text constructions | 77188 |
| Parse-error files skipped | 2 |

Skipped parse-error files:

- `XRL.World.AI/AllegianceSet.cs`
- `XRL.World.AI/PartyCollection.cs`

### Shape Counts

| Shape | Count |
| --- | ---: |
| `static_literal` | 69371 |
| `concatenation` | 7528 |
| `interpolation` | 289 |

### Context Counts

| Context | Count |
| --- | ---: |
| `invocation_argument` | 48428 |
| `other` | 7825 |
| `initializer` | 7767 |
| `assignment_rhs` | 7409 |
| `return_expression` | 3552 |
| `attribute_argument` | 2207 |

### Route-Surface Counts

| Surface | Count |
| --- | ---: |
| `OtherInvocation` | 38892 |
| `Initializer` | 13576 |
| `Assignment` | 11048 |
| `Other` | 7825 |
| `Return` | 4722 |
| `StringBuilderAppend` | 2700 |
| `Attribute` | 2207 |
| `Popup` | 1925 |
| `MessageFrame` | 1147 |
| `ReplaceChain` | 1065 |
| `EffectDescriptionReturn` | 651 |
| `AddPlayerMessage` | 516 |
| `DescriptionAssignment` | 513 |
| `ActivatedAbility` | 469 |
| `JournalAPI` | 421 |
| `DisplayNameAssignment` | 346 |
| `Does` | 311 |
| `StringFormat` | 278 |
| `HistoricStringExpander` | 228 |
| `SetText` | 201 |
| `EmitMessage` | 178 |
| `DirectTextAssignment` | 154 |
| `TutorialManagerPopup` | 92 |
| `DisplayNameReturn` | 17 |
| `Description` | 3 |
| `DescriptionReturn` | 2 |
| `DisplayTextReturn` | 2 |
| `GetDisplayName` | 2 |

## Review Use

Use this inventory as a completeness map, not as proof that a route is already
localized. A family being present means Roslyn found text construction in that
member. It does not mean QudJP owns the route. Route-surface counts may exceed
text-construction counts because nested constructions record both inner surfaces
such as `StringFormat` and outer route clues such as `DisplayNameAssignment`.

When reviewing QudJP changes:

- require owner-route reasoning for any family surfaced by the inventory;
- treat `AddPlayerMessage`, `SetText`, `DirectTextAssignment`, and renderer-like
  surfaces as sink-adjacent until producer ownership is proven;
- require tests before accepting changes that claim a family is covered;
- flag PR claims of "complete runtime text coverage" unless the Roslyn summary,
  local regeneration evidence, and the QudJP owner/test matrix were all updated.

## Limits

This inventory is Roslyn syntax based. It does not build a full semantic
compilation, infer runtime receiver types for every call, inspect XML/JSON game
data, or prove UI rendering behavior. Runtime logs and L2/L2G/L3 evidence remain
required only when static evidence cannot settle final behavior.
