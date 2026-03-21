# Logic-Required Translation Policy

This document defines the default policy for dynamic or procedurally composed text in QudJP.

## Scope

Use this policy whenever the rendered text is not a stable leaf string and instead comes from upstream composition such as:

- `GetDisplayNameEvent` / `DescriptionBuilder`
- `Grammar` helpers (`A`, `Pluralize`, `MakePossessive`, `MakeAndList`, `MakeOrList`, `SplitOfSentenceList`)
- message assembly (`AddPlayerMessage`, `XDidYToZ`, `TakeDamage`, etc.)
- popup / sink templates with interpolated values
- inventory suffixes, item states, generated titles, and liquid descriptions

## Core Rule

Generator-first is the default policy for logic-required text.
Do not close a dynamic string from logs alone when the upstream generator is still unknown.
For logic-required text, translation, implementation, and tests must be organized from the upstream generator outward.
For logic-required text, translation must follow the upstream structure that generated the text.
Do not treat a single log line as the translation unit by default; name the family first, then verify that family against upstream evidence.

## Required Workflow

1. Identify the upstream generator from source/decompiled game code, reference data, or other repository evidence before designing the translation family.
2. Determine the slot structure precisely from that generator.
   Examples:
   - amount + unit + liquid name
   - base name + adjective list + title suffix
   - message template + actor + target + weapon + numeric values
   - bracketed state tag + optional nested target
3. Name the family from the generator/slot structure, not from a single observed log line.
4. Record the evidence pointers used for that conclusion.
5. Implement the smallest route/template logic that matches the upstream structure.
6. Add regression tests that reflect the same generator-derived slot structure and its boundaries.
7. Do not write tests around the surface string alone if the generator structure is already known.
8. Confirm the runtime route with L3 logs when needed.

## What Counts As Sufficient Upstream Confirmation

At least one of the following must be true before adding or widening a route/template family:

- The upstream C# generator was located and inspected.
- The upstream text asset was located and matched to the runtime output.
- The runtime patch target is already known and the remaining uncertainty is only a stable formatting detail.

If none of these are true, stop and investigate upstream first.

## Grammar Vs Feature Generator

Before treating a dynamic family as a general `Grammar` problem, explicitly separate these two cases:

- the text is produced by `XRL.Language.Grammar` itself, or by a direct caller whose only structure comes from grammar helpers
- the text is produced by a feature-specific generator that merely happens to contain grammar words such as `of`, articles, conjunctions, or number words

This distinction must be confirmed from upstream evidence.

Examples of feature-specific generators include:

- `LiquidVolume.AppendLiquidDescription(...)`
- `GetDisplayNameEvent` / `DescriptionBuilder`
- combat/message assembly code
- popup/menu builders

The presence of a grammar word in the final English string is not sufficient evidence that the family belongs to `Grammar`.

For example, if a rendered string contains `of`, first determine whether:

1. `Grammar` produced that structure, or
2. a feature-specific generator emitted a fixed slot pattern containing the word `of`

If case `2` is true, implement and test the family under that feature-specific route instead of widening a general grammar route.

## Testing Requirements

Logic-required work needs generator-first tests that match the upstream composition boundary, not just the final surface string.

### L1

Add pure logic tests for:

- parser / regex shape
- placeholder ordering
- optional segments
- singular/plural branches
- list cardinality boundaries (`0`, `1`, `2`, `3+`)
- state/tag combinations

L1 cases should be derived from the generator's slots and branches, not from isolated log examples alone.

### L2

Add Harmony integration tests for the actual patch route:

- the route intercepts the right method
- the right text field / return value is transformed
- untranslated branches pass through unchanged
- already localized branches do not create missing-key noise

L2 should prove that the chosen route matches the generator family you identified upstream.

### L3

Use runtime logs only for:

- verifying that the expected route/family is actually hit
- finding new upstream families that were not yet characterized
- checking rendered Japanese for regressions after route changes

L3 is not a substitute for upstream analysis.

## Design Rules

- Prefer generator-first design over log-first patching.
- Prefer slot-aware translation over exact-key accumulation.
- Prefer upstream-family routes over downstream sink-only cleanup.
- Do not use a single observed log line as the family definition; define the family name first, then decide whether the observed line is only one member of that family.
- Prefer stable leaf dictionaries only for true atomic terms or fixed labels.
- Do not treat arbitrary mixed strings as free-form phrases if the source is actually structured.
- Do not widen a regex family unless its upstream cardinality and optional parts are understood.

## Common Families

### Display Name

Expected upstream families include:

- adjective + base
- proper name + title
- base + bracketed state
- base + parenthesized state
- base + quantity/code suffix
- amount + `dram(s) of` + liquid name

### Grammar

Expected upstream families include:

- article handling
- possession
- conjunction/disjunction lists
- capitalization
- number words

### Message Log / Popup

Expected upstream families include:

- fixed sentence template + interpolated entities
- quoted speech
- combat side-effect messages
- death wrappers
- menu / hotkey labels

## Observability

When changing a logic-required family, make sure the route remains observable.

Preferred evidence sources:

- `DynamicTextProbe/v1`
- `missing key` logs
- `MessagePatternTranslator: no pattern`
- targeted temporary probes when the route is still unclear

## Escalation Rule

If a candidate translation family cannot be justified from upstream evidence, do not normalize it into a broad template yet.

Instead:

1. document the unresolved family in a TODO/plan note,
2. gather upstream evidence,
3. then reopen the route/template change.
