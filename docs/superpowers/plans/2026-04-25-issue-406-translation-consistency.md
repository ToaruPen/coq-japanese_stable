# Issue #406 — Translation consistency implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 23 個の `text` フィールド差し替えで、cross-Scoped 12 skill 名の不整合・mutation-points 警告文の二重ぶれ・Cudgel と Grenades の表記揺れを解消する。

**Architecture:** Pure data fix in 5 JSON dictionaries. No new test (cross-context glossary は #409 で扱うため scope 外)。

**Tech Stack:** Edit tool で 1 edit = 1 `text` 行差し替え。`key` は不変。

**Spec:** `docs/superpowers/specs/2026-04-25-issue-406-translation-consistency-design.md`

---

## File map

| Action | Path | Responsibility |
| --- | --- | --- |
| Modify | `Mods/QudJP/Localization/Dictionaries/Scoped/ui-chargen-skill-context.ja.json` | 12 件の skill 名を Skills.jp.xml Snippet 形へ flip |
| Modify | `Mods/QudJP/Localization/Dictionaries/ui-chargen.ja.json` | mutation-points 警告文 2 件を canonical 形に統一 |
| Modify | `Mods/QudJP/Localization/Dictionaries/ui-default.ja.json` | Cudgel 武器カテゴリ context 2 件 (`棍棒` → `鈍器`) |
| Modify | `Mods/QudJP/Localization/Dictionaries/world-mods.ja.json` | Cudgel 武器カテゴリ context 6 件 (`棍棒` → `鈍器`) |
| Modify | `Mods/QudJP/Localization/Dictionaries/ui-phase3b-static.ja.json` | Grenades カテゴリ用途を `グレネード` へ統一 |

新規ファイルなし。C# 変更なし。新規テストなし。

---

### Task 1: `ui-chargen-skill-context.ja.json` の 12 skill 名を Snippet 形へ flip

**Files:**
- Modify: `Mods/QudJP/Localization/Dictionaries/Scoped/ui-chargen-skill-context.ja.json`

各 entry は 1 行 (`{ "key": "...", "context": "Chargen.SkillName", "text": "..." }`) なので、`text` 値だけを差し替える。`key` と `context` は不変。

| Line | 旧 text | 新 text |
| ---: | --- | --- |
| 12 | `弓・ライフル` | `弓とライフル` |
| 13 | `風習と民間伝承` | `慣習と伝承` |
| 14 | `獅子心` | `ライオンハート` |
| 15 | `多刀流` | `多肢格闘` |
| 16 | `説得術` | `説得` |
| 17 | `ピストル` | `拳銃術` |
| 18 | `毒耐性` | `耐毒` |
| 19 | `自己鍛錬` | `自律` |
| 20 | `盾` | `盾術` |
| 21 | `短剣` | `短剣術` |
| 22 | `軽業` | `身軽` |
| 23 | `サバイバル` | `辺境行` |

- [ ] **Step 1: 12 edit 適用**

各行 `Edit` ツール 1 回ずつ。`old_string` は entry の line 全体 (line 12 なら `    { "key": "Bow and Rifle", "context": "Chargen.SkillName", "text": "弓・ライフル" },` が unique なので surrounding context 不要)。

例 (line 12):

```
old_string: { "key": "Bow and Rifle", "context": "Chargen.SkillName", "text": "弓・ライフル" }
new_string: { "key": "Bow and Rifle", "context": "Chargen.SkillName", "text": "弓とライフル" }
```

各 entry が unique key を持つため `replace_all: false` (default) で OK。

- [ ] **Step 2: JSON parse 検証**

```bash
python3.12 -c "import json,pathlib; json.loads(pathlib.Path('Mods/QudJP/Localization/Dictionaries/Scoped/ui-chargen-skill-context.ja.json').read_text())"
```

期待: 出力なし、exit 0。

- [ ] **Step 3: 12 件すべて反映確認**

```bash
grep -nE 'Chargen.SkillName' Mods/QudJP/Localization/Dictionaries/Scoped/ui-chargen-skill-context.ja.json
```

期待: 12 行、すべての text が新 canonical 形。

---

### Task 2: `ui-chargen.ja.json` mutation-points 警告文 2 件統一

**Files:**
- Modify: `Mods/QudJP/Localization/Dictionaries/ui-chargen.ja.json`

両 entry ともに同じ canonical 形 `未使用の突然変異ポイントがあります。` に統一。`key` は変更しない。

| Line | context | 旧 text | 新 text |
| ---: | --- | --- | --- |
| 390 | `Chargen.Mutations.UnspentWarning` | `未使用の突然変異ポイントが残っています。` | `未使用の突然変異ポイントがあります。` |
| 425 | `XRL.CharacterBuilds.Qud.XRL.CharacterBuilds.Qud.QudMutationsModule` | `未使用の変異ポイントがあります。` | `未使用の突然変異ポイントがあります。` |

- [ ] **Step 1: L390 の text を flip**

`Edit`:

```
old_string: "text": "未使用の突然変異ポイントが残っています。"
new_string: "text": "未使用の突然変異ポイントがあります。"
```

`old_string` は file 内で unique (`残っています。` は L390 の text 行のみ)。

- [ ] **Step 2: L425 の text を flip**

`Edit`:

```
old_string: "text": "未使用の変異ポイントがあります。"
new_string: "text": "未使用の突然変異ポイントがあります。"
```

`old_string` は file 内で unique (`変異ポイント` は L425 の text 行のみ)。

- [ ] **Step 3: JSON parse 検証**

```bash
python3.12 -c "import json,pathlib; json.loads(pathlib.Path('Mods/QudJP/Localization/Dictionaries/ui-chargen.ja.json').read_text())"
```

期待: 出力なし、exit 0。

- [ ] **Step 4: 統一確認**

```bash
grep -nE 'unspent mutation points|突然変異ポイント' Mods/QudJP/Localization/Dictionaries/ui-chargen.ja.json
```

期待: L388 / L390 / L423 / L425 に該当、両 entry の text が `未使用の突然変異ポイントがあります。` で一致。

---

### Task 3: Cudgel 武器カテゴリ context 8 件を `棍棒` → `鈍器` に flip

**Files:**
- Modify: `Mods/QudJP/Localization/Dictionaries/ui-default.ja.json`
- Modify: `Mods/QudJP/Localization/Dictionaries/world-mods.ja.json`

維持対象 (`棍棒` のまま):
- `Skills.jp.xml` 全箇所
- `ui-skillsandpowers.ja.json` skill 名 + description
- `ui-popup.ja.json:1843` 武器の実物参照

#### `ui-default.ja.json` — 2 件

| Line | key | 旧 text | 新 text |
| ---: | --- | --- | --- |
| 846 | `Cudgel (dazes on critical hit)` (no explicit context) | `棍棒（クリティカル時に朦朧付与）` | `鈍器（クリティカル時に朦朧付与）` |
| 990 | `Cudgel (dazes on critical hit)` (context `Look.TooltipValue`) | `棍棒（クリティカル時に朦朧付与）` | `鈍器（クリティカル時に朦朧付与）` |

両 entry の text 行は完全一致 (`"text": "棍棒（クリティカル時に朦朧付与）"`)。同一 file 内で 2 occurrence あり、両方とも flip 対象なので **`replace_all: true` を使う 1 回の `Edit` で完結**。

- [ ] **Step 1: `ui-default.ja.json` で `replace_all` flip**

`Edit`:

```
old_string: "text": "棍棒（クリティカル時に朦朧付与）"
new_string: "text": "鈍器（クリティカル時に朦朧付与）"
replace_all: true
```

期待: 2 occurrence が一括で flip される。

- [ ] **Step 2: `ui-default.ja.json` JSON parse 検証**

```bash
python3.12 -c "import json,pathlib; json.loads(pathlib.Path('Mods/QudJP/Localization/Dictionaries/ui-default.ja.json').read_text())"
```

#### `world-mods.ja.json` — 6 件

flip 対象は context が `XRL.World.Parts.MeleeWeapon.GetShortDescription` の以下の text 行：

| Line | 旧 text | 新 text |
| ---: | --- | --- |
| 680 | `\n{{rules\|筋力ボーナス上限: {0}\n武器カテゴリ: 棍棒（クリティカル時に朦朧）}}` | `\n{{rules\|筋力ボーナス上限: {0}\n武器カテゴリ: 鈍器（クリティカル時に朦朧）}}` |
| 685 | `\n{{rules\|筋力ボーナス上限: なし\n武器カテゴリ: 棍棒（クリティカル時に朦朧）}}` | `\n{{rules\|筋力ボーナス上限: なし\n武器カテゴリ: 鈍器（クリティカル時に朦朧）}}` |
| 730 | `筋力ボーナス上限: {0}\n武器カテゴリ: 棍棒（クリティカル時に朦朧）` | `筋力ボーナス上限: {0}\n武器カテゴリ: 鈍器（クリティカル時に朦朧）` |
| 735 | `筋力ボーナス上限: なし\n武器カテゴリ: 棍棒（クリティカル時に朦朧）` | `筋力ボーナス上限: なし\n武器カテゴリ: 鈍器（クリティカル時に朦朧）` |
| 770 | `武器カテゴリ: 棍棒（クリティカル時に朦朧）` | `武器カテゴリ: 鈍器（クリティカル時に朦朧）` |
| 965 | `武器カテゴリ: 棍棒` | `武器カテゴリ: 鈍器` |

すべての旧 text に共通する substring `武器カテゴリ: 棍棒` を file 内で grep すると **6 件すべて該当**かつ **他 entry には出現しない**。一方で「`武器カテゴリ: 鈍器` に置換すべき同 file 内 entry は他にない」ことが事前確認済。したがって `replace_all: true` で `武器カテゴリ: 棍棒` → `武器カテゴリ: 鈍器` の 1 回 Edit で全 6 件を一括 flip 可能。

ただし注意: 旧 text に `武器カテゴリ: 棍棒（クリティカル時に朦朧）` (5 件) と `武器カテゴリ: 棍棒` (1 件 only — L965) が混在するので、前者を後者の prefix と見なし `replace_all` で `武器カテゴリ: 棍棒` を `武器カテゴリ: 鈍器` に置換すれば、6 件すべて (`〜棍棒` 部分が flip し、`（クリティカル時に朦朧）` 以降は無傷) で一括完了する。

- [ ] **Step 3: `world-mods.ja.json` で `replace_all` flip**

`Edit`:

```
old_string: 武器カテゴリ: 棍棒
new_string: 武器カテゴリ: 鈍器
replace_all: true
```

期待: 6 occurrence (L680, L685, L730, L735, L770, L965) が一括で flip。

- [ ] **Step 4: `world-mods.ja.json` JSON parse 検証**

```bash
python3.12 -c "import json,pathlib; json.loads(pathlib.Path('Mods/QudJP/Localization/Dictionaries/world-mods.ja.json').read_text())"
```

- [ ] **Step 5: flip 件数確認**

```bash
grep -cE '武器カテゴリ: 鈍器' Mods/QudJP/Localization/Dictionaries/world-mods.ja.json
grep -cE '武器カテゴリ: 棍棒' Mods/QudJP/Localization/Dictionaries/world-mods.ja.json
```

期待: 鈍器 = 6, 棍棒 = 0。

- [ ] **Step 6: 維持対象が変化していないことを確認**

```bash
grep -nE '棍棒' Mods/QudJP/Localization/Dictionaries/ui-skillsandpowers.ja.json | head -5
grep -nE '棍棒' Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json
```

期待: ui-skillsandpowers の skill 名 (L431, L436, L441 等) と ui-popup:1843 が `棍棒` のまま残っている。

---

### Task 4: Grenades カテゴリ用途を `グレネード` に flip

**Files:**
- Modify: `Mods/QudJP/Localization/Dictionaries/ui-phase3b-static.ja.json`

| Line | 旧 entry | 新 entry |
| ---: | --- | --- |
| 32 | `{ "key": "Grenades", "text": "手榴弾" },` | `{ "key": "Grenades", "text": "グレネード" },` |

- [ ] **Step 1: edit 適用**

`Edit`:

```
old_string: { "key": "Grenades", "text": "手榴弾" },
new_string: { "key": "Grenades", "text": "グレネード" },
```

- [ ] **Step 2: JSON parse 検証**

```bash
python3.12 -c "import json,pathlib; json.loads(pathlib.Path('Mods/QudJP/Localization/Dictionaries/ui-phase3b-static.ja.json').read_text())"
```

- [ ] **Step 3: 抽象用途 `手榴弾` が touch されていないことを確認**

```bash
grep -nE '手榴弾' Mods/QudJP/Localization/Dictionaries/ui-skillsandpowers.ja.json
grep -nE '手榴弾' Mods/QudJP/Localization/Skills.jp.xml
grep -nE '手榴弾' Mods/QudJP/Localization/ActivatedAbilities.jp.xml
```

期待: 各ファイル内で `手榴弾` 出現箇所は触っていない。

---

### Task 5: 全リポジトリ verification

**Files:** (none — verification only)

- [ ] **Step 1: full pytest**

```bash
uv run pytest scripts/tests/ -q
```

期待: 全 green (現在 467 passed)。今回新規テストを追加していないので件数変化なし。

- [ ] **Step 2: validate_xml strict**

```bash
python3.12 scripts/validate_xml.py Mods/QudJP/Localization --strict --warning-baseline scripts/validate_xml_warning_baseline.json
```

期待: exit 0。

- [ ] **Step 3: encoding check**

```bash
python3.12 scripts/check_encoding.py Mods/QudJP/Localization scripts
```

期待: 190 OK / 0 issues。

- [ ] **Step 4: ruff**

```bash
ruff check scripts/
```

期待: clean (今回 Python は touch していないので影響なし)。

- [ ] **Step 5: dotnet build**

```bash
dotnet build Mods/QudJP/Assemblies/QudJP.csproj
```

期待: 0 warnings, 0 errors。

- [ ] **Step 6: L1 dotnet test**

```bash
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L1
```

期待: 全 passed (現在 1182)。

- [ ] **Step 7: L2 dotnet test**

```bash
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L2
```

期待: 全 passed (現在 713)。

- [ ] **Step 8: stop**

commit しない。user が `/codex` review → `/simplify` → PR の flow を回す。

---

## Verification summary

| Check | Command | Expectation |
| --- | --- | --- |
| pytest 全体 | `uv run pytest scripts/tests/ -q` | 467 passed |
| Markup parity (#401 由来) | (上記に含む) | 124 cases pass |
| validate_xml strict | `python3.12 scripts/validate_xml.py ...` | exit 0 |
| Encoding | `python3.12 scripts/check_encoding.py ...` | clean |
| Ruff | `ruff check scripts/` | clean |
| L1 / L2 dotnet | `dotnet test ... --filter TestCategory=L1`, `--filter TestCategory=L2` | 1182 / 713 passed |
| DLL build | `dotnet build ...` | 0 warn, 0 err |

## Out-of-scope reminders

- `Bows && Rifles` weapon-category ラベル (`world-mods.ja.json:975`, `ui-default.ja.json:960`) は触らない。Cudgel split 原則どおり、weapon-category ラベルは skill 名と独立。
- `ui-options.ja.json:23,28` `Change Value` と `ui-skillsandpowers.ja.json:1219,1224` `Weathered` は意図的分化、修正対象外。
- 新規テスト contract は追加しない。cross-context glossary の機械検査は #409 (CI gate) で扱う。
- 抽象用途の `手榴弾` (Skills/Abilities/Conversations/LibraryCorpus 等 9 件) は触らない。
- Cudgel 維持対象 (`Skills.jp.xml`, `ui-skillsandpowers.ja.json`, `ui-popup.ja.json`) は触らない。
- `key` フィールドは一切変更しない。
- C# は変更しない。
