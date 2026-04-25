# Issue #401 — JSON markup token parity plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restore `{{X|...}}` color spans and `&&` / `^^` literal escapes in 49 shipped translation entries across 9 JSON dictionaries, and lock the multiset-parity invariant behind a new pytest contract.

**Architecture:** Pure data fix in 9 JSON files plus one new pytest module that owns the dict-wide markup-preservation contract for markup tokens.

**Tech Stack:** Python 3.12 + pytest, `re` for regex extraction, `json` for parsing.

**Spec:** `docs/superpowers/specs/2026-04-25-issue-401-json-markup-token-parity-design.md`

---

## File map

| Action | Path | Responsibility |
| --- | --- | --- |
| Create | `scripts/tests/test_json_markup_parity.py` | Dict-wide pytest: every entry's `text` must preserve every `{{NAME\|` opener and `&&` / `^^` literal that `key` carries (text may add more; key-side tokens must not be lost) |
| Modify | `Mods/QudJP/Localization/Dictionaries/ui-displayname-adjectives.ja.json` | 33 `text` field updates (substance-stained adjectives + nested-color adjectives) |
| Modify | `Mods/QudJP/Localization/Dictionaries/ui-chargen.ja.json` | 4 biome-village `text` updates |
| Modify | `Mods/QudJP/Localization/Dictionaries/ui-default.ja.json` | 2 `&&` literal restorations |
| Modify | `Mods/QudJP/Localization/Dictionaries/ui-options.ja.json` | 2 `&&` literal restorations |
| Modify | `Mods/QudJP/Localization/Dictionaries/ui-skillsandpowers.ja.json` | 2 `{{B\|recycling suit}}` wrapper restorations |
| Modify | `Mods/QudJP/Localization/Dictionaries/world-effects-tonics.ja.json` | 2 named-shader / color span restorations |
| Modify | `Mods/QudJP/Localization/Dictionaries/world-mods.ja.json` | 2 `&&` restorations |
| Modify | `Mods/QudJP/Localization/Dictionaries/ui-help.ja.json` | 1 `&&` restoration |
| Modify | `Mods/QudJP/Localization/Dictionaries/ui-keybinds.ja.json` | 1 `&&` restoration |

No new module files outside the test. No production C# changes.

---

### Task 1: Failing pytest

**Files:**
- Create: `scripts/tests/test_json_markup_parity.py`

- [ ] **Step 1: Write the failing test**

```python
"""Issue #401 — every JSON dictionary entry must preserve markup tokens carried by `key`.

Two invariants checked, both asymmetric (preservation, not exact equality):

- For every `{{NAME|` opener present in `key`, `text` must carry at least the same
  count under the same color/shader name (key_multiset - text_multiset must be empty).
- For every `&&` / `^^` literal escape count in `key`, `text` must carry at least
  the same count.

Adding decoration in `text` that is not in `key` is allowed — some dictionaries
(e.g. `mutation-descriptions.ja.json`, `mutation-ranktext.ja.json`) use
identifier-style keys without markup and style the rendered text. The contract is
"do not LOSE source-side tokens", not "match exactly".

The opener regex captures the name before the pipe; bare `{{phase-conjugate}}`
forms (no pipe) are intentionally ignored because the Caves of Qud runtime
rejects them. The inner content of color spans legitimately changes across
translation, so it is not compared.
"""

from __future__ import annotations

import json
import re
from collections import Counter
from pathlib import Path

import pytest

REPO_ROOT = Path(__file__).resolve().parents[2]
DICTIONARIES_ROOT = REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"

OPENER = re.compile(r"\{\{(?P<name>[^|}]+)\|")
LITERALS: tuple[str, ...] = ("&&", "^^")


def _opener_multiset(value: str) -> Counter[str]:
    return Counter(match.group("name") for match in OPENER.finditer(value))


def _literal_multiset(value: str) -> Counter[str]:
    return Counter({token: count for token in LITERALS if (count := value.count(token)) > 0})


def _all_dictionary_files() -> list[Path]:
    # Intentionally scans all *.json (not just *.ja.json): the downstream _entries
    # function filters to files with the expected key/text dictionary structure.
    files = sorted(DICTIONARIES_ROOT.rglob("*.json"))
    if not files:
        msg = (
            f"No *.json files found under {DICTIONARIES_ROOT}; "
            "DICTIONARIES_ROOT may be misconfigured or the directory is missing."
        )
        raise AssertionError(msg)
    return files


def _entries(path: Path) -> list[tuple[int, str, str]]:
    """Return [(index, key, text), ...] for every entry in a dictionary file."""
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as exc:
        rel = path.relative_to(REPO_ROOT)
        msg = f"Failed to read/parse {rel}: {exc}"
        raise ValueError(msg) from exc
    raw_entries = data.get("entries", []) if isinstance(data, dict) else data
    if not isinstance(raw_entries, list):
        return []
    pairs: list[tuple[int, str, str]] = []
    for index, entry in enumerate(raw_entries, start=1):
        if not isinstance(entry, dict):
            continue
        key = entry.get("key", "")
        text = entry.get("text", "")
        if isinstance(key, str) and isinstance(text, str):
            pairs.append((index, key, text))
    return pairs


@pytest.mark.parametrize("path", _all_dictionary_files(), ids=lambda p: p.relative_to(REPO_ROOT).as_posix())
def test_json_dictionary_markup_openers_preserved(path: Path) -> None:
    """`text` must carry at least the `{{NAME|` openers present in `key`."""
    mismatches: list[str] = []
    for index, key, text in _entries(path):
        key_sig = _opener_multiset(key)
        if not key_sig:
            continue
        text_sig = _opener_multiset(text)
        missing = key_sig - text_sig
        if missing:
            mismatches.append(
                f"  entry #{index}\n    key={key!r}\n    text={text!r}\n"
                f"    missing_openers={dict(missing)} key_openers={dict(key_sig)} text_openers={dict(text_sig)}"
            )
    assert not mismatches, f"Markup opener loss in {path.relative_to(REPO_ROOT)}:\n" + "\n".join(mismatches)


@pytest.mark.parametrize("path", _all_dictionary_files(), ids=lambda p: p.relative_to(REPO_ROOT).as_posix())
def test_json_dictionary_literal_escapes_preserved(path: Path) -> None:
    """`text` must carry at least the `&&` / `^^` literal escape counts present in `key`."""
    mismatches: list[str] = []
    for index, key, text in _entries(path):
        key_sig = _literal_multiset(key)
        if not key_sig:
            continue
        text_sig = _literal_multiset(text)
        missing = key_sig - text_sig
        if missing:
            mismatches.append(
                f"  entry #{index}\n    key={key!r}\n    text={text!r}\n"
                f"    missing_literals={dict(missing)} key_literals={dict(key_sig)} text_literals={dict(text_sig)}"
            )
    assert not mismatches, f"Literal escape loss in {path.relative_to(REPO_ROOT)}:\n" + "\n".join(mismatches)
```

- [ ] **Step 2: Run the test and confirm exactly the expected failure pattern**

Run: `uv run pytest scripts/tests/test_json_markup_parity.py -v`

Expected per-file failure breakdown (one assertion failure per file per test, listing all entries inside):

| File | opener-multiset test | literal-escape test |
| --- | :-: | :-: |
| `ui-displayname-adjectives.ja.json` | FAIL (33) | pass |
| `ui-chargen.ja.json` | FAIL (4) | pass |
| `ui-default.ja.json` | pass | FAIL (2) |
| `ui-options.ja.json` | FAIL (1, the nested case) | FAIL (2, includes one same-entry overlap) |
| `ui-skillsandpowers.ja.json` | FAIL (2) | pass |
| `world-effects-tonics.ja.json` | FAIL (2) | pass |
| `world-mods.ja.json` | pass | FAIL (2) |
| `ui-help.ja.json` | pass | FAIL (1) |
| `ui-keybinds.ja.json` | pass | FAIL (1) |
| All other 50+ files | pass | pass |

Total: ~14 parametrized cases failing across the two tests; entries-failing total 49.

If any unexpected file fails, **stop**. Report DONE_WITH_CONCERNS with the surfaced filename and entry. Do not silently absorb the new finding.

---

### Task 2: Fix `ui-displayname-adjectives.ja.json`

**Files:**
- Modify: `Mods/QudJP/Localization/Dictionaries/ui-displayname-adjectives.ja.json`

This is the largest file: 33 entries to fix. Each is a single `text` field replacement; the `key` is unchanged. Use one Edit call per entry.

The fixes split into three patterns:

**Pattern 2A — substance-stained color spans** (27 entries):

For each entry where the key is `{{COLOR|substance}}-stained` and the current text drops the wrapper, restore the wrapper around the Japanese substance name. Concrete edits:

| Line | Current text | New text |
| ---: | --- | --- |
| 588 | `コンバレセンスで染みた` | `{{C\|コンバレセンス}}で染みた` |
| 613 | `酸で染みた` | `{{G\|酸}}で染みた` |
| 628 | `どろどろに染みた` | `{{G\|どろどろ}}に染みた` |
| 643 | `インクで染みた` | `{{K\|インク}}で染みた` |
| 658 | `油で染みた` | `{{K\|油}}で染みた` |
| 668 | `軟泥で汚れた` | `{{K\|軟泥}}で汚れた` |
| 693 | `タールで染みた` | `{{K\|タール}}で染みた` |
| 708 | `樹液で染みた` | `{{W\|樹液}}で染みた` |
| 728 | `ゲルで染みた` | `{{Y\|ゲル}}で染みた` |
| 748 | `塩で染みた` | `{{Y\|塩}}で染みた` |
| 763 | `ウォームスタティックで染みた` | `{{Y\|ウォームスタティック}}で染みた` |
| 778 | `蝋で汚れた` | `{{Y\|蝋}}で汚れた` |
| 788 | `脳髄汁で染みた` | `{{brainbrine\|脳髄汁}}で染みた` |
| 798 | `サイダーで染みた` | `{{cider\|サイダー}}で染みた` |
| 813 | `クローン薬液で染みた` | `{{cloning\|クローン薬液}}で染みた` |
| 833 | `スープで染みた` | `{{c\|スープ}}で染みた` |
| 843 | `藻で染まった` | `{{g\|藻}}で染まった` |
| 858 | `スライムでぬめった` | `{{g\|スライム}}でぬめった` |
| 868 | `溶岩で汚れた` | `{{lava\|溶岩}}で汚れた` |
| 888 | `ワインで染みた` | `{{m\|ワイン}}で染みた` |
| 903 | `中性子フラックスで染みた` | `{{neutronic\|中性子フラックス}}で染みた` |
| 923 | `腐敗液で染みた` | `{{putrid\|腐敗液}}で染みた` |
| 943 | `血まみれになった` | `{{r\|血まみれ}}になった` |
| 948 | `血に染まった` | `{{r\|血}}に染まった` |
| 963 | `サンスラグで染みた` | `{{sunslag\|サンスラグ}}で染みた` |
| 973 | `はちみつで染みた` | `{{w\|はちみつ}}で染みた` |
| 983 | `汚泥で汚れた` | `{{w\|汚泥}}で汚れた` |

(The table above lists 27 entries; together with the three nested-color cases below they total 30. The remaining 3 are the legendary-style entries handled separately.)

**Pattern 2B — nested color spans** (3 entries — outer `{{Y|...}}` + inner highlight):

| Line | Current text | New text | Source key form |
| ---: | --- | --- | --- |
| 368 | `{{Y\|伝説的}}` | `{{Y\|伝説{{W\|的}}}}` | `{{Y\|lege{{W\|n}}dary}}` |
| 408 | `{{Y\|微鋸歯}}` | `{{Y\|{{R\|微}}鋸{{R\|歯}}}}` | `{{Y\|mi{{R\|c}}roserra{{R\|t}}ed}}` |
| 473 | `{{Y\|鋸歯状の}}` | `{{Y\|鋸歯{{R\|状}}の}}` | `{{Y\|serra{{R\|t}}ed}}` |

**Pattern 2C — wrong-color overrides** (3 entries — current text uses `{{Y|...}}` even though source has only `{{W|...}}` or `{{R|...}}`):

| Line | Source key | Current text | New text |
| ---: | --- | --- | --- |
| 1158 | `lege{{W\|ndary}}` | `{{Y\|伝説的}}` | `伝説{{W\|的}}` |
| 1183 | `mi{{R\|croserrated}}` | `{{Y\|微鋸歯}}` | `微{{R\|鋸歯}}` |
| 1238 | `serra{{R\|ted}}` | `{{Y\|鋸歯状の}}` | `鋸歯{{R\|状の}}` |

#### Per-entry edit steps

For each of the 33 lines listed above, perform an `Edit` tool call replacing the entire `"text": "..."` line with the new value. Each `old_string` is the unique current line; each `new_string` is the corresponding fixed line. The entries are sufficiently distinct (different substance names) that uniqueness is preserved without surrounding context.

Do not modify any other line in the file.

- [ ] **Step 1: Apply all 33 edits**

Use 33 separate Edit tool calls, one per entry. Match each `old_string` to the existing `"text": "..."` line at the indicated line number.

- [ ] **Step 2: Validate JSON parses**

Run: `python3.12 -c "import json,pathlib; json.loads(pathlib.Path('Mods/QudJP/Localization/Dictionaries/ui-displayname-adjectives.ja.json').read_text())"`

Expected: no output, exit 0.

- [ ] **Step 3: Re-run the markup-parity test**

Run: `uv run pytest scripts/tests/test_json_markup_parity.py::test_json_dictionary_markup_openers_preserved -v 2>&1 | tail -30`

Expected: `ui-displayname-adjectives.ja.json` parametrized case PASSES. Other files still fail.

---

### Task 3: Fix the 8 smaller files (16 edits)

**Files:** Modify each of:
- `Mods/QudJP/Localization/Dictionaries/ui-chargen.ja.json` (4 edits)
- `Mods/QudJP/Localization/Dictionaries/ui-default.ja.json` (2 edits)
- `Mods/QudJP/Localization/Dictionaries/ui-options.ja.json` (2 edits)
- `Mods/QudJP/Localization/Dictionaries/ui-skillsandpowers.ja.json` (2 edits)
- `Mods/QudJP/Localization/Dictionaries/world-effects-tonics.ja.json` (2 edits)
- `Mods/QudJP/Localization/Dictionaries/world-mods.ja.json` (2 edits)
- `Mods/QudJP/Localization/Dictionaries/ui-help.ja.json` (1 edit)
- `Mods/QudJP/Localization/Dictionaries/ui-keybinds.ja.json` (1 edit)

#### `ui-chargen.ja.json` — 4 biome village entries

| Line | Source key | Current text | New text |
| ---: | --- | --- | --- |
| 363 | `{{G\|salt marsh}}\nvillage` | `塩性湿地の村` | `{{G\|塩性湿地}}の村` |
| 368 | `{{Y\|salt dunes}}\nvillage` | `塩砂丘の村` | `{{Y\|塩砂丘}}の村` |
| 373 | `{{W\|desert canyon}}\nvillage` | `砂漠峡谷の村` | `{{W\|砂漠峡谷}}の村` |
| 378 | `{{Y\|hill}}\nvillage` | `丘の村` | `{{Y\|丘}}の村` |

The source `\n` is dropped intentionally — Japanese reads naturally inline as `{{COLOR|biome}}の村`.

#### `ui-default.ja.json` — 2 `&&` restorations

| Line | Current text | New text |
| ---: | --- | --- |
| 25 | `能力値と能力` | `能力値 && 能力` |
| 958 | `弓・ライフル` | `弓 && ライフル` |

#### `ui-options.ja.json` — 2 entries (one literal-only, one mixed)

| Line | Current text | New text |
| ---: | --- | --- |
| 108 | `キーボード＆マウス` | `キーボード && マウス` |
| 128 | `{{C\|設定中の入力デバイス:}} {{c\|キーボード＆マウス}}` | `{{C\|設定中の入力デバイス:}} {{c\|キーボード && マウス}}` |

#### `ui-skillsandpowers.ja.json` — 2 `{{B|recycling suit}}` wrappers

| Line | Source key | Current text | New text |
| ---: | --- | --- | --- |
| 1074 | `Starts with a {{B\|recycling suit}}` | `リサイクルスーツを所持して開始` | `{{B\|リサイクルスーツ}}を所持して開始` |
| 1964 | `{{c\|?}} Starts with a {{B\|recycling suit}}` | `{{c\|?}} リサイクルスーツを所持して開始` | `{{c\|?}} {{B\|リサイクルスーツ}}を所持して開始` |

#### `world-effects-tonics.ja.json` — 2 named-shader / color spans

| Line | Source key | Current text | New text |
| ---: | --- | --- | --- |
| 82 | `under the effects of {{rubbergum\|rubbergum}} tonic` | `ラバーガムトニックの効果を受けている` | `{{rubbergum\|ラバーガム}}トニックの効果を受けている` |
| 112 | `under the effects of {{Y\|salve}} tonic` | `サルヴトニックの効果を受けている` | `{{Y\|サルヴ}}トニックの効果を受けている` |

#### `world-mods.ja.json` — 2 `&&` restorations

| Line | Current text | New text |
| ---: | --- | --- |
| 974 | `武器カテゴリ: 弓・ライフル` | `武器カテゴリ: 弓 && ライフル` |
| 1014 | `武器カテゴリ: 弓・ライフル` | `武器カテゴリ: 弓 && ライフル` |

(The two entries differ in their `context` field — melee vs. missile — but their `text` is identical.)

#### `ui-help.ja.json` — 1 `&&`

| Line | Current text | New text |
| ---: | --- | --- |
| 17 | `キーボード＆マウス` | `キーボード && マウス` |

#### `ui-keybinds.ja.json` — 1 `&&`

| Line | Current text | New text |
| ---: | --- | --- |
| 13 | `キーボード＆マウス` | `キーボード && マウス` |

#### Edit steps

- [ ] **Step 1: Apply all 16 edits across the 8 files**

Use one Edit call per `text` line. Each `old_string` is the unique current `"text": "..."` line; each `new_string` is the fixed value. For files where two entries have identical current text (e.g. `world-mods.ja.json:974,1014`), use `replace_all: true` on the first call so both entries are updated, since both need the same fix.

Specifically for `world-mods.ja.json`: the `"text": "武器カテゴリ: 弓・ライフル"` line appears twice with identical content. A single Edit with `replace_all: true` handles both.

For all other files, `replace_all: false` (default) is correct — every `old_string` is unique.

- [ ] **Step 2: Validate JSON for each file**

Run:

```bash
for f in ui-chargen ui-default ui-options ui-skillsandpowers world-effects-tonics world-mods ui-help ui-keybinds; do
  python3.12 -c "import json,pathlib; json.loads(pathlib.Path('Mods/QudJP/Localization/Dictionaries/$f.ja.json').read_text())" && echo "$f: ok"
done
```

Expected: each prints `<name>: ok`.

- [ ] **Step 3: Re-run the markup-parity test**

Run: `uv run pytest scripts/tests/test_json_markup_parity.py -v`

Expected: every parametrized case PASSES (both opener-multiset test and literal-escape test green across all dictionaries).

If any case still fails, do not widen scope. Report which file/entry and stop.

---

### Task 4: Full verification

**Files:**
- (none — verification only)

- [ ] **Step 1: Run all repo checks**

Run each in order; all must succeed:

```bash
uv run pytest scripts/tests/ -q
python3.12 scripts/validate_xml.py Mods/QudJP/Localization --strict --warning-baseline scripts/validate_xml_warning_baseline.json
python3.12 scripts/check_encoding.py Mods/QudJP/Localization scripts
ruff check scripts/
dotnet build Mods/QudJP/Assemblies/QudJP.csproj
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L1
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L2
```

Expected:
- pytest: all green; markup-parity test contributes ~120 parametrized cases (62 files × 2 tests) all green.
- validate_xml: exit 0, no new warnings.
- check_encoding: 190 OK, 0 issues.
- ruff: clean.
- dotnet build: 0 warnings, 0 errors.
- L1 / L2 dotnet tests: green (no count change since we did not touch C#).

If any fails, stop, report, do not widen scope.

- [ ] **Step 2: Stop**

Do not commit. The user reviews the diff and runs the `/codex` review → `/simplify` → PR flow.

---

## Verification summary

After Task 4:

| Check | Command | Expectation |
| --- | --- | --- |
| Markup parity | `uv run pytest scripts/tests/test_json_markup_parity.py -v` | every parametrized case green |
| Full pytest | `uv run pytest scripts/tests/ -q` | all green |
| Strict XML validation | `python3.12 scripts/validate_xml.py ... --strict ...` | exit 0 |
| Encoding | `python3.12 scripts/check_encoding.py ...` | clean |
| Ruff | `ruff check scripts/` | clean |
| L1 / L2 dotnet | `dotnet test ... --filter TestCategory=L1`, `--filter TestCategory=L2` | all green |
| DLL build | `dotnet build ...` | 0 warnings, 0 errors |

## Out-of-scope reminders

- Do NOT touch `mutation-descriptions.ja.json` or `mutation-ranktext.ja.json`. Their keys are identifier-style (no markup), so the new test correctly skips them.
- Do NOT remove the bare `{{phase-conjugate}}` entry at `ui-displayname-adjectives.ja.json:58`. Its cleanup belongs to a separate issue.
- Do NOT extend the test to other markup classes (`&X` color codes, `=variable=` slots). Those are #404, #402, and #409.
- Do NOT change `key` fields anywhere.
- Do NOT modify any C# code.
