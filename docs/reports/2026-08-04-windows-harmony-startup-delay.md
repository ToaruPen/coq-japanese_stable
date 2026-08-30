# Windows 通常公開版での Harmony 起動待ち調査

## 結論

Steam Workshop コメントで報告された「QudJP を有効にすると白または黒い画面のまま応答しなくなり、`harmony.scan_patch_types` を最後にログが止まる」という症状は、QudJP と通常公開版の `Assembly-CSharp.dll` の不一致ではなかった。Windows 版 Caves of Qud に同梱された Harmony 2.2.2 が、多数の QudJP パッチを適用する間に長時間ログを出さない既知の起動待ちと一致する。

同条件の Windows 実機ログでは、`harmony.scan_patch_types` 自体は 9.59 ms で完了していた。その直後、534 個のパッチ型を 1,695 メソッドへ適用する処理に 72,615 ms を要している。この間は次のタイミング行が出ないため、Player.log は走査完了行で止まったように見え、Windows のウィンドウも「応答なし」と判定され得る。処理後には `Bootstrap: initialization complete.` まで到達しており、同じログ位置が必ずデッドロックを示すわけではない。

既存の任意導入 Harmony 2.4.2 更新パッチが、この待ち時間に対する現行の解決策である。過去の Windows 検証では、パッチ適用時間が約 72.6 秒から 9.4 秒へ短縮された。ただし、この更新パッチはゲーム本体の `0Harmony.dll` を置き換えるため、確認済みの通常公開版だけを対象とし、ベータ版や更新後の未確認ビルドには適用しない。

## 受領した報告

- 収集日時: 2026-08-04 22:56 JST
- Steam comment ID: `585056119637054321`
- 環境: Windows、Steam の通常公開版、QudJP 以外の Mod は無効
- 症状: 起動後に白または黒い画面で停止し、数十秒後に「応答がありません」と表示
- 最終ログ: `StartupTiming/v1: phase=harmony.scan_patch_types`
- 比較結果: `lang-experimental` へ切り替えると QudJP を有効にしたまま起動

コメント本文は外部からの信頼できない入力としてローカル inbox に保存し、上記には調査に必要な事実だけを要約した。

## 調査結果

### 配布 DLL と通常公開版の参照先は一致していた

Workshop から取得した QudJP 0.5.02 の `QudJP.dll` は SHA-256 `90d90cc94ecd4cf41f0a886c22b5c47010cde248ebaf4d08c0e4ed78e9d9a27b` で、macOS と Windows の双方で同一だった。アセンブリ参照は `Assembly-CSharp 2.0.211.50` であり、Caves of Qud 1.0.5 の安定版参照と Windows 通常公開版の実 DLL も `2.0.211.50` だった。

この一致により、「Workshop へベータ版用の QudJP.dll を誤って配布した」「通常公開版だけ API 参照が解決できない」という初期仮説は棄却できる。

### ログが止まって見える位置は、実際の遅延位置より一段手前だった

`QudJPMod.PatchByClassProcessor` は、Harmony パッチ型の列挙を終えた時点で `harmony.scan_patch_types` を記録する。その後に全パッチ型を順番に適用し、ループが終了してから `harmony.apply_patch_types` を記録する。詳細タイミングを有効にしていない通常の配布構成では、長いループの途中に進捗行は出ない。

Windows 実機の既存 Player.log には次の結果が残っていた。

| 項目 | 実測値 |
| --- | ---: |
| Harmony パッチ型走査 | 9.59 ms |
| パッチ型数 | 534 |
| 適用メソッド数 | 1,695 |
| Harmony パッチ適用 | 72,615 ms |
| QudJP 初期化全体 | 72,834 ms |

実機のゲームは Steam 通常公開ブランチ、build `24054858`、`Assembly-CSharp 2.0.211.50` だった。ゲーム同梱 `0Harmony.dll` は Harmony `2.2.2.0`、Workshop の QudJP.dll は上記の配布ハッシュと一致していた。報告された最終行と、実測で約 72 秒の無出力区間が始まる位置は同じである。

### `lang-experimental` で起動する理由は断定しない

ベータ版で正常に起動するという利用者の比較結果は、通常公開版側のランタイム依存差という結論と矛盾しない。ただし、今回の調査では `lang-experimental` の実 DLL と Harmony バージョンを直接採取していない。したがって、「ベータ版が Harmony 2.4.2 を同梱している」など、未確認の内部差までは結論に含めない。

## 対応

1. Workshop の日英説明で、Windows の白・黒画面、「応答なし」、`harmony.scan_patch_types` のログ位置を明示する。
2. 任意導入 Harmony 2.4.2 更新パッチの実測効果、可逆性、ゲーム本体 DLL を変更する点、適用可能な版の制限を同じ節にまとめる。
3. 報告者へは、通常公開版でまず 2 分程度待って起動が完了するかを確認してもらう。待ち時間を短縮する場合だけ、公開済みの更新パッチ手順を案内する。
4. 2 分を超えても `harmony.apply_patch_types` または `Bootstrap: initialization complete.` が出ない場合は、別の停止として扱い、個人情報とローカルパスを伏せた Player.log 全体を依頼する。

## 投稿結果

2026-08-04 23:20 JST、次の案内を [Steam Workshop のコメント欄](https://steamcommunity.com/sharedfiles/filedetails/?id=3718988020)へ作成者コメントとして投稿した。投稿後、コメント総数が 40 件から 41 件へ増え、作成者表示、本文、Harmony 配布スレッドへのリンクが公開ページに表示されることを確認した。

ローカル Workshop inbox には bug として調査結果を追記し、既知症状を文書化して Steam で案内済みのため、新規 GitHub Issue の作成は `skipped` と記録した。

```text
ご報告ありがとうございます。Windows通常公開版とPlayer.logを確認しました。

このログは停止箇所そのものではなく、直後のHarmonyパッチ適用中にログが約72.6秒途切れる既知症状と一致します。まず2分ほど待って起動するかご確認ください。

短縮用の任意導入Harmony 2.4.2更新パッチ（検証値: 約72.6秒→9.4秒）:
https://steamcommunity.com/workshop/filedetails/discussion/3718988020/572669660098532087/

ゲーム本体の0Harmony.dllを変更するため、リンク先の対象版・バックアップ・復元手順を確認し、ベータ版や更新後の未確認版には適用しないでください。

2分以上待っても起動せず、Player.logにharmony.apply_patch_typesが出ない場合は、個人情報とパスを伏せたログ全体をご共有ください。
```

## 検証範囲と制約

- `QudJP.dll` と通常公開版 `Assembly-CSharp.dll` のアセンブリ identity を macOS と Windows で照合した。
- Windows 実機の app manifest、Harmony、Workshop 配布物、既存 Player.log を Tailscale 経由で確認した。
- SSH の非対話セッションから起動した `CoQ.exe` は Steam Workshop 購読を読み込まなかった。対話セッションの一時タスクから Steam 起動も試みたが、今回の実行では CoQ プロセスを生成できなかったため、新しい起動時間としては採用していない。
- 検証用に起動したプロセスと一時スケジュールタスクはすべて終了・削除した。ゲーム DLL は変更していない。
- Workshop 説明の BBCode と既存の対象ゲーム版契約はリポジトリの静的検証で確認する。バージョン番号や実測値を新しいテスト契約として固定せず、この時点の調査証拠として本書へ保存する。

## 関連資料

- [Harmony 2.4.2 配布設計](../superpowers/specs/2026-07-15-harmony-2.4.2-distribution-design.md)
- [QudJP v0.5.02 Workshop リリース証拠](2026-07-16-workshop-v0.5.02.md)
- [QudJP v0.5.02 GitHub Release](https://github.com/ToaruPen/coq-japanese_stable/releases/tag/v0.5.02) — `QudJP-Harmony-2.4.2-Windows.zip` と導入・復元手順
