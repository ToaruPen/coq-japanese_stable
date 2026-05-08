# TMP And Unity UI Runtime Rendering Notes

This document records derived QudJP review knowledge from runtime investigation
of Caves of Qud 1.0.4 UI rendering. It is intended for maintainers and
CodeRabbit-facing review context.

Do not add copied game source, decompiled method bodies, or large symbol
inventories here. Keep this file limited to behavioral contracts, review rules,
and evidence patterns.

## Scope

These notes apply to QudJP patches that touch TextMeshPro, Unity UI
`CanvasRenderer`, ModelShark tooltips, or runtime UI text repair. They are not a
general license to mutate final UI sinks. Route ownership rules in
`CODERABBIT.md` and `docs/RULES.md` still apply.

## Runtime Rendering Contracts

TMP mesh state is necessary but not sufficient evidence that text is visible.
When investigating invisible UI text, check these layers separately:

| Layer | Useful evidence | What it proves |
| --- | --- | --- |
| Text content | non-empty TMP text or legacy UI text | The route populated the field. |
| TMP mesh | `characterCount`, `pageCount`, `rect` | TMP generated glyph geometry. |
| Visibility limits | `maxVisibleCharacters`, `maxVisibleLines`, `pageToDisplay` | TMP is not hiding text via paging or line limits. |
| Canvas renderer | `CanvasRenderer.GetAlpha()`, `cull`, active state | Unity UI will actually draw the generated geometry. |
| Lifecycle | tooltip display/hide callers | The object is not being hidden after repair. |

Reviewers should not accept "TMP has characters" as complete visibility proof.
If a bug says text flashes and disappears, distinguish generated-but-hidden
state from missing glyph or wrapping failures.

## Tooltip-Specific Findings

ModelShark tooltip display has a relevant warmup/display sequence:

- tooltip warmup activates the tooltip object and can set child
  `CanvasRenderer` alpha to `0`;
- tooltip display later restores renderer alpha;
- QudJP font/material/TMP repair may run between these lifecycle steps;
- a tooltip can contain valid TMP text and mesh while still not being visible if
  active child `CanvasRenderer` instances remain at alpha `0`.

For PolatLooker-style L-key looker tooltips, `ForceHideTooltip()` in logs is not
automatically a bug. The looker flow explicitly hides the current tooltip during
target changes and on exit. Review runtime evidence by caller:

| Caller family | Default interpretation |
| --- | --- |
| `TooltipManager.Update` | Suspicious for keyboard looker if it hides while the looker remains active. |
| `Look.ShowLooker` target-change path | Usually expected when the selected target changes. |
| `Look.HideTooltips` exit path | Expected when leaving the looker or closing UI. |

The L-key looker route also needs the tooltip trigger and tooltip instance to
agree on stay-open state. Setting only an `alwaysStayOpen`-style tooltip flag is
not enough when the manager checks trigger fields for automatic hide behavior.

## Review Guidance

Flag TMP or tooltip fixes that:

- rely only on `characterCount` or non-empty text as proof of visible output;
- read `Graphic.color.a` as the primary runtime visibility signal when
  `CanvasRenderer.GetAlpha()` is the actual draw-state evidence needed;
- add permanent high-volume runtime logs in hot UI paths;
- collect `StackTrace` during normal tooltip rendering without a debug gate;
- repair broad tooltip or final UI sink behavior when the bug is scoped to a
  known route such as PolatLooker;
- keep diagnostic patches after the root cause is understood.

Accept focused repairs when they:

- target a known tooltip route or owner surface;
- preserve fallback behavior and wrap Harmony patch bodies in crash-safe
  `try`/`catch`;
- include L2G target-resolution coverage for real game methods;
- keep runtime diagnostics temporary, bounded, and free of raw body text;
- use `CanvasRenderer.GetAlpha()` / `SetAlpha()` when fixing Unity UI draw
  state, with the required Unity assembly references declared explicitly.

## Runtime Evidence Pattern

A healthy PolatLooker display after repair should have evidence equivalent to:

- QudJP assembly loaded from local `StreamingAssets/Mods/QudJP`;
- the relevant Harmony patches applied;
- PolatLooker repair occurred for active TMP fields;
- tooltip display state has `CanvasRenderer` alpha restored to `1`;
- `cull` count is zero for visible tooltip renderers;
- no automatic hide from `TooltipManager.Update` while the looker should remain
  visible.

Do not print raw tooltip bodies in diagnostic logs. Prefer bounded structural
evidence: route name, object path, text length, character count, page count,
active/enabled flags, rect size, renderer alpha range, cull count, and caller
family.
