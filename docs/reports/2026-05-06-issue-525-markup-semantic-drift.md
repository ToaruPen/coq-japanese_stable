# Issue #525 Markup Semantic Drift Classification

## Runtime Evidence

Fresh triage against `~/Library/Logs/Freehold Games/CavesOfQud/Player.log`
reproduced the issue count:

| Phase F bucket | Count |
| --- | ---: |
| total | 1582 |
| dynamic_text_probe | 381 |
| sink_observe | 249 |
| final_output_probe | 952 |
| markup_semantic_drift | 107 |

All 107 entries have:

- `sink=UITextSkinTranslationPatch`
- `route=UITextSkinTranslationPatch`
- `detail=Skipped`
- `translation_status=skipped`
- `markup_status=matched`
- `markup_span_status=matched`
- `source_text_sample == final_text_sample`

This means the observed strings reached `UITextSkin` already in that markup
shape. The evidence does not show QudJP final-output mutation.

## Producer Families

| Count | Previous raw flags | Producer/screen family | Classification |
| ---: | --- | --- | --- |
| 92 | `empty_qud_wrapper,unclosed_qud_scope` | `Qud.UI.WorldGenerationScreen` progress bar (`ProgressBasis` plus `_IncrementProgress()` inserting <code>}}>{{K&#124;</code>) | acceptable upstream relaxed markup |
| 13 | `repeated_same_qud_scope_start` | `Qud.UI.SelectableTextMenuItem.SelectChanged()` selected row wrapper around hotkey/menu labels | acceptable upstream menu-state markup |
| 2 | `repeated_same_qud_scope_start` | `Qud.UI.PopupMessage`/conversation body wrapper overlapping already yellow conversation text | acceptable upstream wrapper overlap; untranslated English is separate localization evidence |

## Static Producer Evidence

The classification is not based only on `Player.log` samples. Static inspection
of the decompiled game source identifies the producer expressions that create
the observed markup before QudJP sees it:

- `Qud.UI.WorldGenerationScreen.ProgressBasis` defines the progress bar with
  leading `{{Y|}}` and a trailing relaxed `{{Y|}}` marker. `_IncrementProgress()`
  then inserts `}}>{{K|` into that string and calls `progressText.SetText(text)`.
- `Qud.UI.SelectableTextMenuItem.SelectChanged()` calls
  `item.SetText("{{W|" + itemText + "}}")` for selected rows. Popup and bottom
  context menu item text already contains inner hotkey markup such as
  `{{W|[y]}} {{y|Yes}}`, so selected rows naturally become nested
  same-shader wrappers.
- `XRL.UI.Popup.GetPopupOption()` builds hotkey options as
  `{{W|[key]}} {{y|option}}`.
- `Qud.UI.PopupMessage` defines standard buttons using the same inner hotkey
  markup and sets message bodies with `Message.SetText("{{y|" + message + "}}")`.
  If the incoming conversation text is already yellow, this creates the
  same-shader body overlap.

The static route therefore proves these families are upstream UI composition,
not QudJP restore corruption.

## Full UITextSkin Surface Verification

It is feasible to verify the whole `UITextSkin` surface, but it is a broader
inventory task than the 107 drift rows in issue #525. The right unit is not
"all runtime strings" as literal values, because many `UITextSkin.SetText`
arguments are data-bound values such as `message`, `itemText`, `sb.ToString()`,
`GO.DisplayName`, or `playerStringData[...]`. Static analysis can prove the
producer callsites and construction routes; it cannot enumerate every runtime
value without complementary observations or focused fixtures.

Current static bounds from the local decompiled `1.0.4` source:

| Probe | Count | Notes |
| --- | ---: | --- |
| syntax `.SetText(...)` calls | 315 | name-only upper bound; includes same-name methods such as `HPBar.SetText`, `UIHotkeySkin.SetText`, and `TooltipTrigger.SetText` |
| `TextConstructionInventory` `SetText` text constructions | 201 | Roslyn syntax inventory of string literals, concatenations, and interpolations directly inside `SetText` arguments |
| `TextConstructionInventory` `DirectTextAssignment` text constructions | 154 | adjacent `UITextSkin.text = ...; Apply()`-style route candidates, not all necessarily `UITextSkin` |

An authoritative full audit should add a symbol-backed Roslyn inventory for:

- resolved calls to `XRL.UI.UITextSkin.SetText(string)`;
- direct writes to `XRL.UI.UITextSkin.text` followed by `Apply()` or equivalent
  render paths;
- owner/screen grouping, argument shape classification, markup-risk flags, and
  route ownership status;
- explicit `resolved` / `candidate` / `unresolved` symbol status so same-name
  methods do not become silent false positives.

That scanner would let future work claim full `UITextSkin` surface coverage.
For #525, the evidence needed is narrower: every currently observed drift row
maps to the three upstream relaxed-markup families above. The diagnostic fix is
now semantic rather than broadly shape-specific: empty Qud wrappers and
same-shader nested wrappers are idempotent color composition and are not
reported as drift. Unclosed Qud scopes remain reportable except for the
statically proven `WorldGenerationScreen` progress-bar shape.

## Fix Boundary

`UITextSkinTranslationPatch` remains observation/pass-through for these strings.
The fix is limited to `MarkupSemanticDiagnostics`: Qud color wrappers are now
judged by semantic risk instead of raw shape. Idempotent color composition is
clean, while malformed cases such as literal shader fragments, bracket-close
drift, generic unclosed Qud scopes, unmatched Qud closes, unmatched TMP closes,
and unclosed TMP color tags remain reportable. The only unclosed Qud exception
is the statically proven world-generation progress-bar family.

## Relationship To #459

Issue `#459` tracks broad Restore ownership and producer-route gaps. Issue `#525` is narrower:
it handles current `UITextSkinTranslationPatch` runtime semantic drift evidence.
The 107 entries classified here are not new Restore ownership targets because
they are upstream UI wrapper/progress-bar syntax and QudJP is not changing the
final text.
