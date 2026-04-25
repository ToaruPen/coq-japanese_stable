# Issue #401 — Restore `{{X|...}}` color spans and `&&` / `^^` literal escapes in shipped JSON dictionaries

## Why

Forty-nine shipped translation entries across nine dictionary files dropped or replaced markup tokens that the Caves of Qud renderer treats as load-bearing:

- `{{X|...}}` color spans (and named-shader spans like `{{cider|...}}`, `{{phase-conjugate|...}}`) determine on-screen colour. Dropping the wrapper turns coloured nouns into uncoloured text — visible to the player on item descriptions, chargen tiles, and skill descriptions.
- `&&` and `^^` are the markup engine's literal escapes for `&` and `^`. The Japanese translator habitually replaced `&&` with the full-width `＆`, which the renderer does not recognise as an escape — and crucially, the source-string lookup contract requires multiset parity.

Per `docs/RULES.md`: "Preserve markup and placeholders exactly, including `{{...}}`, `&X`, `^x`, `&&`, `^^`, and `=variable.name=`." The current state violates that invariant.

This is data-only oversight: there is no runtime pipeline that strips the markup. Decompiled `Markup.Transform`, `Sidebar.FormatToRTF`, and `Qud.UI.RTF.FormatToRTF` are syntax-based and not locale-aware. The Translator passes through the dictionary `text` field unchanged. Evidence: many other dictionary entries with the same token shapes (e.g. the Preacher and trade entries we already exercise) render correctly. The 49 here are translator slips that escaped review because there was no automated parity gate.

## What

### Data fixes

Forty-nine entries change. `key` stays as-is; only the `text` value is updated. Counts per file:

| File | Entries to fix |
| --- | ---: |
| `Mods/QudJP/Localization/Dictionaries/ui-displayname-adjectives.ja.json` | 33 |
| `Mods/QudJP/Localization/Dictionaries/ui-chargen.ja.json` | 4 |
| `Mods/QudJP/Localization/Dictionaries/ui-default.ja.json` | 2 |
| `Mods/QudJP/Localization/Dictionaries/ui-options.ja.json` | 2 |
| `Mods/QudJP/Localization/Dictionaries/ui-skillsandpowers.ja.json` | 2 |
| `Mods/QudJP/Localization/Dictionaries/world-effects-tonics.ja.json` | 2 |
| `Mods/QudJP/Localization/Dictionaries/world-mods.ja.json` | 2 |
| `Mods/QudJP/Localization/Dictionaries/ui-help.ja.json` | 1 |
| `Mods/QudJP/Localization/Dictionaries/ui-keybinds.ja.json` | 1 |

The complete per-line fix list lives in [the implementation plan](../plans/2026-04-25-issue-401-json-markup-token-parity.md). Each fix follows one of three patterns:

1. **Restore wrapper around the substance / item name**, e.g. `{{G|acid}}-stained` → currently `酸で染みた` → fixed to `{{G|酸}}で染みた`.
2. **Reinsert literal `&&`** (and `^^` if it occurred), e.g. `Bows && Rifles` → currently `弓・ライフル` or `弓＆ライフル` → fixed to `弓 && ライフル`.
3. **Restore nested color spans** in adjectives like `lege{{W|n}}dary`, mapping the highlighted English character to a semantically equivalent highlighted Japanese character (e.g. one-char emphasis on `的`).

The chargen biome village entries (`{{G|salt marsh}}\nvillage` etc.) drop the source `\n` to render inline as `{{G|塩性湿地}}の村`. The two-line layout makes sense in English where the biome adjective leads; in Japanese the genitive `の村` reads naturally inline. Codex confirms this is acceptable.

### New test contract

Add `scripts/tests/test_json_markup_parity.py`. It scans every `*.json` under `Mods/QudJP/Localization/Dictionaries/` (recursively, so `Scoped/` is included); the `_entries` helper filters to files with the expected key/text dictionary structure. For each entry:

- `text` must contain at least the same occurrences of each `{{NAME|` opener prefix as `key` does (i.e. `key_multiset - text_multiset` must be empty). Text may add opener prefixes not present in `key`.
- `text` must carry at least the same count of `&&` literals as `key`. Text may add more.
- `text` must carry at least the same count of `^^` literals as `key`. Text may add more.

The opener-prefix regex is `\{\{(?P<name>[^|}]+)\|` — captures the color/shader name before the pipe. This deliberately ignores bare `{{phase-conjugate}}` (no pipe) which the runtime rejects anyway, and ignores the inner content (which legitimately changes across translation).

The test fails before the fixes (49 cases) and passes after the fixes. It runs as part of the standard pytest suite. No allowlist is needed.

## How

1. Write the failing pytest first.
2. Confirm exactly 49 entries fail across the 9 expected files; halt if a 50th surfaces.
3. Apply the 49 `text`-field edits.
4. Re-run pytest; expect green.
5. Run the full repo verification suite.

The implementation is split into per-file tasks for review granularity:

- Task 1: failing pytest
- Task 2: `ui-displayname-adjectives.ja.json` (33 edits — the long file)
- Task 3: smaller files batch (`ui-chargen`, `ui-default`, `ui-options`, `ui-skillsandpowers`, `world-effects-tonics`, `world-mods`, `ui-help`, `ui-keybinds`) — 16 edits across 8 files
- Task 4: full verification

## Verification

```bash
uv run pytest scripts/tests/test_json_markup_parity.py -v
uv run pytest scripts/tests/ -q
python3.12 scripts/validate_xml.py Mods/QudJP/Localization --strict --warning-baseline scripts/validate_xml_warning_baseline.json
python3.12 scripts/check_encoding.py Mods/QudJP/Localization scripts
ruff check scripts/
dotnet build Mods/QudJP/Assemblies/QudJP.csproj
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L1
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L2
```

All must pass.

## Out of scope

- **`{{phase-conjugate}}` bare entry at `ui-displayname-adjectives.ja.json:58`** — the no-pipe form is not a runtime template. The real form `{{K|phase-conjugate}}` is already correctly translated at line 443. The bare entry is stale data; cleaning it requires a separate review of whether the entry should be removed entirely or remapped. File a follow-up issue.
- **`ui-messagelog-world.ja.json:993`** — the key is extraction-time truncated to `{{G|You prepare·` and the text contains additional markup. This is not parity loss; it is a dynamic-message extraction problem. Defer to #409 with a baseline reason.
- **Translation tone polish** — improving Japanese fluency on entries that already preserve markup. Not blocker.
- **CI workflow plumbing** — landing the new test in CI workflow files is part of #409.
- **`mutation-descriptions.ja.json` and `mutation-ranktext.ja.json`** — these dictionaries use identifier-style keys (e.g. `mutation:Adrenal Control`) where the `key` carries no markup; the markup lives only in `text`. The new test correctly skips them because the `key`-side multiset is empty and so is unconstrained.
- **C# patch changes** — none required. Verified via decompiled `Markup.Transform` / `Sidebar.FormatToRTF` that the renderer is syntax-based.

## Risks

- **Per-line accuracy at scale.** 49 individual fixes is the largest data PR in this round. The pytest is the safety net: if a single fix gets the markup wrong, that entry stays red.
- **`&&` vs. `＆` reading**: Japanese readers may prefer `・` or `＆` aesthetically, but the renderer's contract requires `&&` (which renders as `&`). Codex confirms via `Sidebar.cs:650-679`. Translation tone is secondary.
- **Nested color preservation in adjectives**: the highlighted-character replacement is a creative-translation call (e.g. `lege{{W|n}}dary` → `伝説{{W|的}}`). The test only enforces multiset parity, so any reasonable Japanese arrangement will pass. The proposed mapping is one defensible choice; future translators can adjust without the test going red.
- **Stale entries surface as soon as the parity test runs across the rest of the dict tree** — most are filtered correctly, but if any genuine pre-existing parity issue surfaces beyond the documented 49, we will catch it during Task 1 verification and triage at that point.
