# Issue #406 — Translation consistency: cross-Scoped skill names, mutation-points wording, Cudgel / Grenades terminology

## Why

QudJP は context スコープごとに翻訳が積み上がってきたため、同じ英語キー (または同じ意味概念) に異なる日本語訳が当たっているケースが残っている。プレイヤーから見て同じ skill が Chargen 画面と Skills & Powers 画面で別表記になり、「違う skill か？」と誤読される。Steam リリース水準の polish として揃える。

具体的には以下の三系統の不整合：

1. **Cross-Scoped 12 件のスキル名**: `ui-chargen-skill-context.ja.json` (context: `Chargen.SkillName`) と `ui-skillsandpowers-skill-names.ja.json` (context: `TMP.Skill Name`) で訳がずれている。
2. **同一辞書内 1 件の mutation-points 警告文**: `ui-chargen.ja.json` の L388 と L423 が同一英語キーに対し別訳。
3. **用語表記揺れ**: `Cudgel` と `Grenades` が context 横断で混在。

## What

### データ修正 — 計 23 件 / 5 ファイル

| File | 種別 | 件数 |
| --- | --- | ---: |
| `Mods/QudJP/Localization/Dictionaries/Scoped/ui-chargen-skill-context.ja.json` | 12 cross-Scoped skill 名 flip | 12 |
| `Mods/QudJP/Localization/Dictionaries/ui-chargen.ja.json` | mutation-points 2 件統一 | 2 |
| `Mods/QudJP/Localization/Dictionaries/ui-default.ja.json` | Cudgel 武器カテゴリ flip | 2 |
| `Mods/QudJP/Localization/Dictionaries/world-mods.ja.json` | Cudgel 武器カテゴリ flip | 6 |
| `Mods/QudJP/Localization/Dictionaries/ui-phase3b-static.ja.json` | Grenades カテゴリ統一 | 1 |

### 1. Cross-Scoped 12 skill 名 — Skills.jp.xml `Snippet` 形に揃える

`Skills.jp.xml` の `Snippet` が canonical な skill 表示名であり、現状 `ui-skillsandpowers-skill-names.ja.json` が一致している。Chargen 側を Snippet 形に flip する。

| 英語キー | 旧 (Chargen) | 新 = Snippet 形 |
| --- | --- | --- |
| `Bow and Rifle` | 弓・ライフル | 弓とライフル |
| `Customs and Folklore` | 風習と民間伝承 | 慣習と伝承 |
| `Lionheart` | 獅子心 | ライオンハート |
| `Multiweapon Fighting` | 多刀流 | 多肢格闘 |
| `Persuasion` | 説得術 | 説得 |
| `Pistol` | ピストル | 拳銃術 |
| `Poison Tolerance` | 毒耐性 | 耐毒 |
| `Self-discipline` | 自己鍛錬 | 自律 |
| `Shield` | 盾 | 盾術 |
| `Short Blade` | 短剣 | 短剣術 |
| `Spry` | 軽業 | 身軽 |
| `Wayfaring` | サバイバル | 辺境行 |

### 2. Mutation-points warning 文の統一

`ui-chargen.ja.json` で同一英語キーが二箇所に存在し、両方とも別訳になっている。canonical = **`未使用の突然変異ポイントがあります。`**

- L388 (context `Chargen.Mutations.UnspentWarning`): `未使用の突然変異ポイントが残っています。` → `未使用の突然変異ポイントがあります。`
- L423 (context `XRL.CharacterBuilds.Qud.XRL.CharacterBuilds.Qud.QudMutationsModule`): `未使用の変異ポイントがあります。` → `未使用の突然変異ポイントがあります。`

`Mutation Points` 系は他箇所でも `突然変異ポイント` が主流。文末は警告として `あります` の方が簡潔。

なお issue #406 が同一辞書内ぶれとして列挙していた他 2 件は調査の結果、修正対象外とする：

- `ui-options.ja.json:23,28` `Change Value`: context が `OptionsSliderControl.CHANGE_VALUE` と `ARROWS_CHANGE_VALUE` で異なり、`値を変更` と `値を変更（カーソル）` の分化は意図的。
- `ui-skillsandpowers.ja.json:1219,1224` `Weathered`: context が `TMP.Description` (`風雨に耐えた`) と `TMP.Skill Name` (`風雪錬成`) で意図的に分化。

### 3. Cudgel — 武器カテゴリ vs スキル名の context split

Cudgel は文脈によって canonical を分ける：

- **武器カテゴリラベル** (`Look.TooltipValue` / `Weapon Class:` プレフィックス文脈): **鈍器**
- **スキル名 / スキル説明 / 武器の実物参照**: **棍棒** (`Skills.jp.xml` `Snippet="棍棒"` が source of truth)

これは英語が `Cudgel` ひとつでカテゴリと skill を兼ねているのを、日本語の語感に合わせて分けるもの。`Grenades` と同じ「表示面の役割差に基づく split」(Codex 諮問でも条件付き defensible と確認)。

#### Flip 対象 (棍棒 → 鈍器): 8 箇所

`ui-default.ja.json`:
- L846 `key="Cudgel (dazes on critical hit)"`: `棍棒（クリティカル時に朦朧付与）` → `鈍器（クリティカル時に朦朧付与）`
- L990 同 key の `Look.TooltipValue` バリアント: 同じ flip

`world-mods.ja.json` (すべて context `XRL.World.Parts.MeleeWeapon.GetShortDescription`):
- L680, L685: `武器カテゴリ: 棍棒（クリティカル時に朦朧）` → `武器カテゴリ: 鈍器（クリティカル時に朦朧）` (rule-block 内)
- L730, L735: 同上
- L770: 同上
- L965 `key="Weapon Class: Cudgel"`: `武器カテゴリ: 棍棒` → `武器カテゴリ: 鈍器`

#### 維持 (棍棒 のまま)

- `Skills.jp.xml` 全箇所 (skill 名 + skill description 内の `棍棒で〜`)
- `ui-skillsandpowers.ja.json` の skill 名 + description (例 `棍棒の習熟` `棍棒で突撃`)
- `ui-popup.ja.json:1843` `主手に棍棒を装備しなければならない` (武器の実物参照)

### 4. Grenades — カテゴリ vs prose の split

Grenades は intentional split として運用する：

- **アイテム固有名 / カテゴリラベル**: **グレネード** (`Items.jp.xml` 60 件すべて、`ui-default.ja.json:172` と一致)
- **抽象概念 / 行為説明 / prose**: **手榴弾** (`ActivatedAbilities.jp.xml`, `Skills.jp.xml`, `Conversations.jp.xml`, `LibraryCorpus.ja.json` 等)

Issue が指摘している outlier は `ui-phase3b-static.ja.json:32` のみ。これは category-list 用途なので `グレネード` に flip：

- `ui-phase3b-static.ja.json:32` `{ "key": "Grenades", "text": "手榴弾" }` → `{ "key": "Grenades", "text": "グレネード" }`

### Out of scope

- **`Bows && Rifles` weapon-category ラベル** (`world-mods.ja.json:975` / `ui-default.ja.json:960`): 英語キーが `Bow and Rifle` (skill 名) と異なる別 key。Cudgel split 原則 (カテゴリと skill 名は別系統) に従い触らない。skill 名は `弓とライフル` に統一されるが、weapon-category ラベルは `弓・ライフル` 維持で原則整合。
- **同一辞書内 `Change Value` / `Weathered`**: 上記の通り意図的分化、修正対象外。
- **手榴弾 ↔ グレネード の context-split を runtime で検証する仕組み**: Cudgel/Grenades のような語彙 glossary をプログラムで強制する CI gate は #409 で扱う。今回は data-only 修正に留める。
- **C# 変更**: 不要。すべて dictionary `text` フィールドの差し替えで完結する。

## How

1. 全 23 edit を Edit ツールで適用 (1 edit = 1 `text` 行の書き換え)。`key` は触らない。
2. JSON parse 検証: 5 ファイルすべて `python3.12 -c "import json,pathlib; json.loads(...)"` で OK。
3. 既存テストスイート全通過: pytest / validate_xml / check_encoding / ruff / dotnet build / L1 / L2。
4. テスト contract の追加なし — 今回の修正は context-aware な glossary 検査が必要なため、機械的 invariant としては #409 で実装する。

タスク分割：

- Task 1: `ui-chargen-skill-context.ja.json` の 12 件 flip
- Task 2: `ui-chargen.ja.json` の mutation-points 2 件統一
- Task 3: Cudgel 8 件 flip (`ui-default.ja.json` 2 + `world-mods.ja.json` 6)
- Task 4: Grenades 1 件 flip (`ui-phase3b-static.ja.json:32`)
- Task 5: 全ファイル JSON parse 検証 + 全リポジトリ verification

## Verification

```bash
uv run pytest scripts/tests/ -q
python3.12 scripts/validate_xml.py Mods/QudJP/Localization --strict --warning-baseline scripts/validate_xml_warning_baseline.json
python3.12 scripts/check_encoding.py Mods/QudJP/Localization scripts
ruff check scripts/
dotnet build Mods/QudJP/Assemblies/QudJP.csproj
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L1
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L2
```

すべて green でなければならない。

## Risks

- **Skill 名統一の文化的判断**: `Lionheart` `獅子心` vs `ライオンハート` のような美学差は議論の余地あり。Codex 諮問で `Skills.jp.xml` Snippet 採用が defensible と確認済 (Snippet が source of truth として既に稼働している事実が決め手)。
- **Cudgel split の player 認知**: Cudgel スキル所持時に「武器カテゴリ: 鈍器」と「《棍棒》スキル」が同画面で混在する場面ありうる。Codex 諮問で「Weapon Class: 系を全て鈍器に統一すれば defensible」確認済。本修正はその前提を満たす。
- **Cross-context glossary の機械検査が無い**: 今回の修正後も新規 PR で再ぶれが起きうる。#409 の CI gate で fix。

## References

- Codex 諮問 1 回目: `Skills.jp.xml` Snippet 形が cross-Scoped skill 名の canonical / `突然変異ポイントがあります。` / Cudgel `棍棒` 統一推奨 (user override) / Grenades split 容認
- Codex 諮問 2 回目 (Cudgel split second-opinion): user 提案の文脈 split を「条件付き容認」、border-line ケースの判断を提示
- Issue #406 全文 (parent: #400)
