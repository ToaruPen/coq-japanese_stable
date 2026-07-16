# Steam Workshop 概要欄 BBCode 化設計

## 目的

Steam Workshop の英語・日本語概要欄を、Steam が対応する BBCode に合わせて
読みやすく再構成する。特に Apple Silicon 向け説明から高度な Harmony 手順を
分離し、通常の利用者が Rosetta 2 による推奨手順を短時間で理解できる状態にする。

## 対象

- 既定言語の原稿: `steam/workshop_description.en.txt`
- 日本語原稿: `steam/workshop_description.ja.txt`
- 概要欄の言語・ゲームバージョン契約テスト
- Steam Workshop アイテム `3718988020` の英語・日本語概要欄

Mod 本体、Workshop 配布物、Harmony 配布物、ゲームのインストール内容は変更しない。

## 表示構造

英語・日本語で同じセクション順と情報量を維持する。

1. Mod 名と対応バージョン
2. 生成 AI 翻訳に関する注意
3. 概要
4. 主な内容
5. 導入方法
6. 既存セーブ
7. Apple Silicon Mac
8. 問題報告とコントリビューション
9. 注意事項
10. 既知の問題

Steam 公式の Text Formatting で案内されている次のタグだけを使用する。

- セクション見出し: `[h1]`、補助見出し: `[h2]`
- 強調と短いファイル名: `[b]`
- 箇条書き: `[list]` と `[*]`
- 番号付き手順: `[olist]` と `[*]`
- 長いパスとコマンド: `[code]`
- 外部リンク: `[url=URL]表示名[/url]`
- 大きな区切り: `[hr][/hr]`

Markdown のバッククォート、行頭の `*`、裸の外部 URL は使用しない。

## Apple Silicon Mac の情報設計

概要欄には次の内容だけを残す。

1. ネイティブ起動ではゲーム同梱 Harmony により UI 翻訳が適用されない場合がある。
2. 推奨方法は同梱の `Launch CavesOfQud (Rosetta).command` から起動すること。
3. Wrapper の場所を Steam Workshop の既定パスとして示す。
4. Wrapper がゲームを見つけられない場合は、表示される選択画面で `CoQ.app` を選ぶ。
5. Steam から通常起動すると wrapper の設定は適用されないため、問題がある環境では毎回 wrapper を使う。
6. Rosetta 2 がなければ wrapper の案内に従う。
7. ネイティブ起動用 Harmony 2.4.2 の導入・復元・制約は、既存の Steam 配布スレッドへリンクする。

直接起動コマンド、手動 DLL 差し替えパス、Harmony ZIP の展開手順は概要欄から削除する。
これらは高度な手順であり、更新可能な単一の配布スレッドを情報源とする。

## 契約と検証

決定論的テストで次を保証する。

- `steam/workshop_metadata.json` の既定説明は英語原稿を参照する。
- 英語・日本語原稿の両方が Caves of Qud `1.0.5` を示す。
- 英語原稿に日本語見出しが混ざらない。
- 両原稿に必要な Steam BBCode と Harmony 配布スレッド URL が存在する。
- 使用タグが閉じており、許可したタグ以外を含まない。
- Markdown のバッククォート、行頭の `*`、裸の Harmony URL が残らない。
- Workshop VDF の生成後も既定説明が英語で、BBCode がリテラルな改行とともに保持される。

実装後は関連 Python テスト、Workshop VDF 生成テスト、`just python-check`、
`git diff --check` を実行する。

## 公開手順

1. リポジトリ上の英語・日本語原稿とテストをレビューする。
2. 英語・日本語それぞれの Steam 登録予定本文を利用者へ提示する。
3. 現在の会話で明示的な保存許可を得る。
4. Steam の言語別編集画面で保存する。
5. 非認証の英語・日本語ページから、見出し、1.0.5、Rosetta 手順、Harmony スレッドリンクを確認する。

Steam 側の保存は外部公開状態の変更なので、リポジトリ変更の承認とは分けて扱う。

## 非目標

- Harmony 2.4.2 の自動導入または自動更新
- Mod 配布物や Workshop コンテンツの再アップロード
- Harmony 配布スレッド本文の改訂
- Caves of Qud の実ゲーム起動テスト
