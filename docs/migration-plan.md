# レガシー翻訳XML監査・移行計画

- 生成日時: `2026-03-10T16:06:40.840605+00:00`
- 監査対象: `/Users/sankenbisha/Dev/Caves-of-Qud_Japanese_legacy/Mods/QudJP/Localization/`
- 現行ゲームデータ: `/Users/sankenbisha/Library/Application Support/Steam/steamapps/common/Caves of Qud/CoQ.app/Contents/Resources/Data/StreamingAssets/Base/`
- 対象ゲームバージョン: `2.0.4`

## 1. 監査サマリー

| 項目 | 値 |
| --- | ---: |
| XMLファイル数 | 35 |
| XML総行数 | 66306 |
| XMLパース成功 | 35 |
| XMLパース失敗 | 0 |
| エンコーディング問題（BOM含む） | 15 |
| モジバケ検出ファイル | 0 |
| 辞書JSONファイル数 | 35 |
| 辞書JSON総行数 | 32836 |
| 辞書JSON妥当 | 35 |
| 辞書JSON不正 | 0 |

## 2. ファイル別ステータス

| ファイル | 行数 | encoding(file) | BOM | XML | モジバケ | 問題 |
| --- | ---: | --- | --- | --- | --- | --- |
| `ActivatedAbilities.jp.xml` | 1734 | Unicode text, UTF-8 (with BOM) text | Yes | OK | No | utf8_bom_present |
| `Books.jp.xml` | 2098 | XML 1.0 document text, Unicode text, UTF-8 (with BOM) text, with very long lines (318) | Yes | OK | No | utf8_bom_present |
| `ChiliadFactions.jp.xml` | 752 | XML 1.0 document text, Unicode text, UTF-8 text | No | OK | No | なし |
| `Commands.jp.xml` | 861 | XML 1.0 document text, Unicode text, UTF-8 text | No | OK | No | なし |
| `Conversations.jp.xml` | 13184 | XML 1.0 document text, Unicode text, UTF-8 (with BOM) text, with very long lines (373) | Yes | OK | No | utf8_bom_present |
| `EmbarkModules.jp.xml` | 473 | XML 1.0 document text, Unicode text, UTF-8 (with BOM) text, with very long lines (761) | Yes | OK | No | utf8_bom_present |
| `Factions.jp.xml` | 137 | XML 1.0 document text, Unicode text, UTF-8 text | No | OK | No | なし |
| `Genotypes.jp.xml` | 47 | XML 1.0 document text, Unicode text, UTF-8 text, with very long lines (454) | No | OK | No | なし |
| `HiddenConversations.jp.xml` | 2168 | XML 1.0 document text, Unicode text, UTF-8 (with BOM) text | Yes | OK | No | utf8_bom_present |
| `HiddenMutations.jp.xml` | 59 | XML 1.0 document text, Unicode text, UTF-8 text | No | OK | No | なし |
| `Manual.jp.xml` | 7 | XML 1.0 document text, ASCII text | No | OK | No | なし |
| `Mods.jp.xml` | 86 | XML 1.0 document text, Unicode text, UTF-8 text, with very long lines (328) | No | OK | No | なし |
| `Mutations.jp.xml` | 119 | XML 1.0 document text, Unicode text, UTF-8 text | No | OK | No | なし |
| `ObjectBlueprints/Creatures.jp.xml` | 14101 | XML 1.0 document text, Unicode text, UTF-8 (with BOM) text, with very long lines (409) | Yes | OK | No | utf8_bom_present |
| `ObjectBlueprints/Data.jp.xml` | 357 | XML 1.0 document text, Unicode text, UTF-8 text | No | OK | No | なし |
| `ObjectBlueprints/Foods.jp.xml` | 1352 | XML 1.0 document text, Unicode text, UTF-8 (with BOM) text | Yes | OK | No | utf8_bom_present |
| `ObjectBlueprints/Furniture.jp.xml` | 5457 | XML 1.0 document text, Unicode text, UTF-8 text, with very long lines (1080) | No | OK | No | なし |
| `ObjectBlueprints/HiddenObjects.jp.xml` | 2454 | XML 1.0 document text, Unicode text, UTF-8 text, with very long lines (544) | No | OK | No | なし |
| `ObjectBlueprints/Items.jp.xml` | 10996 | XML 1.0 document text, Unicode text, UTF-8 (with BOM) text, with very long lines (535) | Yes | OK | No | utf8_bom_present |
| `ObjectBlueprints/PhysicalPhenomena.jp.xml` | 757 | XML 1.0 document text, Unicode text, UTF-8 (with BOM) text | Yes | OK | No | utf8_bom_present |
| `ObjectBlueprints/RootObjects.jp.xml` | 13 | XML 1.0 document text, Unicode text, UTF-8 text | No | OK | No | なし |
| `ObjectBlueprints/Staging.jp.xml` | 2 | XML 1.0 document text, ASCII text | No | OK | No | なし |
| `ObjectBlueprints/TutorialStaging.jp.xml` | 99 | XML 1.0 document text, Unicode text, UTF-8 text | No | OK | No | なし |
| `ObjectBlueprints/Walls.jp.xml` | 2348 | XML 1.0 document text, Unicode text, UTF-8 text | No | OK | No | なし |
| `ObjectBlueprints/Widgets.jp.xml` | 916 | XML 1.0 document text, Unicode text, UTF-8 (with BOM) text, with very long lines (325) | Yes | OK | No | utf8_bom_present |
| `ObjectBlueprints/WorldTerrain.jp.xml` | 587 | XML 1.0 document text, Unicode text, UTF-8 (with BOM) text, with very long lines (380) | Yes | OK | No | utf8_bom_present |
| `ObjectBlueprints/ZoneTerrain.jp.xml` | 1228 | XML 1.0 document text, Unicode text, UTF-8 (with BOM) text, with very long lines (317) | Yes | OK | No | utf8_bom_present |
| `Options.jp.xml` | 229 | XML 1.0 document text, Unicode text, UTF-8 (with BOM) text | Yes | OK | No | utf8_bom_present |
| `Quests.jp.xml` | 502 | XML 1.0 document text, Unicode text, UTF-8 text | No | OK | No | なし |
| `Relics.jp.xml` | 85 | XML 1.0 document text, ASCII text | No | OK | No | なし |
| `Skills.jp.xml` | 168 | XML 1.0 document text, Unicode text, UTF-8 text, with very long lines (368) | No | OK | No | なし |
| `SparkingBaetyls.jp.xml` | 82 | XML 1.0 document text, Unicode text, UTF-8 text | No | OK | No | なし |
| `Subtypes.jp.xml` | 332 | XML 1.0 document text, Unicode text, UTF-8 (with BOM) text | Yes | OK | No | utf8_bom_present |
| `WishCommands.jp.xml` | 17 | XML 1.0 document text, Unicode text, UTF-8 text | No | OK | No | なし |
| `Worlds.jp.xml` | 2499 | XML 1.0 document text, Unicode text, UTF-8 (with BOM) text, with very long lines (498) | Yes | OK | No | utf8_bom_present |

## 3. 推奨移行順（大規模・影響大を先行）

1. `ObjectBlueprints/Creatures.jp.xml` (14,101行)
2. `Conversations.jp.xml` (13,184行)
3. `ObjectBlueprints/Items.jp.xml` (10,996行)
4. `ObjectBlueprints/Furniture.jp.xml` (5,457行)
5. `Books.jp.xml` (2,098行)
6. 残りカテゴリ（Options/Skills/Mutations 等の高カバレッジ群）

## 4. 既知課題と修正要求

- XML側: BOM付きファイル `15` 件。移行時に UTF-8 (BOMなし) へ正規化が必要。
- XML側: モジバケ指標文字（`繧`,`縺`,`驕`,`蜒`）は 0 件。
- XML側: ASCII判定ファイル（`Manual.jp.xml`,`Relics.jp.xml`,`ObjectBlueprints/Staging.jp.xml`）はUTF-8互換だが、統一のためUTF-8明示で再保存推奨。
- Books比較: 現行 `Books.xml` はXML不正文字参照を含むため、ID抽出に regex フォールバックを使用。
- Dictionary側: BOM付きJSON `2` 件（`Dictionaries/mutation-ranktext.ja.json`, `Dictionaries/world-parts.ja.json`）。移行時にBOM除去が必要。
- 用語集: `glossary.csv` は 83 エントリ、列 `English, Japanese, Short, Notes, Status`、移行利用可否は `True`。

## 5. 移行戦略（実施手順）

1. レガシーXML/JSONを複製せず監査結果に基づき対象を選定する。
2. 移行時に全ファイルを UTF-8 (BOMなし) / LF へ正規化する。
3. XMLはカテゴリごとに現行BaseデータとID照合し、未訳IDを埋める。
4. Dictionary JSONはBOM除去後にスキーマ（`meta`,`entries`,`rules`中心）を維持して取り込む。
5. 取り込み後に再度 `ET.parse` / JSON parse / mojibake検査を実行し、差分レポートを更新する。

## 6. カバレッジ要点

- ObjectBlueprints: matched 4020/5220 (77.01%), 未訳 1200, 削除/改名 0
- Conversations: matched 195/200 (97.5%), 未訳 5, 削除/改名 0
- Options: matched 187/187 (100.0%), 未訳 0, 削除/改名 6
- Skills: matched 21/21 (100.0%), 未訳 0, 削除/改名 0
- Mutations: matched 82/82 (100.0%), 未訳 0, 削除/改名 0
- Books: matched 53/53 (100.0%), 未訳 0, 削除/改名 3
