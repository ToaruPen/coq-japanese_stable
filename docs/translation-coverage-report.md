# 翻訳カバレッジ分析レポート

- 生成日時: `2026-03-10T16:06:40.840605+00:00`
- 対象ゲームバージョン: `2.0.4`

## 1. カテゴリ別カバレッジ

| カテゴリ | Game IDs | Legacy IDs | Matched | 未訳 | 削除/改名 | カバレッジ |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| ObjectBlueprints | 5220 | 4020 | 4020 | 1200 | 0 | 77.01% |
| Conversations | 200 | 195 | 195 | 5 | 0 | 97.5% |
| Options | 187 | 193 | 187 | 0 | 6 | 100.0% |
| Skills | 21 | 21 | 21 | 0 | 0 | 100.0% |
| Mutations | 82 | 82 | 82 | 0 | 0 | 100.0% |
| Books | 53 | 56 | 53 | 0 | 3 | 100.0% |

## 2. カテゴリ別 未訳トップ10（サンプル）

### ObjectBlueprints
1. `*PooledObject`
2. `ActiveFungus`
3. `ActivePlant`
4. `AmmoBuildDataDisk`
5. `Antelopes_Data`
6. `AoygDagger`
7. `Apes_Data`
8. `Arachnids_Data`
9. `Arconaut`
10. `Arconaut Still`

### Conversations
1. `BaseDynamicShim`
2. `Euclid`
3. `Naphtaali`
4. `PaxKlanq2`
5. `Slynth`

### Options
- 未訳サンプルなし

### Skills
- 未訳サンプルなし

### Mutations
- 未訳サンプルなし

### Books
- 未訳サンプルなし

## 3. 削除/改名エントリ一覧（全件）

### ObjectBlueprints
- 該当なし

### Conversations
- 該当なし

### Options
- `OptionDebugCheats`
- `OptionDebugMissilePaths`
- `OptionDebugMissileShots`
- `OptionDebugMissileTargetChoices`
- `OptionDebugPopulation`
- `OptionDebugZoneBuild`

### Skills
- 該当なし

### Mutations
- 該当なし

### Books
- `AcrossTheSpindle1`
- `AcrossTheSpindle2`
- `AcrossTheSpindle3`

## 4. 全体カバレッジ

- 合計 Game IDs: **5763**
- 合計 Matched IDs: **4558**
- 合計 未訳 IDs: **1205**
- 合計 削除/改名 IDs: **9**
- 全体カバレッジ: **79.09%**

## 5. 100%達成までの工数見積り（仮定ベース）

- 主要残件は `ObjectBlueprints` の未訳 1,200 IDs。
- 仮定A: 1日80 IDs対応（抽出・翻訳適用・検証込み）→ 約15人日。
- 仮定B: 1日120 IDs対応（半自動化前提）→ 約10人日。
- Conversations未訳5件は同時対応可能（0.5人日未満）。
- 削除/改名9件は手動棚卸し対象（0.5〜1人日）。
