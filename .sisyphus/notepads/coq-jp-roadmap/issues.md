# Issues

## 2026-03-11 Task 0 PoC
- `net9.0`/`net8.0` テスト実行はランタイム未導入で失敗（`Microsoft.NETCore.App 9/8` 不足）。
- `net48` テスト実行は `mono` ホスト未導入で失敗（ビルドのみ成功）。
- 既存 `Player.log` に他Mod由来の `mprotect returned EACCES` を確認し、macOS Harmony ランタイム失敗リスクは高い。

## 2026-03-11 Task 3 Legacy XML監査
- 現行ゲーム `Base/Books.xml` は XML 不正文字参照（line 1091, col 6）を含み、通常XMLパース不可。ID抽出は regex フォールバックが必要。
- 事前想定（辞書37ファイル）に対し、実観測は辞書JSON35ファイル。件数差分の原因確認が必要。
- Optionsで6件（`OptionDebug*`）がレガシーのみ、Booksで3件（`AcrossTheSpindle1-3`）がレガシーのみ。

## 2026-03-11 Task 0: Known Issues
- NUnit3TestAdapter 5.0.0 NU1701 warning for netstandard2.0 target — not critical
- [ModuleInitializer] on Mono runtime: unverified, Part B pending
- Part B (game runtime Harmony verification): PENDING manual game launch

## 2026-03-11 Task MODWARN属性修正
- `Player.log` の MODWARN 対象8ファイルを Base XML と比較し、未対応属性のみ削除（翻訳本文は保持）。
- 削除した未対応属性:
  - `Skills.jp.xml`: `skill` / `power` の `Load` と `DisplayName`
  - `HiddenMutations.jp.xml`: ルート `mutations` の `Load` と `mutation` の `DisplayName`
  - `EmbarkModules.jp.xml`: `module` / `window` / `mode` の `Load`
  - `Genotypes.jp.xml`: `genotype` の `Load`
  - `SparkingBaetyls.jp.xml` / `Relics.jp.xml` / `Quests.jp.xml` / `ActivatedAbilities.jp.xml`: ルート要素の `Load`
- `python3 - <<...ET.parse...>>` で8ファイルすべてのXMLパース成功（well-formed）。
- `python3 scripts/validate_xml.py Mods/QudJP/Localization/Skills.jp.xml` は OK。
- `python3 scripts/validate_xml.py Mods/QudJP/Localization/HiddenMutations.jp.xml` は OK。
- `python3 scripts/validate_xml.py Mods/QudJP/Localization/` は完走。既存の警告（重複ID/空テキスト等）は出るが、今回修正対象8ファイルの属性不整合に関する新規エラーはなし。
