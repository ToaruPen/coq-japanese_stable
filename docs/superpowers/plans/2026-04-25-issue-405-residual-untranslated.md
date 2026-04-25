# Issue #405 — Residual untranslated implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 30 件の Books.jp.xml `Title` 属性 + 1 件の Subtypes.jp.xml `<extrainfo>` を日本語化し、Steam リリース水準の completeness を達成する。

**Architecture:** Pure data fix in 2 XML files. No new test (XML attribute 翻訳の機械検証は #409 で扱う)。

**Tech Stack:** Edit ツールで XML attribute 値の差し替え。`xmllint --noout` で構文検証。`validate_xml.py --strict` で merge consistency 検証。

**Spec:** `docs/superpowers/specs/2026-04-25-issue-405-residual-untranslated-design.md`

---

## File map

| Action | Path | Responsibility |
| --- | --- | --- |
| Modify | `Mods/QudJP/Localization/Books.jp.xml` | 19 wrapped 英語 Title 日本語化 + 10 raw ID を `{{W\|<JP>}}` 形式に置換 |
| Modify | `Mods/QudJP/Localization/Subtypes.jp.xml` | L200 Arconaut の `<extrainfo>` 日本語化 |

新規ファイルなし。C# 変更なし。新規テストなし。

---

### Task 1: Books.jp.xml — 19 wrapped 英語タイトル日本語化

**Files:**
- Modify: `Mods/QudJP/Localization/Books.jp.xml`

各 entry は単一行の `<book ID="..." Title="{{W|<English>}}" ...>` で構成。`Title="{{W|<English>}}"` の部分のみを `Title="{{W|<Japanese>}}"` に差し替え。`ID` と他の属性 (`Format`, `Margins` 等) は不変。

| Line | ID | 旧 Title 部分 | 新 Title 部分 |
| ---: | --- | --- | --- |
| 5 | DagashasSpur | `Title="{{W\|Dagasha Remembers}}"` | `Title="{{W\|ダガーシャは覚えている}}"` |
| 17 | FearinBeyLah | `Title="{{W\|A Tale of Fear in Bey Lah}}"` | `Title="{{W\|ベイ・ラーの恐怖譚}}"` |
| 141 | LoveinBeyLah | `Title="{{W\|A Tale of Love in Bey Lah}}"` | `Title="{{W\|ベイ・ラーの恋愛譚}}"` |
| 557 | Skybear | `Title="{{W\|Song of the Sky-Bear}}"` | `Title="{{W\|天空の熊の歌}}"` |
| 578 | MimicandMadpole | `Title="{{W\|The Mimic and the Madpole}}"` | `Title="{{W\|ミミックとマッドポール}}"` |
| 598 | TeleporterOrbs | `Title="{{W\|The Girl in the Sky}}"` | `Title="{{W\|空の娘}}"` |
| 620 | Sonnet | `Title="{{W\|a crumpled sheet of paper}}"` | `Title="{{W\|くしゃくしゃの紙片}}"` |
| 642 | DarkCalculus | `Title="{{W\|On the Origins and Nature of the Dark Calculus}}"` | `Title="{{W\|暗き微積の起源と本質}}"` |
| 658 | CrimeandPunishment | `Title="{{W\|Crime and Punishment}}"` | `Title="{{W\|罪と罰}}"` |
| 664 | AphorismsAboutBirds | `Title="{{W\|Aphorisms about Birds}}"` | `Title="{{W\|鳥についての箴言}}"` |
| 702 | Animals | `Title="{{W\|On Humanoid Mimicry of Animals and Plants}}"` | `Title="{{W\|ヒューマノイドによる動植物模倣について}}"` |
| 716 | EntropytoHierarchy | `Title="{{W\|From Entropy to Hierarchy}}"` | `Title="{{W\|エントロピーからヒエラルキーへ}}"` |
| 735 | BloodstainedSheaf | `Title="{{W\|A sheaf of bloodstained, goatskin parchment}}"` | `Title="{{W\|血染めの山羊皮紙の束}}"` |
| 755 | Sheaf1 | `Title="{{W\|A sheaf of tattered parchment}}"` | `Title="{{W\|ぼろぼろの羊皮紙の束}}"` |
| 783 | TornGraphPaper | `Title="{{W\|a sheet of torn graph paper}}"` | `Title="{{W\|破れた方眼紙}}"` |
| 908 | Corpus | `Title="{{W\|Corpus}}"` | `Title="{{W\|コルプス}}"` |
| 970 | Across1 | `Title="{{W\|Across Moghra'yi, Vol. I: The Sunderlies}}"` | `Title="{{W\|モグラヤイを越えて 第1巻：サンダリーズ}}"` |
| 1371 | Across2 | `Title="{{W\|Across Moghra'yi, Vol. II: Athenreach}}"` | `Title="{{W\|モグラヤイを越えて 第2巻：アセンリーチ}}"` |
| 1402 | Across3 | `Title="{{W\|Across Moghra'yi, Vol. III: Oth, the Free City}}"` | `Title="{{W\|モグラヤイを越えて 第3巻：自由都市オース}}"` |

L294 CyberIntro `Title="{{W|125810239481203-41023}}"` は数字列のまま維持 (spec の policy)、edit 不要。

- [ ] **Step 1: 19 件の Edit 適用**

各 entry に対し `Edit` ツールを 1 回ずつ。`old_string` は `Title="{{W|<English>}}"` の部分文字列 (file 内で unique なので surrounding context 不要)。

例 (L5 DagashasSpur):

```
old_string: Title="{{W|Dagasha Remembers}}"
new_string: Title="{{W|ダガーシャは覚えている}}"
```

各 Title 文字列は file 内で unique (タイトルそのものが識別子) なので `replace_all: false` (default) で問題なし。

- [ ] **Step 2: XML 構文検証**

```bash
xmllint --noout Mods/QudJP/Localization/Books.jp.xml
echo "exit=$?"
```

期待: exit 0、エラー出力なし。

- [ ] **Step 3: wrapped 英語タイトルが残っていないことを確認**

```bash
python3.12 - <<'PY'
import re
text = open('Mods/QudJP/Localization/Books.jp.xml').read()
EN = re.compile(r'^[ -~]+$')
remaining = []
for i, line in enumerate(text.splitlines(), 1):
    m = re.search(r'Title="\{\{W\|([^}]+)\}\}"', line)
    if m and EN.match(m.group(1)):
        remaining.append((i, m.group(1)))
print(f"Remaining wrapped English titles: {len(remaining)}")
for i, t in remaining:
    print(f"  L{i}: {t}")
PY
```

期待: 「Remaining wrapped English titles: 1」と出力され、残るのは L294 CyberIntro `125810239481203-41023` のみ (数字列維持の policy)。

---

### Task 2: Books.jp.xml — 10 raw ID を `{{W|<JP>}}` 形式に置換

**Files:**
- Modify: `Mods/QudJP/Localization/Books.jp.xml`

`Title="<RawID>"` を `Title="{{W|<Japanese>}}"` に置換。`{{W|...}}` wrapper を新たに追加することで他の翻訳済 Title と表示揃え。

| Line | 旧 Title 部分 | 新 Title 部分 |
| ---: | --- | --- |
| 42 | `Title="HighSermon"` | `Title="{{W\|シェキーナの大説教}}"` |
| 135 | `Title="Klanq"` | `Title="{{W\|クランク！}}"` |
| 153 | `Title="Preacher1"` | `Title="{{W\|銀の父祖への賛美}}"` |
| 180 | `Title="Preacher2"` | `Title="{{W\|腐蝕したベテルへの戒め}}"` |
| 216 | `Title="Preacher3"` | `Title="{{W\|聖なる結合}}"` |
| 249 | `Title="Preacher4"` | `Title="{{W\|啓発の儀}}"` |
| 279 | `Title="TemplarDomesticant"` | `Title="{{W\|ニューファーザーへの呼び声}}"` |
| 354 | `Title="AlchemistMutterings"` | `Title="{{W\|錬金術師のぶつぶつ}}"` |
| 375 | `Title="Quotes"` | `Title="{{W\|引用集}}"` |
| 1900 | `Title="EndCredits"` | `Title="{{W\|制作クレジット}}"` |

注意: `Title="HighSermon"` は同じ ID `<book ID="HighSermon" Title="HighSermon">` の構造で出現するが、`Title=` の値部分のみ差し替えるので `ID="HighSermon"` 側は変更されない。`old_string` を `Title="HighSermon"` だけにすれば file 内で unique。

- [ ] **Step 1: 10 件の Edit 適用**

各 entry に対し `Edit` ツールを 1 回ずつ。例 (L42 HighSermon):

```
old_string: Title="HighSermon"
new_string: Title="{{W|シェキーナの大説教}}"
```

各 raw ID は file 内で `Title="<RawID>"` 形式で 1 回だけ出現するので unique。

- [ ] **Step 2: XML 構文検証**

```bash
xmllint --noout Mods/QudJP/Localization/Books.jp.xml
echo "exit=$?"
```

期待: exit 0。

- [ ] **Step 3: raw ID が残っていないことを確認**

```bash
python3.12 - <<'PY'
import re
text = open('Mods/QudJP/Localization/Books.jp.xml').read()
remaining = []
for i, line in enumerate(text.splitlines(), 1):
    m = re.search(r'<book\s[^>]*Title="([^"]+)"', line)
    if m:
        title = m.group(1)
        if not title.startswith('{{W|') and not title.endswith('}}'):
            remaining.append((i, title))
print(f"Remaining raw-ID Titles (unwrapped): {len(remaining)}")
for i, t in remaining:
    print(f"  L{i}: {t}")
PY
```

期待: 「Remaining raw-ID Titles (unwrapped): 0」。

- [ ] **Step 4: 全 Title 件数確認**

```bash
python3.12 - <<'PY'
import re
text = open('Mods/QudJP/Localization/Books.jp.xml').read()
total = 0
wrapped_jp = 0
wrapped_en = 0
EN = re.compile(r'^[ -~]+$')
for line in text.splitlines():
    m = re.search(r'Title="([^"]+)"', line)
    if not m:
        continue
    total += 1
    title = m.group(1)
    inner = re.match(r'^\{\{W\|(.+)\}\}$', title)
    if inner:
        if EN.match(inner.group(1)):
            wrapped_en += 1
        else:
            wrapped_jp += 1
print(f"Total Titles: {total}, wrapped_jp: {wrapped_jp}, wrapped_en: {wrapped_en}")
PY
```

期待: `Total Titles: 56, wrapped_jp: 55, wrapped_en: 1` (CyberIntro 1 件のみ英語数字列維持)。

---

### Task 3: Subtypes.jp.xml — Arconaut extrainfo 日本語化

**Files:**
- Modify: `Mods/QudJP/Localization/Subtypes.jp.xml`

| Line | 旧 | 新 |
| ---: | --- | --- |
| 200 | `<extrainfo>Starts with random junk and artifacts</extrainfo>` | `<extrainfo>ランダムなガラクタとアーティファクトを所持して開始</extrainfo>` |

L199 `<removeextrainfo>Starts with random junk and artifacts</removeextrainfo>` は **触らない** (XML merge 命令で base 英語と byte-equal 必須)。

- [ ] **Step 1: edit 適用**

`Edit`:

```
old_string: <extrainfo>Starts with random junk and artifacts</extrainfo>
new_string: <extrainfo>ランダムなガラクタとアーティファクトを所持して開始</extrainfo>
```

`old_string` は file 内で unique (`<removeextrainfo>` は `<` 直後の要素名が異なるため別文字列)。

- [ ] **Step 2: XML 構文検証**

```bash
xmllint --noout Mods/QudJP/Localization/Subtypes.jp.xml
echo "exit=$?"
```

期待: exit 0。

- [ ] **Step 3: 修正反映確認**

```bash
grep -nE '<extrainfo>|<removeextrainfo>.*Starts with random junk' Mods/QudJP/Localization/Subtypes.jp.xml | head
```

期待:
- L199 `<removeextrainfo>Starts with random junk and artifacts</removeextrainfo>` が残存
- L200 `<extrainfo>ランダムなガラクタとアーティファクトを所持して開始</extrainfo>` に置換済

- [ ] **Step 4: 残存英語 extrainfo がないことを確認**

```bash
python3.12 - <<'PY'
import re
text = open('Mods/QudJP/Localization/Subtypes.jp.xml').read()
EN = re.compile(r'^[ -~]+$')
remaining = []
for i, line in enumerate(text.splitlines(), 1):
    m = re.search(r'<extrainfo>([^<]+)</extrainfo>', line)
    if m and EN.match(m.group(1)):
        remaining.append((i, m.group(1)))
print(f"Remaining English extrainfo: {len(remaining)}")
for i, t in remaining:
    print(f"  L{i}: {t}")
PY
```

期待: 「Remaining English extrainfo: 0」。

---

### Task 4: 全リポジトリ verification

**Files:** (none — verification only)

- [ ] **Step 1: full pytest**

```bash
uv run pytest scripts/tests/ -q
```

期待: 全 green (現在 343 passed)。今回新規テストを追加していないので件数変化なし。

- [ ] **Step 2: validate_xml strict**

```bash
python3.12 scripts/validate_xml.py Mods/QudJP/Localization --strict --warning-baseline scripts/validate_xml_warning_baseline.json
```

期待: exit 0。raw ID Title を `{{W|<JP>}}` に置換するので、validate_xml の `Unbalanced color code` 警告が 0 増加で済む (`{{W|...}}` は閉じ括弧含めて balanced)。

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

- [ ] **Step 8: sporadic 4 件が touch されていないことを確認**

```bash
grep -nE '"-{0} Quickness"|"VISAGE"|"TerrainJoppa"|"TerrainKyakukya"|"TerrainPalladiumReef 3l"' \
  Mods/QudJP/Localization/Dictionaries/world-effects-status.ja.json \
  Mods/QudJP/Localization/Dictionaries/ui-displayname-adjectives.ja.json \
  Mods/QudJP/Localization/Dictionaries/ui-journal.ja.json
```

期待: 4 件 (0 件かも — `-{0}` のエスケープ次第) いずれも intentional pass-through として現状維持。git diff にこれらファイルが含まれないこと。

- [ ] **Step 9: stop**

commit しない。user が `/codex` review → PR の flow を回す (純データ XML PR なので simplify はスキップ可能)。

---

## Verification summary

| Check | Command | Expectation |
| --- | --- | --- |
| XML 構文 (Books) | `xmllint --noout Mods/QudJP/Localization/Books.jp.xml` | exit 0 |
| XML 構文 (Subtypes) | `xmllint --noout Mods/QudJP/Localization/Subtypes.jp.xml` | exit 0 |
| pytest 全体 | `uv run pytest scripts/tests/ -q` | 343 passed |
| validate_xml strict | `python3.12 scripts/validate_xml.py ...` | exit 0 |
| Encoding | `python3.12 scripts/check_encoding.py ...` | clean |
| Ruff | `ruff check scripts/` | clean |
| L1 / L2 dotnet | `dotnet test ... --filter TestCategory=L1`, `--filter TestCategory=L2` | 1182 / 713 passed |
| DLL build | `dotnet build ...` | 0 warn, 0 err |
| Sporadic 維持 | `grep` 4 件 | 現状維持、git diff に未出 |

## Out-of-scope reminders

- Sporadic 4 件 (`-{0} Quickness`, `VISAGE`, 3 `Terrain*`) は intentional pass-through、touch しない。
- `<removeextrainfo>` 要素は base 英語と byte-equal 必須、touch しない。
- L294 CyberIntro `{{W|125810239481203-41023}}` は数字列維持、edit 不要。
- `<page>` 内の本文は今回 touch しない (既訳済)。
- `Saad Amus` の `サード`/`サアド` 表記揺れは別 issue として follow-up。
- 新規テスト contract は追加しない。XML attribute 翻訳の機械検査は #409 (CI gate) で扱う。
- C# 変更なし。
