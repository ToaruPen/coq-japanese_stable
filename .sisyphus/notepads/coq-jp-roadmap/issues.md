# Issues

## 2026-03-11 Task 0 PoC
- `net9.0`/`net8.0` テスト実行はランタイム未導入で失敗（`Microsoft.NETCore.App 9/8` 不足）。
- `net48` テスト実行は `mono` ホスト未導入で失敗（ビルドのみ成功）。
- 既存 `Player.log` に他Mod由来の `mprotect returned EACCES` を確認し、macOS Harmony ランタイム失敗リスクは高い。

## 2026-03-11 Task 3 Legacy XML監査
- 現行ゲーム `Base/Books.xml` は XML 不正文字参照（line 1091, col 6）を含み、通常XMLパース不可。ID抽出は regex フォールバックが必要。
- 事前想定（辞書37ファイル）に対し、実観測は辞書JSON35ファイル。件数差分の原因確認が必要。
- Optionsで6件（`OptionDebug*`）がレガシーのみ、Booksで3件（`AcrossTheSpindle1-3`）がレガシーのみ。
