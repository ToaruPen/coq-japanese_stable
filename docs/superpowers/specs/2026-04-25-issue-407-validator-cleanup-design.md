# Issue #407 — Validator false-positive cleanup + TombExteriorWall_SW dedup

## Why

`scripts/validate_xml.py` は親要素ごとに `Name` または `ID` 属性のみで兄弟重複を検出する汎用ロジックを持つ。これにより `Worlds.jp.xml` の同 Name 別 zone (Level/x/y で識別)、`Quests.jp.xml` の同 Name 別 step (ID/Value で識別)、`Conversations.jp.xml` の条件分岐 node、`Naming.jp.xml` の重み付け反復候補など、**XML 構造的に正当な多義パターン** が大量に false-positive 警告として報告される。現状はこれらすべて `validate_xml_warning_baseline.json` 242 件で抑止しているが、結果として #404 で発見された `Creatures.jp.xml:5574` の unbalanced color のような **真の警告** も baseline に紛れて見逃されやすくなっている。

加えて、`_find_empty_text_elements` は `element.text.strip() == ""` で whitespace-only text を任意タグで検出する bug がある。`<object Name="X" Inherits="Widget" Replace="true">\n  </object>` のような inheritance-only stub に whitespace 改行があると trigger し、Walls/Widgets/Items 系で大量の empty-text 警告 (現 baseline 39 件中 26+ 件) を生み出している。

最後に `Widgets.jp.xml` には `TombExteriorWall_SW` の byte-equal 重複定義が L650-652 と L671-673 にある (1 件のみの実 redundancy)。

本 issue は (1) 重複検出ロジックを汎用検出から **schema-aware allowlist** へ刷新、(2) 空テキスト検出を `<text>` タグに限定、(3) 実 redundancy 1 件を削除、(4) baseline を再生成、(5) 新ロジックの test coverage を追加する。

## What

### コード修正 — `scripts/validate_xml.py`

#### 1. `_find_duplicate_siblings` の刷新

汎用 `Name`/`ID` 検出を撤去し、**explicit allowlist** に基づく検出に変更。allowlist は `(parent_tag, child_tag, key_attribute)` の tuple リスト。指定された組み合わせに該当する兄弟だけを重複対象とする。

最小 allowlist (本 PR の対象):

```python
DUPLICATE_DETECTION_RULES: tuple[tuple[str, str, str], ...] = (
    ("objects", "object", "Name"),
)
```

このルールは `<objects>` を parent に持つ `<object>` 兄弟のうち同じ `Name` 属性を持つものを flag する。`Mods/QudJP/Localization/ObjectBlueprints/*.jp.xml` は root tag が `<objects>` なので parent.tag == 'objects' で機能する (Codex 検証済)。

将来必要になれば、新しい parent/child/key 組み合わせを explicit に追加する想定。汎用検出は廃止する。

#### 2. `_find_empty_text_elements` の `<text>` タグ限定

現状コード:

```python
if element.text is None:
    if element.tag == "text":
        warnings.append(...)
    continue
if element.text.strip() == "":
    warnings.append(...)  # bug: ANY tag triggers
```

新コード:

```python
if element.tag != "text":
    continue
if element.text is None or element.text.strip() == "":
    warnings.append(...)
```

これにより `<object>` / `<command>` / `<help>` / `<start>` / `<node>` / `<module>` の空本文は **検出対象から外れる**。これらは継承・stub・属性主体の構造で空本文が正常なため、Codex 検証で「除外して問題なし」と確認済。`<command>` / `<help>` 等を真に検出復活させたいケースが将来出れば、別 rule (例: `_find_empty_required_content`) として独立実装する余地を残す。

### データ修正 — `Widgets.jp.xml`

`Mods/QudJP/Localization/ObjectBlueprints/Widgets.jp.xml` L671-673 の `TombExteriorWall_SW` 重複定義を削除。L650-652 のオリジナル定義を残す。両者は byte-equal payload (`<object Name="TombExteriorWall_SW" Inherits="Widget" Replace="true">` + `<part Name="MapChunkPlacement" Map="preset_tile_chunks/TombExteriorWall_SW.rpm" Width="9" Height="6" />`) なので削除に副作用なし。

### Baseline 再生成 — `validate_xml_warning_baseline.json`

新ロジック + Tomb 削除後、現 242 件 → **2 件** に削減 (Codex 検証):

1. `Mods/QudJP/Localization/Conversations.jp.xml:261` の `<text />` (genuine empty translation text — 本物の警告で対処余地)
2. `Mods/QudJP/Localization/ObjectBlueprints/Creatures.jp.xml:5574` の `Unbalanced color code at line 5574` (#404 で対応予定の本物の警告)

両方とも **本物の警告** であり、baseline で抑止する性質ではなく将来 PR で実コードを修正すべきもの。本 PR では「再生成された baseline に 2 件残るが、いずれも actionable な real warning」と明示し、後続 issue (#404 / Conversations 該当エントリの empty 解消) に追跡を委譲する。

### テスト追加 — `scripts/tests/test_validate_xml.py`

7 ケース追加 (Codex 推奨に従う):

1. `test_duplicate_siblings_with_distinguishing_attribute_not_flagged` — 同 Name + 異なる Level/x/y は warning なし (新検出は `Name` 単独では trigger しない)
2. `test_byte_equal_object_siblings_flagged` — `<objects>` 配下の同 Name `<object>` 兄弟は warning (TombExteriorWall_SW の regression)
3. `test_duplicate_conditional_nodes_not_flagged` — 同 ID + 異なる IfHaveState は warning なし
4. `test_repeated_naming_entries_not_flagged` — `<prefix Name="ニ"/>` × 2 は warning なし
5. `test_empty_text_only_flagged_for_text_tag` — `<text>   </text>` は warning
6. `test_empty_text_self_closing_flagged` — `<text/>` (self-close) も warning
7. `test_empty_object_stub_not_flagged` — `<object Inherits="X" Replace="true"></object>` は warning なし

既存 9 ケースのうち、`test_duplicate_id_in_same_parent_reports_warning` と `test_empty_text_element_reports_warning` は新ロジック下で挙動が変わる可能性あり (汎用 ID 検出廃止 / empty-text 限定)。新仕様に合わせて test を更新する:

- `test_duplicate_id_in_same_parent_reports_warning` → `<root><item ID="A"/><item ID="A"/></root>` は新ロジックでは flag されない。これを「汎用 ID 検出は廃止された」regression として書き換え (assert no warning)、または allowlist 適用パターンに変更。
- `test_empty_text_element_reports_warning` → `<root><text>   </text></root>` の場合、tag は `text` なので flag される。挙動維持。

## How

実装順序 (Codex 推奨 + TDD):

1. **failing tests 追加** (Red): 7 新ケース + 既存 2 ケース更新を追加し、`uv run pytest scripts/tests/test_validate_xml.py` で 7 件以上 fail を確認。
2. **`_find_duplicate_siblings` 刷新** (Green 1): allowlist ベース実装に書き換え、汎用 ID/Name 検出を削除。
3. **`_find_empty_text_elements` 制限** (Green 2): `tag == 'text'` 限定。
4. **TombExteriorWall_SW 削除**: Widgets.jp.xml L671-673 の 3 行削除。
5. **pytest 全 green 確認**: `uv run pytest scripts/tests/test_validate_xml.py`。
6. **baseline 再生成**: `python3.12 scripts/validate_xml.py Mods/QudJP/Localization --write-warning-baseline scripts/validate_xml_warning_baseline.json`。残存 2 件を確認。
7. **strict validation 確認**: `python3.12 scripts/validate_xml.py Mods/QudJP/Localization --strict --warning-baseline scripts/validate_xml_warning_baseline.json` で exit 0。
8. **全リポジトリ verification**: pytest 全体 / encoding / ruff / dotnet build / L1 / L2。

タスク分割:

- Task 1: failing tests 追加 (Red 確認)
- Task 2: validator 刷新 (Green、両関数まとめて)
- Task 3: TombExteriorWall_SW 削除
- Task 4: baseline 再生成 + 残存 2 件確認
- Task 5: 全リポジトリ verification

## Verification

```bash
uv run pytest scripts/tests/test_validate_xml.py -v
uv run pytest scripts/tests/ -q
python3.12 scripts/validate_xml.py Mods/QudJP/Localization --strict --warning-baseline scripts/validate_xml_warning_baseline.json
python3.12 scripts/check_encoding.py Mods/QudJP/Localization scripts
ruff check scripts/
dotnet build Mods/QudJP/Assemblies/QudJP.csproj
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L1
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L2
```

すべて green でなければならない。新 baseline は 2 件 (Codex 予測)、いずれも actionable な real warning。

## Risks

- **Allowlist が空に等しい (1 entry のみ)**: 汎用検出を撤去するため、現在 baseline 抑止されている 200 件超の duplicate 警告は全て silent になる。これは意図 (false-positive 削減) であり Codex 検証で「混在する real bug なし」と確認済。ただし将来 base-game 更新で意図せぬ重複が混入した場合、新検出に乗らず気付きにくくなる可能性。リスク低 (XML 構造変更は base-game 更新時に必ず手作業 review される)。
- **`<command>` / `<help>` の empty 検出復活ニーズ**: 将来「`<command>` の本文が抜けている bug を検出したい」要望が出た場合は、別 rule (`_find_empty_required_content` など、required-content tag の whitelist 方式) として独立実装する。本 PR では含めない。
- **新 baseline の 2 件 (real warnings)**: `Conversations.jp.xml:261 <text />` は元から baselined だったため state 変化なし。`Creatures.jp.xml:5574` unbalanced color は #404 で対応予定。両方とも本 PR scope 外で、baseline に残ることは正当。
- **テストが多数追加される**: 7 ケース新規 + 2 ケース更新で合計 16 ケース。既存テスト構造に従えばパターンマッチで実装可能、リスク低。

## Out of scope

- **未使用 `_format_element_descriptor`** や他の utility 整理: 汎用検出を撤去すると `_format_element_descriptor` の使用箇所が `_find_empty_text_elements` 1 箇所だけになる。除去や inline 化は検討余地あるが本 PR では touch しない (refactor の最小化)。
- **`<command>` / `<help>` の empty 検出復活**: 将来別 rule で実装。
- **`Conversations.jp.xml:261 <text />` の解消**: 別 issue 扱い。本 PR では baseline 残存。
- **`Creatures.jp.xml:5574` unbalanced color の解消**: #404 で扱う。
- **C# コード変更**: 不要。
- **`validate_xml.py` の CLI / `run_validation` API 変更**: 不要、内部関数のみ。
- **新規 dictionary 追加 / 翻訳変更**: 不要。

## References

- Codex 諮問 1 (validator design): allowlist 方式 + tag-limited empty-text 推奨
- Codex 諮問 2 (verify allowlist + baseline prediction): `(objects, object, Name)` 1 entry で十分、新 baseline 2 件予測
- Issue #407 全文 (parent: #400)
- 関連: #404 (Creatures unbalanced color の本物警告解消)
